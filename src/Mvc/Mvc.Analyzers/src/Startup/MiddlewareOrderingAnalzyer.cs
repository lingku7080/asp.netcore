// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.AspNetCore.Analyzers
{
    internal class MiddlewareOrderingAnalyzer
    {
        // This should probably be a multi-map, but oh-well.
        private readonly static ImmutableDictionary<string, string> MiddlewareHappensAfterMap = ImmutableDictionary.CreateRange<string, string>(new[]
        {
            new KeyValuePair<string, string>("UseAuthorization", "UseAuthentication"),
        });

        private readonly StartupAnalyzerContext _context;

        public MiddlewareOrderingAnalyzer(StartupAnalyzerContext context)
        {
            _context = context;
        }

        public void AnalyzeSymbol(SymbolAnalysisContext context)
        {
            Debug.Assert(context.Symbol.Kind == SymbolKind.NamedType);
            Debug.Assert(StartupFacts.IsStartupClass(_context.StartupSymbols, (INamedTypeSymbol)context.Symbol));

            var type = (INamedTypeSymbol)context.Symbol;

            var middlwareAnalyses = _context.GetRelatedAnalyses<MiddlewareAnalysis>(type);
            foreach (var middlewareAnalsysis in middlwareAnalyses)
            {
                for (var i = 0; i < middlewareAnalsysis.Middleware.Length; i++)
                {
                    var middlewareItem = middlewareAnalsysis.Middleware[i];
                    if (MiddlewareHappensAfterMap.TryGetValue(middlewareItem.UseMethod.Name, out var cannotComeAfter))
                    {
                        for (var j = i; j < middlewareAnalsysis.Middleware.Length; j++)
                        {
                            var candidate = middlewareAnalsysis.Middleware[j];
                            if (string.Equals(cannotComeAfter, candidate.UseMethod.Name, StringComparison.Ordinal))
                            {
                                // Found the other middleware after current one. This is an error.
                                context.ReportDiagnostic(Diagnostic.Create(
                                    StartupAnalzyer.Diagnostics.MiddlewareInvalidOrder,
                                    candidate.Operation.Syntax.GetLocation(),
                                    middlewareItem.UseMethod.Name,
                                    candidate.UseMethod.Name));
                            }
                        }
                    }
                }
            }
        }
    }
}
