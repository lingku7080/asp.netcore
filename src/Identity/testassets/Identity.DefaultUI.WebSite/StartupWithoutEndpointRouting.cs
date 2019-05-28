// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Identity.DefaultUI.WebSite
{
    public class StartupWithoutEndpointRouting : StartupBase<IdentityUser, IdentityDbContext>
    {
        public StartupWithoutEndpointRouting(IConfiguration configuration) : base(configuration)
        {
        }

        public override void ConfigureServices(IServiceCollection services)
        {
            base.ConfigureServices(services);
            services.AddMvc(options => options.EnableEndpointRouting = false);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public override void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            // This prevents running out of file watchers on some linux machines
            var pendingProviders = new Stack<IFileProvider>();
            pendingProviders.Push(env.WebRootFileProvider);
            while (pendingProviders.TryPop(out var currentProvider))
            {
                switch (currentProvider)
                {
                    case PhysicalFileProvider physical:
                        physical.UseActivePolling = false;
                        break;
                    case StaticWebAssetsFileProvider staticWebAssets:
                        staticWebAssets.InnerProvider.UseActivePolling = false;
                        break;
                    case CompositeFileProvider composite:
                        foreach (var childFileProvider in composite.FileProviders)
                        {
                            pendingProviders.Push(childFileProvider);
                        }
                        break;
                    default:
                        throw new InvalidOperationException("Unknown provider");
                }
            }

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseDatabaseErrorPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            app.UseAuthentication();

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseCookiePolicy();

            app.UseMvc();
        }
    }
}
