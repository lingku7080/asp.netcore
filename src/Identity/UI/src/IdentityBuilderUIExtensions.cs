// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity.UI;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Microsoft.AspNetCore.Identity
{
    /// <summary>
    /// Default UI extensions to <see cref="IdentityBuilder"/>.
    /// </summary>
    public static class IdentityBuilderUIExtensions
    {
        private static readonly IDictionary<UIFramework, string> _assemblyMap =
            new Dictionary<UIFramework, string>()
            {
                [UIFramework.Bootstrap3] = "Microsoft.AspNetCore.Identity.UI.Views.V3",
                [UIFramework.Bootstrap4] = "Microsoft.AspNetCore.Identity.UI.Views.V4",
            };

        /// <summary>
        /// Adds a default, self-contained UI for Identity to the application using
        /// Razor Pages in an area named Identity.
        /// </summary>
        /// <remarks>
        /// In order to use the default UI, the application must be using <see cref="Microsoft.AspNetCore.Mvc"/>,
        /// <see cref="Microsoft.AspNetCore.StaticFiles"/> and contain a <c>_LoginPartial</c> partial view that
        /// can be found by the application.
        /// </remarks>
        /// <param name="builder">The <see cref="IdentityBuilder"/>.</param>
        /// <returns>The <see cref="IdentityBuilder"/>.</returns>
        public static IdentityBuilder AddDefaultUI(this IdentityBuilder builder)
        {
            builder.AddSignInManager();

            AddRelatedParts(builder);

            builder.Services.ConfigureOptions(
                typeof(IdentityDefaultUIConfigureOptions<>)
                    .MakeGenericType(builder.UserType));
            builder.Services.TryAddTransient<IEmailSender, EmailSender>();

            return builder;
        }

        private static void AddRelatedParts(IdentityBuilder builder)
        {
            var environment = (IWebHostEnvironment)builder
                .Services
                .LastOrDefault(d => d.ServiceType == typeof(IWebHostEnvironment))
                ?.ImplementationInstance;

            var appAssembly = Assembly.Load(environment.ApplicationName);
            var framework = ResolveUIFramework(appAssembly);

            var mvcBuilder = builder.Services
                .AddMvc()
                .ConfigureApplicationPartManager(partManager =>
                {
                    var thisAssembly = typeof(IdentityBuilderUIExtensions).Assembly;
                    var relatedAssemblies = RelatedAssemblyAttribute.GetRelatedAssemblies(thisAssembly, throwOnError: true);
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

        private static UIFramework ResolveUIFramework(Assembly assembly)
        {
            var metadata = assembly.GetCustomAttributes<UIFrameworkAttribute>()
                .Single().Framework;

            return Enum.Parse<UIFramework>(metadata, ignoreCase: true);
        }
    }
}
