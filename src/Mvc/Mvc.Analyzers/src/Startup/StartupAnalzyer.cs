// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.AspNetCore.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public partial class StartupAnalzyer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => Diagnostics.SupportedDiagnostics;

        public override void Initialize(AnalysisContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterCompilationStartAction(OnCompilationStart);
        }

        private void OnCompilationStart(CompilationStartAnalysisContext context)
        {
            var symbols = new StartupSymbols(context.Compilation);

            // Don't run analyzer if ASP.NET Core types cannot be found
            if (!symbols.HasRequiredSymbols)
            {
                return;
            }

            context.RegisterSymbolStartAction(context =>
            {
                var type = (INamedTypeSymbol)context.Symbol;
                if (!StartupFacts.IsStartupClass(symbols, type))
                {
                    // Not a startup class, nothing to do.
                    return;
                }

                // This analyzer fans out a bunch of jobs. The context will capture the results of doing analysis
                // on the startup code, so that other analyzers that run later can examine them.
                var startupAnalysisContext = new StartupAnalyzerContext(this, symbols);

                var services = new ServicesAnalyzer(startupAnalysisContext);
                var options = new OptionsAnalyzer(startupAnalysisContext);
                var middleware = new MiddlewareAnalyzer(startupAnalysisContext);

                context.RegisterOperationBlockStartAction(context =>
                {
                    if (context.OwningSymbol.Kind != SymbolKind.Method)
                    {
                        return;
                    }

                    var method = (IMethodSymbol)context.OwningSymbol;
                    if (StartupFacts.IsConfigureServices(symbols, method))
                    {
                        OnConfigureServicesMethodFound(method);

                        services.AnalyzeConfigureServices(context);
                        options.AnalyzeConfigureServices(context);
                    }

                    if (StartupFacts.IsConfigure(symbols, method))
                    {
                        OnConfigureMethodFound(method);

                        middleware.AnalyzeConfigureMethod(context);
                    }
                });

                // Run after analyses have had a chance to finish to add diagnostics.
                context.RegisterSymbolEndAction(new MiddlewareOrderingAnalyzer(startupAnalysisContext).AnalyzeSymbol);
                context.RegisterSymbolEndAction(new MiddlewareRequiredServiceAnalyzer(startupAnalysisContext).AnalyzeSymbol);
                context.RegisterSymbolEndAction(new UseMvcAnalyzer(startupAnalysisContext).AnalyzeSymbol);
                context.RegisterSymbolEndAction(new BuildServiceProviderValidator(startupAnalysisContext).AnalyzeSymbol);

            }, SymbolKind.NamedType);
        }
    }
}
