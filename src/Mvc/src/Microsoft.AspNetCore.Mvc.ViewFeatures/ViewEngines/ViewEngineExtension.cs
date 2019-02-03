// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.Mvc.ViewEngines
{
    internal static class ViewEngineExtension
    {
        public static async ValueTask<ViewEngineResult> FindViewAsync(
            this IViewEngine viewEngine, 
            ActionContext actionContext, 
            string viewName, 
            string executingFilePath,
            bool isMainPage)
        {
            if (viewEngine is ICompositeViewEngine composite)
            {
                var searchedLocations = Enumerable.Empty<string>();
                for (var i = 0; i < composite.ViewEngines.Count; i++)
                {
                    var result = await FindViewAsync(composite.ViewEngines[i], actionContext, viewName, executingFilePath, isMainPage);
                    if (result.Success)
                    {
                        return result;
                    }

                    searchedLocations = searchedLocations.Concat(result.SearchedLocations);
                }

                return ViewEngineResult.NotFound(viewName, searchedLocations);
            }
            else if (viewEngine is ViewEngineBase viewEngineBase)
            {
                var result = await viewEngineBase.FindViewAsync(actionContext, viewName, executingFilePath, isMainPage);
                return result;
            }
            else
            {
#pragma warning disable CS0618 // Type or member is obsolete
                var getViewResult = viewEngine.GetView(executingFilePath, viewName, isMainPage);
#pragma warning restore CS0618 // Type or member is obsolete
                if (getViewResult.Success)
                {
                    return getViewResult;
                }

#pragma warning disable CS0618 // Type or member is obsolete
                var findViewResult = viewEngine.FindView(actionContext, viewName, isMainPage);
#pragma warning restore CS0618 // Type or member is obsolete
                if (findViewResult.Success)
                {
                    return findViewResult;
                }

                return ViewEngineResult.NotFound(viewName, getViewResult.SearchedLocations.Concat(findViewResult.SearchedLocations));
            }
        }
    }
}
