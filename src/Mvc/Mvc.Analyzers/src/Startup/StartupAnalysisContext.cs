// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Microsoft.AspNetCore.Analyzers
{
    internal class StartupAnalyzerContext
    {
        private readonly object _lock;
        private readonly Dictionary<INamedTypeSymbol, List<object>> _analysesByType;
        private readonly StartupAnalzyer _analyzer;

        public StartupAnalyzerContext(StartupAnalzyer analyzer, StartupSymbols startupSymbols)
        {
            _analyzer = analyzer;
            StartupSymbols = startupSymbols;

            _analysesByType = new Dictionary<INamedTypeSymbol, List<object>>();
            _lock = new object();
        }

        public StartupSymbols StartupSymbols { get; }

        public T? GetRelatedSingletonAnalysis<T>(INamedTypeSymbol type) where T : class
        {
            lock (_lock)
            {
                if (_analysesByType.TryGetValue(type, out var list))
                {
                    for (var i = 0; i < list.Count; i++)
                    {
                        if (list[i] is T item)
                        {
                            return item;
                        }
                    }
                }
            }

            return null;
        }

        public ImmutableArray<T> GetRelatedAnalyses<T>(INamedTypeSymbol type) where T : class
        {
            var items = ImmutableArray.CreateBuilder<T>();
            lock (_lock)
            {
                if (_analysesByType.TryGetValue(type, out var list))
                {
                    for (var i = 0; i < list.Count; i++)
                    {
                        if (list[i] is T item)
                        {
                            items.Add(item);
                        }
                    }
                }
            }

            return items.ToImmutable();
        }

        public void ReportAnalysis(ServicesAnalysis analysis)
        {
            ReportAnalysisCore(analysis.StartupType, analysis);
            _analyzer.OnServicesAnalysisCompleted(analysis);
        }

        public void ReportAnalysis(OptionsAnalysis analysis)
        {
            ReportAnalysisCore(analysis.StartupType, analysis);
            _analyzer.OnOptionsAnalysisCompleted(analysis);
        }

        public void ReportAnalysis(MiddlewareAnalysis analysis)
        {
            ReportAnalysisCore(analysis.StartupType, analysis);
            _analyzer.OnMiddlewareAnalysisCompleted(analysis);
        }

        private void ReportAnalysisCore(INamedTypeSymbol type, object analysis)
        {
            lock (_lock)
            {
                if (!_analysesByType.TryGetValue(type, out var list))
                {
                    list = new List<object>();
                    _analysesByType.Add(type, list);
                }

                list.Add(analysis);
            }
        }
    }
}
