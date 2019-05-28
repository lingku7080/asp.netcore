// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.InteropServices;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace Microsoft.AspNetCore
{
    /// <summary>
    /// A <see cref="IFileProvider"/> for serving static web assets during development.
    /// </summary>
    public class StaticWebAssetsFileProvider : IFileProvider
    {
        private static readonly StringComparison FileSystemBasePathComparisonMode = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ?
            StringComparison.OrdinalIgnoreCase :
            StringComparison.Ordinal;

        private readonly string _pathPrefix;

        /// <summary>
        /// Initializes a new instance of <see cref="StaticWebAssetsFileProvider"/>.
        /// </summary>
        /// <param name="pathPrefix">The path prefix under which the files in the <paramref name="contentRoot"/> folder will
        /// be mapped.</param>
        /// <param name="contentRoot">The absolute path to the content root associated with the static web assets.</param>
        public StaticWebAssetsFileProvider(string pathPrefix, string contentRoot)
        {
            _pathPrefix = pathPrefix.StartsWith("/") ? pathPrefix : "/" + pathPrefix;
            InnerProvider = new PhysicalFileProvider(contentRoot);
        }

        /// <summary>
        /// Gets the underlying <see cref="PhysicalFileProvider"/> for this <see cref="StaticWebAssetsFileProvider"/>.
        /// </summary>
        public PhysicalFileProvider InnerProvider { get; }

        /// <inheritdoc />
        public IDirectoryContents GetDirectoryContents(string subpath)
        {
            if (!subpath.StartsWith(_pathPrefix, FileSystemBasePathComparisonMode))
            {
                return NotFoundDirectoryContents.Singleton;
            }
            else
            {
                return InnerProvider.GetDirectoryContents(subpath.Substring(_pathPrefix.Length));
            }
        }

        /// <inheritdoc />
        public IFileInfo GetFileInfo(string subpath)
        {
            if (!subpath.StartsWith(_pathPrefix, FileSystemBasePathComparisonMode))
            {
                return new NotFoundFileInfo(subpath);
            }
            else
            {
                return InnerProvider.GetFileInfo(subpath.Substring(_pathPrefix.Length));
            }
        }

        /// <inheritdoc />
        public IChangeToken Watch(string filter)
        {
            return InnerProvider.Watch(filter);
        }
    }
}
