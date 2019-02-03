// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace Microsoft.AspNetCore.Mvc.ViewEngines
{
    public abstract class ViewEngineBase : IViewEngine
    {
        /// <summary>
        /// Finds the view with the given <paramref name="viewName"/> using view locations and information from the
        /// <paramref name="actionContext"/>.
        /// </summary>
        /// <param name="actionContext">The <see cref="ActionContext"/>.</param>
        /// <param name="viewName">The name or path of the view that is rendered to the response.</param>
        /// <param name="executingFilePath">The absolute path to the currently-executing view, if any.</param>
        /// <param name="isMainPage">Determines if the page being found is the main page for an action.</param>
        /// <returns>A <see cref="ValueTask"/> that on completion returns a <see cref="ViewEngineResult"/>.</returns>
        public abstract ValueTask<ViewEngineResult> FindViewAsync(ActionContext actionContext, string viewName, string executingFilePath, bool isMainPage);

        ViewEngineResult IViewEngine.FindView(ActionContext context, string viewName, bool isMainPage)
            => FindViewAsync(context, viewName, executingFilePath: null, isMainPage).GetAwaiter().GetResult();

        ViewEngineResult IViewEngine.GetView(string executingFilePath, string viewPath, bool isMainPage)
            => FindViewAsync(actionContext: null, viewPath, executingFilePath, isMainPage).GetAwaiter().GetResult();
    }
}
