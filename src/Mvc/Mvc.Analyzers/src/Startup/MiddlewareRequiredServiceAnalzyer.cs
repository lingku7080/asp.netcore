// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.AspNetCore.Analyzers
{
    internal class MiddlewareRequiredServiceAnalyzer
    {
        private readonly static ImmutableDictionary<string, ImmutableArray<string>> MiddlewareMap = ImmutableDictionary.CreateRange<string, ImmutableArray<string>>(new[]
        {
            new KeyValuePair<string, ImmutableArray<string>>("UseHealthChecks", ImmutableArray.Create<string>(new[]
            {
                "AddHealthChecks",
            })),
        });

        private readonly static ImmutableDictionary<string, ImmutableArray<string>> ServicesMap = ImmutableDictionary.CreateRange<string, ImmutableArray<string>>(new[]
        {
            new KeyValuePair<string, ImmutableArray<string>>("AddMvc", ImmutableArray.Create<string>(new[]
            {
                "AddRouting",
            })),
        });

        private readonly StartupAnalyzerContext _context;

        public MiddlewareRequiredServiceAnalyzer(StartupAnalyzerContext context)
        {
            _context = context;
        }

        public void AnalyzeSymbol(SymbolAnalysisContext context)
        {
            Debug.Assert(context.Symbol.Kind == SymbolKind.NamedType);
            Debug.Assert(StartupFacts.IsStartupClass(_context.StartupSymbols, (INamedTypeSymbol)context.Symbol));

            var type = (INamedTypeSymbol)context.Symbol;

            // Find the services analysis for each of the ConfigureServices methods defined by this class.
            //
            // There should just be one.
            var servicesAnalysis = _context.GetRelatedSingletonAnalysis<ServicesAnalysis>(type);
            if (servicesAnalysis == null)
            {
                return;
            }

            var occluded = new HashSet<string>();
            foreach (var entry in servicesAnalysis.Services)
            {
                occluded.Add(entry.UseMethod.Name);

                if (ServicesMap.TryGetValue(entry.UseMethod.Name, out var additional))
                {
                    foreach (var item in additional)
                    {
                        occluded.Add(item);
                    }
                }
            }

            // Find the middleware analysis for each of the Configure methods defined by this class and validate.
            //
            // Note that this doesn't attempt to handle inheritance scenarios.
            foreach (var middlewareAnalsysis in _context.GetRelatedAnalyses<MiddlewareAnalysis>(type))
            {
                foreach (var middlewareItem in middlewareAnalsysis.Middleware)
                {
                    if (MiddlewareMap.TryGetValue(middlewareItem.UseMethod.Name, out var requiredServices))
                    {
                        foreach (var requiredService in requiredServices)
                        {
                            if (!occluded.Contains(requiredService))
                            {
                                context.ReportDiagnostic(Diagnostic.Create(
                                    StartupAnalzyer.Diagnostics.MiddlewareMissingRequiredServices,
                                    middlewareItem.Operation.Syntax.GetLocation(),
                                    middlewareItem.UseMethod.Name,
                                    requiredService,
                                    servicesAnalysis.ConfigureServicesMethod.Name));
                            }
                        }
                    }
                }
            }
        }
    }
}
