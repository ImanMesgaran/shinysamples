﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Acr.UserDialogs;
using Prism.Navigation;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Shiny.Beacons;
using Shiny.Logging;


namespace Samples.Beacons
{
    public class MonitoringViewModel : ViewModel
    {
        readonly IBeaconManager beaconManager;
        readonly IUserDialogs dialogs;


        public MonitoringViewModel(INavigationService navigator,
                                   IUserDialogs dialogs,
                                   IBeaconManager beaconManager)
        {
            this.dialogs = dialogs;
            this.beaconManager = beaconManager;

            this.Add = navigator.NavigateCommand(
                "CreateBeacon",
                p => p.Add("Monitoring", true)
            );

            this.Load = ReactiveCommand.CreateFromTask(async () =>
            {
                var regions = await this.beaconManager.GetMonitoredRegions();

                this.Regions = regions
                    .Select(x => new CommandItem
                    {
                        Text = $"{x.Identifier}",
                        Detail = $"{x.Uuid}/{x.Major ?? 0}/{x.Minor ?? 0}",
                        PrimaryCommand = ReactiveCommand.CreateFromTask(async () =>
                        {
                            await this.beaconManager.StopMonitoring(x);
                            this.Load.Execute(null);
                        })
                    })
                    .ToList();
            });

            this.StopAllMonitoring = ReactiveCommand.CreateFromTask(async () =>
            {
                var result = await dialogs.ConfirmAsync("Are you sure you wish to stop all monitoring");
                if (result)
                {
                    await this.beaconManager.StopAllMonitoring();
                    this.Load.Execute(null);
                }
            });
        }


        public ICommand Load { get; }
        public ICommand Add { get; }
        public ICommand StopAllMonitoring { get; }
        [Reactive] public IList<CommandItem> Regions { get; private set; }


        public override void OnAppearing()
        {
            base.OnAppearing();
            this.Load.Execute(null);
        }


        public override void OnNavigatingTo(INavigationParameters parameters)
        {
            try
            {
                var newRegion = parameters.GetValue<BeaconRegion>(nameof(BeaconRegion));
                if (newRegion != null)
                    this.beaconManager.StartMonitoring(newRegion);
            }
            catch (Exception ex)
            {
                Log.Write(ex);
                this.dialogs.Alert(ex.ToString());
            }
        }
    }
}