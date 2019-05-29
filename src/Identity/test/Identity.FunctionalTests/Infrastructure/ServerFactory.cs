// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
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

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseStartup<TStartup>();

            builder.ConfigureServices(sc =>
            {
                sc.SetupTestDatabase<TContext>(_connection)
                    .AddMvc()
                    // Mark the cookie as essential for right now, as Identity uses it on
                    // several places to pass important data in post-redirect-get flows.
                    .AddCookieTempDataProvider(o => o.Cookie.IsEssential = true);
            });

            UpdateStaticAssets(builder);
            UpdateApplicationParts(builder);
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

                    var partDescriptions = partManager.ApplicationParts.Select(p => (p.GetType().Name, p.Name)).ToArray();
                });
            }
        }

        private void UpdateStaticAssets(IWebHostBuilder builder)
        {
            var manifestPath = Path.GetDirectoryName(new Uri(typeof(ServerFactory<,>).Assembly.CodeBase).LocalPath);
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

        protected override TestServer CreateServer(IWebHostBuilder builder)
        {
            var server = base.CreateServer(builder);
            EnsureDatabaseCreated(server.Host.Services);

            return server;
        }

        protected override IHost CreateHost(IHostBuilder builder)
        {
            var host = base.CreateHost(builder);
            EnsureDatabaseCreated(host.Services);
            return host;
        }

        public void EnsureDatabaseCreated(IServiceProvider services)
        {
            using (var scope = services.CreateScope())
            {
                scope.ServiceProvider.GetService<TContext>()?.Database?.EnsureCreated();
            }
        }

        protected override void Dispose(bool disposing)
        {
            _connection.Dispose();

            base.Dispose(disposing);
        }
    }
}
