using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ZNetCS.AspNetCore.ResumingFileResults.DependencyInjection;


namespace Samples.Api
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddResumingFileResult();
            services.AddMvc();
            //services
            //    .AddAuthentication("BasicAuthentication")
            //    .AddScheme<AuthenticationSchemeOptions, BasiAuthenticationHandler>("BasicAuthentication", null);
        }


        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
                app.UseDeveloperExceptionPage();

            //app.UseAuthentication();
            app.UseMvc();
        }
    }
}
