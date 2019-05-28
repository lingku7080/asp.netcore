// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Microsoft.AspNetCore.Identity.FunctionalTests
{
    public class ServerFactory<TStartup, TContext> : WebApplicationFactory<TStartup>
        where TStartup : class
        where TContext : DbContext
    {
        private readonly SqliteConnection _connection
            = new SqliteConnection($"DataSource=:memory:");

        public ServerFactory()
        {
            _connection.Open();

            ClientOptions.AllowAutoRedirect = false;
            ClientOptions.BaseAddress = new Uri("https://localhost");
        }

        public string BootstrapFrameworkVersion { get; set; } = "V4";

        protected override IHostBuilder CreateHostBuilder()
        {
            var builder = base.CreateHostBuilder();

            builder.ConfigureWebHost(whb =>
            {
                whb.UseStartup<TStartup>();
                whb.UseSetting(WebHostDefaults.ApplicationKey, "Identity.DefaultUI.WebSite");
                whb.UseSetting(WebHostDefaults.StartupAssemblyKey, "Identity.DefaultUI.WebSite");

                whb.ConfigureServices(sc =>
                {
                    sc.SetupTestDatabase<TContext>(_connection)
                        .AddMvc()
                        // Mark the cookie as essential for right now, as Identity uses it on
                        // several places to pass important data in post-redirect-get flows.
                        .AddCookieTempDataProvider(o => o.Cookie.IsEssential = true);
                });

                whb.Configure(ab =>
                {
                    var factory = ab.ApplicationServices.GetRequiredService<IServiceScopeFactory>();
                    using var scope = factory.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<TContext>();
                    context.Database.EnsureCreated();
                });

                UpdateStaticAssets(whb);
                UpdateApplicationParts(whb);
            });

            return builder;
        }

        private void UpdateApplicationParts(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services => AddRelatedParts(services, BootstrapFrameworkVersion));

            void AddRelatedParts(IServiceCollection services, string framework)
            {
                var _assemblyMap =
                    new Dictionary<string, string>()
                    {
                        ["V3"] = "Microsoft.AspNetCore.Identity.UI.Views.V3",
                        ["V4"] = "Microsoft.AspNetCore.Identity.UI.Views.V4"
                    };
                var mvcBuilder = services
                .AddMvc()
                .ConfigureApplicationPartManager(partManager =>
                {
                    var relatedAssembly = typeof(IdentityBuilderUIExtensions).Assembly;
                    var relatedAssemblies = RelatedAssemblyAttribute.GetRelatedAssemblies(relatedAssembly, throwOnError: true);
                    var relatedParts = relatedAssemblies.ToDictionary(
                        ra => ra,
                        CompiledRazorAssemblyApplicationPartFactory.GetDefaultApplicationParts);

                    var selectedFrameworkAssembly = _assemblyMap[framework];

                    foreach (var kvp in relatedParts)
                    {
                        var assemblyName = kvp.Key.GetName().Name;
                        if (!IsAssemblyForFramework(selectedFrameworkAssembly, assemblyName))
                        {
                            RemoveParts(partManager, kvp.Value);
                        }
                        else
                        {
                            AddParts(partManager, kvp.Value);
                        }
                    }

                    bool IsAssemblyForFramework(string frameworkAssembly, string assemblyName) =>
                        string.Equals(assemblyName, frameworkAssembly, StringComparison.OrdinalIgnoreCase);

                    void RemoveParts(
                        ApplicationPartManager manager,
                        IEnumerable<ApplicationPart> partsToRemove)
                    {
                        for (var i = 0; i < manager.ApplicationParts.Count; i++)
                        {
                            var part = manager.ApplicationParts[i];
                            if (partsToRemove.Any(p => string.Equals(
                                    p.Name,
                                    part.Name,
                                    StringComparison.OrdinalIgnoreCase)))
                            {
                                manager.ApplicationParts.Remove(part);
                            }
                        }
                    }

                    void AddParts(
                        ApplicationPartManager manager,
                        IEnumerable<ApplicationPart> partsToAdd)
                    {
                        foreach (var part in partsToAdd)
                        {
                            if (!manager.ApplicationParts.Any(p => p.GetType() == part.GetType() &&
                                string.Equals(p.Name, part.Name, StringComparison.OrdinalIgnoreCase)))
                            {
                                manager.ApplicationParts.Add(part);
                            }
                        }
                    }
                });
            }
        }

        private void UpdateStaticAssets(IWebHostBuilder builder)
        {
            var manifestPath = new Uri(typeof(ServerFactory<,>).Assembly.CodeBase).LocalPath;
            builder.ConfigureWebHostEnvironment(env =>
            {
                if (env.WebRootFileProvider is CompositeFileProvider composite)
                {
                    var originalWebRoot = composite.FileProviders.First();
                    env.WebRootFileProvider = originalWebRoot;
                }
            });

            builder.UseStaticWebAssets(Path.Combine(manifestPath, $"Testing.DefaultWebSite.StaticWebAssets.{BootstrapFrameworkVersion}.xml"));
        }

        public override void EnsureDatabaseCreated()
        {
            using (var scope = Services.CreateScope())
            {
                scope.ServiceProvider.GetService<TContext>().Database.EnsureCreated();
            }
        }

        protected override void Dispose(bool disposing)
        {
            _connection.Dispose();

            base.Dispose(disposing);
        }
    }
}
