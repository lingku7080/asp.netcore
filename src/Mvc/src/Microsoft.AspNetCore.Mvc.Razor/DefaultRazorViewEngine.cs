// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.ViewEngines;

namespace Microsoft.AspNetCore.Mvc.Razor
{
    internal class DefaultRazorViewEngine : ViewEngineBase
    {
        private readonly RazorViewLookup _fileLookup;

        public DefaultRazorViewEngine(RazorViewLookup fileLookup)
        {
            _fileLookup = fileLookup ?? throw new ArgumentNullException(nameof(fileLookup));
        }

        public override async ValueTask<ViewEngineResult> FindViewAsync(ActionContext actionContext, string viewName, string executingFilePath, bool isMainPage)
        {
            var cacheResult = await _fileLookup.LocateViewAsync(actionContext, viewName, executingFilePath, isMainPage);
            if (!cacheResult.Success)
            {
                return ViewEngineResult.NotFound(viewName, cacheResult.SearchedLocations);
            }

            var razorPage = cacheResult.ViewEntry.PageFactory();
            var viewStarts = Array.Empty<IRazorPage>();

            if (isMainPage)
            {
                viewStarts = new IRazorPage[cacheResult.ViewStartEntries.Count];
                for (var i = 0; i < viewStarts.Length; i++)
                {
                    viewStarts[i] = cacheResult.ViewStartEntries[i].PageFactory();
                }
            }

            var razorView = new RazorView(razorPage, viewStarts);
            return ViewEngineResult.Found(viewName, razorView);
        }
    }
}
