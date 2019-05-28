// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Microsoft.AspNetCore.Identity.UI
{
    /// <summary>
    /// Indicates the UI framework in use on the main Application.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, Inherited = false, AllowMultiple = false)]
    public class UIFrameworkAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of <see cref="UIFrameworkAttribute"/>.
        /// </summary>
        /// <param name="framework">The framework. Valid options are Bootstrap3 and Bootstrap4</param>
        public UIFrameworkAttribute(string framework)
        {
            Framework = framework;
        }

        /// <summary>
        /// Gets the UI framework version in use with Identity UI.
        /// </summary>
        public string Framework { get; }
    }
}
