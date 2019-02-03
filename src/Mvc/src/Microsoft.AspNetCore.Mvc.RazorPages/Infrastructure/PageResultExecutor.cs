// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Mvc.ViewFeatures.Filters;

namespace Microsoft.AspNetCore.Mvc.RazorPages.Infrastructure
{
    /// <summary>
    /// Executes a Razor Page.
    /// </summary>
    public class PageResultExecutor : ViewExecutor
    {
        /// <summary>
        /// Creates a new <see cref="PageResultExecutor"/>.
        /// </summary>
        /// <param name="writerFactory">The <see cref="IHttpResponseStreamWriterFactory"/>.</param>
        /// <param name="compositeViewEngine">The <see cref="ICompositeViewEngine"/>.</param>
        /// <param name="diagnosticListener">The <see cref="DiagnosticListener"/>.</param>
        public PageResultExecutor(
            IHttpResponseStreamWriterFactory writerFactory,
            ICompositeViewEngine compositeViewEngine,
            DiagnosticListener diagnosticListener)
            : base(writerFactory, compositeViewEngine, diagnosticListener)
        {
        }

        /// <summary>
        /// Executes a Razor Page asynchronously.
        /// </summary>
        public virtual Task ExecuteAsync(PageContext pageContext, PageResult result)
        {
            if (pageContext == null)
            {
                throw new ArgumentNullException(nameof(pageContext));
            }

            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            if (result.Model != null)
            {
                pageContext.ViewData.Model = result.Model;
            }

            OnExecuting(pageContext);

            var viewStarts = new IRazorPage[pageContext.ViewStartFactories.Count];
            for (var i = 0; i < pageContext.ViewStartFactories.Count; i++)
            {
                viewStarts[i] = pageContext.ViewStartFactories[i]();
            }

            var viewContext = result.Page.ViewContext;
            var pageAdapter = new RazorPageAdapter(result.Page, pageContext.ActionDescriptor.DeclaredModelTypeInfo);

            viewContext.View = new RazorView(
                pageAdapter,
                viewStarts)
            {
                OnAfterPageActivated = (page, currentViewContext) =>
                {
                    if (page != pageAdapter)
                    {
                        return;
                    }

                    // ViewContext is always activated with the "right" ViewData<T> type.
                    // Copy that over to the PageContext since PageContext.ViewData is exposed
                    // as the ViewData property on the Page that the user works with.
                    pageContext.ViewData = currentViewContext.ViewData;
                },
            };

            return ExecuteAsync(viewContext, result.ContentType, result.StatusCode);
        }

        private void OnExecuting(PageContext pageContext)
        {
            var viewDataValuesProvider = pageContext.HttpContext.Features.Get<IViewDataValuesProviderFeature>();
            if (viewDataValuesProvider != null)
            {
                viewDataValuesProvider.ProvideViewDataValues(pageContext.ViewData);
            }
        }
    }
}
