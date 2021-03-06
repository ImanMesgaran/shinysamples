﻿using System;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using Shiny;
using Acr.UserDialogs;
using Shiny.BluetoothLE.Central;
using ReactiveUI.Fody.Helpers;


namespace Samples.BluetoothLE
{
    public class GattCharacteristicViewModel : ViewModel
    {
        readonly IUserDialogs dialogs;
        IDisposable watcher;


        public GattCharacteristicViewModel(IGattCharacteristic characteristic, IUserDialogs dialogs)
        {
            this.Characteristic = characteristic;
            this.dialogs = dialogs;
        }


        public IGattCharacteristic Characteristic { get; }


        [Reactive] public string Value { get; private set; }
        [Reactive] public bool IsNotifying { get; private set; }
        [Reactive] public bool IsValueAvailable { get; private set; }
        [Reactive] public DateTime LastValue { get; private set; }
        public Guid Uuid => this.Characteristic.Uuid;
        public Guid ServiceUuid => this.Characteristic.Service.Uuid;
        public string Description => this.Characteristic.Description;
        public string Properties => this.Characteristic.Properties.ToString();


        public void Select()
        {
            var cfg = new ActionSheetConfig()
                .SetTitle($"{this.Description} - {this.Uuid}")
                .SetCancel();

            if (this.Characteristic.CanWriteWithResponse())
                cfg.Add("Write With Response", () => this.DoWrite(true));

            if (this.Characteristic.CanWriteWithoutResponse())
                cfg.Add("Write Without Response", () => this.DoWrite(false));

            if (this.Characteristic.CanWrite())
                cfg.Add("Send Test BLOB", this.SendBlob);

            if (this.Characteristic.CanRead())
                cfg.Add("Read", this.DoRead);

            if (this.Characteristic.CanNotify())
            {
                var txt = this.Characteristic.IsNotifying ? "Stop Notifying" : "Notify";
                cfg.Add(txt, this.ToggleNotify);
            }
            if (cfg.Options.Any())
                this.dialogs.ActionSheet(cfg.SetCancel());
        }


        async void SendBlob()
        {
            var useReliableWrite = await this.dialogs.ConfirmAsync(new ConfirmConfig()
                .UseYesNo()
                .SetTitle("Confirm")
                .SetMessage("Use reliable write transaction?")
            );
            var cts = new CancellationTokenSource();
            var bytes = Encoding.UTF8.GetBytes(RandomString(5000));
            var dlg = this.dialogs.Loading("Sending Blob", () => cts.Cancel(), "Cancel");
            var sw = new Stopwatch();
            sw.Start();

            var sub = this.Characteristic
                .BlobWrite(bytes, useReliableWrite)
                .Subscribe(
                    s => dlg.Title = $"Sending Blob - Sent {s.Position} of {s.TotalLength} bytes",
                    ex =>
                    {
                        dlg.Dispose();
                        this.dialogs.Toast("Failed writing blob - " + ex);
                        sw.Stop();
                    },
                    () =>
                    {
                        dlg.Dispose();
                        sw.Stop();

                        var pre = useReliableWrite ? "reliable write" : "write";
                        this.dialogs.Toast($"BLOB {pre} took " + sw.Elapsed);
                    }
                );

            cts.Token.Register(() => sub.Dispose());
        }


        async void DoWrite(bool withResponse)
        {
            var utf8 = await this.dialogs.ConfirmAsync("Write value from UTF8 or HEX?", okText: "UTF8", cancelText: "HEX");
            var result = await this.dialogs.PromptAsync("Please enter a write value", this.Description);

            if (result.Ok && !String.IsNullOrWhiteSpace(result.Text))
            {
                var v = result.Text.Trim();
                var bytes = utf8 ? Encoding.UTF8.GetBytes(v) : v.FromHex();
                if (withResponse)
                {
                    this.Characteristic
                        .Write(bytes)
                        .Timeout(TimeSpan.FromSeconds(2))
                        .Subscribe(
                            x => this.dialogs.Toast("Write Complete"),
                            ex => this.dialogs.Alert(ex.ToString())
                        );
                }
                else
                {
                    this.Characteristic
                        .WriteWithoutResponse(bytes)
                        .Timeout(TimeSpan.FromSeconds(2))
                        .Subscribe(
                            x => this.dialogs.Toast("Write Without Response Complete"),
                            ex => this.dialogs.Alert(ex.ToString())
                        );
                }
            }
        }


        async void ToggleNotify()
        {
            if (this.Characteristic.IsNotifying)
            {
                this.watcher?.Dispose();
                this.IsNotifying = false;
            }
            else
            {
                this.IsNotifying = true;
                var utf8 = await this.dialogs.ConfirmAsync(
                    "Display Value as UTF8 or HEX?",
                    okText: "UTF8",
                    cancelText: "HEX"
                );
                this.watcher = this.Characteristic
                    .RegisterAndNotify()
                    .SubOnMainThread(
                        x => this.SetReadValue(x, utf8),
                        ex => this.dialogs.Alert(ex.ToString())
                    );
            }
        }


        async void DoRead()
        {
            var utf8 = await this.dialogs.ConfirmAsync(
                "Display Value as UTF8 or HEX?",
                okText: "UTF8",
                cancelText: "HEX"
            );
            this.Characteristic
                .Read()
                .Timeout(TimeSpan.FromSeconds(2))
                .Subscribe(
                    x => this.SetReadValue(x, utf8),
                    ex => this.dialogs.Alert(ex.ToString())
                );
        }


        void SetReadValue(CharacteristicGattResult result, bool fromUtf8) => Xamarin.Forms.Device.BeginInvokeOnMainThread(() =>
        {
            this.IsValueAvailable = true;
            this.LastValue = DateTime.Now;

            if (result.Data == null)
                this.Value = "EMPTY";

            else
                this.Value = fromUtf8
                    ? Encoding.UTF8.GetString(result.Data, 0, result.Data.Length)
                    : BitConverter.ToString(result.Data);
        });


        static readonly Random random = new Random();
        static string RandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }
    }
}
