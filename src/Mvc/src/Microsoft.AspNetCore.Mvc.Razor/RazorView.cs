// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.AspNetCore.Mvc.Razor
{
    /// <summary>
    /// Default implementation for <see cref="IView"/> that executes one or more <see cref="IRazorPage"/>
    /// as parts of its execution.
    /// </summary>
    public class RazorView : IView
    {
        public RazorView(
            IRazorPage razorPage,
            IReadOnlyList<IRazorPage> viewStartPages)
        {
            RazorPage = razorPage ?? throw new ArgumentNullException(nameof(razorPage));
            ViewStartPages = viewStartPages ?? throw new ArgumentNullException(nameof(viewStartPages));
        }

        /// <summary>
        /// Initializes a new instance of <see cref="RazorView"/>
        /// </summary>
        /// <param name="viewEngine">The <see cref="IRazorViewEngine"/> used to locate Layout pages.</param>
        /// <param name="pageActivator">The <see cref="IRazorPageActivator"/> used to activate pages.</param>
        /// <param name="viewStartPages">The sequence of <see cref="IRazorPage" /> instances executed as _ViewStarts.
        /// </param>
        /// <param name="razorPage">The <see cref="IRazorPage"/> instance to execute.</param>
        /// <param name="htmlEncoder">The HTML encoder.</param>
        /// <param name="diagnosticListener">The <see cref="DiagnosticListener"/>.</param>
        [Obsolete("This constructor is obsolete and is no longer used by the runtime.")]
        public RazorView(
            IRazorViewEngine viewEngine,
            IRazorPageActivator pageActivator,
            IReadOnlyList<IRazorPage> viewStartPages,
            IRazorPage razorPage,
            HtmlEncoder htmlEncoder,
            DiagnosticListener diagnosticListener)
            : this(razorPage, viewStartPages)
        {
        }

        /// <inheritdoc />
        public string Path => RazorPage.Path;

        /// <summary>
        /// Gets <see cref="IRazorPage"/> instance that the views executes on.
        /// </summary>
        public IRazorPage RazorPage { get; }

        /// <summary>
        /// Gets the sequence of _ViewStart <see cref="IRazorPage"/> instances that are executed by this view.
        /// </summary>
        public IReadOnlyList<IRazorPage> ViewStartPages { get; }

        internal Action<IRazorPage, ViewContext> OnAfterPageActivated { get; set; }

        /// <inheritdoc />
        public virtual Task RenderAsync(ViewContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var viewExecutor = context.HttpContext.RequestServices.GetRequiredService<RazorViewExecutor>();
            return viewExecutor.ExecuteAsync(context, this);
        }
    }
}
