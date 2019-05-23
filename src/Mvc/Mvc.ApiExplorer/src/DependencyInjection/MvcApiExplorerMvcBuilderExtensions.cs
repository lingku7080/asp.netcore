// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Extensions for configuring ApiExplorer using an <see cref="IMvcBuilder"/>.
    /// </summary>
    public static class MvcApiExplorerMvcBuilderExtensions
    {
        /// <summary>
        /// Configures <see cref="IMvcBuilder"/> to use ApiExplorer.
        /// </summary>
        /// <param name="builder">The <see cref="IMvcBuilder"/>.</param>
        /// <returns>The <see cref="IMvcBuilder"/>.</returns>
        public static IMvcBuilder AddApiExplorer(this IMvcBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            MvcApiExplorerMvcCoreBuilderExtensions.AddApiExplorerServices(builder.Services);
            return builder;
        }
    }
}
