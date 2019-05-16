// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.AspNetCore.Mvc.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class TagHelpersInCodeBlocksAnalyzer : DiagnosticAnalyzer
    {
        public TagHelpersInCodeBlocksAnalyzer()
        {
            TagHelperInCodeBlockDiagnostic = DiagnosticDescriptors.MVC1006_FunctionsContainingTagHelpersMustBeAsyncAndReturnTask;
            SupportedDiagnostics = ImmutableArray.Create(new[] { TagHelperInCodeBlockDiagnostic });
        }

        private DiagnosticDescriptor TagHelperInCodeBlockDiagnostic { get; }

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.ReportDiagnostics);
            context.RegisterCompilationStartAction(context =>
            {
                var symbolCache = new SymbolCache(context.Compilation);

                if (symbolCache.TagHelperRunnerRunAsyncMethodSymbol == null)
                {
                    // No-op if we can't find bits we care about.
                    Debugger.Launch();
                    return;
                }

                var diagnostics = context.Compilation.GetDiagnostics();
                Debugger.Launch();

                context.RegisterOperationAction(context =>
                {
                    Debugger.Launch();
                }, OperationKind.Await, OperationKind.Invocation);
            });
        }

        internal void InitializeWorker(CompilationStartAnalysisContext context, SymbolCache symbolCache)
        {
            context.RegisterOperationAction(context =>
            {
                Debugger.Launch();
            }, OperationKind.Await, OperationKind.Invocation);

            context.RegisterOperationAction(context =>
            {
                var awaitOperation = (IAwaitOperation)context.Operation;

                //if (!IsTagHelperRunnerRunAsync(awaitOperation.TargetMethod, symbolCache))
                //{
                //    return;
                //}

                //var parent = context.Operation.Parent;
                //while (parent != null && !IsParentMethod(parent))
                //{
                //    parent = parent.Parent;
                //}

                //if (parent == null)
                //{
                //    return;
                //}

                //// I'd like to register for invocation operations so I can detect awaits inside of this local function.

                //bool IsParentMethod(IOperation operation)
                //{
                //    if (operation.Kind == OperationKind.LocalFunction)
                //    {
                //        return true;
                //    }

                //    if (operation.Kind == OperationKind.MethodBody)
                //    {
                //        return true;
                //    }

                //    if (operation.Kind == OperationKind.AnonymousFunction)
                //    {
                //        return true;
                //    }

                //    return false;
                //}

            }, OperationKind.Await);

            context.RegisterSymbolStartAction(context =>
            {
                var method = (IMethodSymbol)context.Symbol;

                if (method.IsAsync)
                {
                    return;
                }

                context.RegisterOperationAction(context =>
                {
                    var invocationOperation = (IInvocationOperation)context.Operation;

                    if (!IsTagHelperRunnerRunAsync(invocationOperation.TargetMethod, symbolCache))
                    {
                        return;
                    }

                    //context.ReportDiagnostic(Diagnostic.Create(
                    //    TagHelperInCodeBlockDiagnostic,
                    //    method.Identifier.GetLocation(),
                    //    new[] { "method" }));
                }, OperationKind.Invocation);

            }, SymbolKind.Method);

            /*
             * void Foo()
             * {
             *     await __tagHelperRunner.RunAsync...
             * }
             */

            //context.RegisterSyntaxNodeAction(context =>
            //{
            //    var invocationExpression = (InvocationExpressionSyntax)context.Node;
            //    var symbol = context.SemanticModel.GetSymbolInfo(invocationExpression, context.CancellationToken).Symbol;

            //    if (symbol == null || symbol.Kind != SymbolKind.Method)
            //    {
            //        return;
            //    }

            //    var method = (IMethodSymbol)symbol;

            //    if (!IsTagHelperRunnerRunAsync(method, symbolCache))
            //    {
            //        return;
            //    }

            //    var containingFunction = context.Node.FirstAncestorOrSelf<SyntaxNode>(node =>
            //        node.IsKind(SyntaxKind.ParenthesizedLambdaExpression) ||
            //        node.IsKind(SyntaxKind.AnonymousMethodExpression) ||
            //        node.IsKind(SyntaxKind.LocalFunctionStatement) ||
            //        node.IsKind(SyntaxKind.MethodDeclaration));

            //    if (containingFunction == null)
            //    {
            //        // In practice should never happen because the Razor bits at the bare minimum should be encompassed by a method declaration.
            //        // That being said, if a user were to write malformed code that fulfilled our TagHelper lookup outside of a method block we
            //        // would get a null here.
            //        return;
            //    }

            //    switch (containingFunction)
            //    {
            //        case ParenthesizedLambdaExpressionSyntax parenthesizedLambda:
            //            var lambdaSymbol = (IMethodSymbol)context.SemanticModel.GetSymbolInfo(parenthesizedLambda).Symbol;
            //            if (!lambdaSymbol.IsAsync)
            //            {
            //                context.ReportDiagnostic(Diagnostic.Create(
            //                    TagHelperInCodeBlockDiagnostic,
            //                    parenthesizedLambda.ParameterList.GetLocation(),
            //                    new[] { "lambda" }));
            //            }

            //            break;
            //        case AnonymousMethodExpressionSyntax anonymousMethod:
            //            var anonymousMethodSymbol = (IMethodSymbol)context.SemanticModel.GetSymbolInfo(anonymousMethod).Symbol;
            //            if (!anonymousMethodSymbol.IsAsync)
            //            {
            //                context.ReportDiagnostic(Diagnostic.Create(
            //                    TagHelperInCodeBlockDiagnostic,
            //                    anonymousMethod.DelegateKeyword.GetLocation(),
            //                    new[] { "method" }));
            //            }

            //            break;
            //        case LocalFunctionStatementSyntax localFunction:
            //            var localFunctionReturnType = (INamedTypeSymbol)context.SemanticModel.GetSymbolInfo(localFunction.ReturnType).Symbol;
            //            if (localFunction.Modifiers.IndexOf(SyntaxKind.AsyncKeyword) == -1)
            //            {
            //                context.ReportDiagnostic(Diagnostic.Create(
            //                    TagHelperInCodeBlockDiagnostic,
            //                    localFunction.Identifier.GetLocation(),
            //                    new[] { "local function" }));
            //            }
            //            break;
            //        case MethodDeclarationSyntax methodDeclaration:
            //            var methodDeclarationReturnType = (INamedTypeSymbol)context.SemanticModel.GetSymbolInfo(methodDeclaration.ReturnType).Symbol;
            //            if (methodDeclaration.Modifiers.IndexOf(SyntaxKind.AsyncKeyword) == -1)
            //            {
            //                context.ReportDiagnostic(Diagnostic.Create(
            //                    TagHelperInCodeBlockDiagnostic,
            //                    methodDeclaration.Identifier.GetLocation(),
            //                    new[] { "method" }));
            //            }
            //            break;
            //    }

            //}, SyntaxKind.InvocationExpression);
        }

        private bool IsTagHelperRunnerRunAsync(IMethodSymbol method, SymbolCache symbolCache)
        {
            if (method.IsGenericMethod)
            {
                return false;
            }

            if (method != symbolCache.TagHelperRunnerRunAsyncMethodSymbol)
            {
                return false;
            }

            return true;
        }

        internal readonly struct SymbolCache
        {
            public SymbolCache(Compilation compilation)
            {
                var tagHelperRunnerType = compilation.GetTypeByMetadataName(SymbolNames.TagHelperRunnerTypeName);
                var members = tagHelperRunnerType.GetMembers(SymbolNames.RunAsyncMethodName);

                TagHelperRunnerRunAsyncMethodSymbol = members.Length == 1 ? (IMethodSymbol)members[0] : null;
            }

            public IMethodSymbol TagHelperRunnerRunAsyncMethodSymbol { get; }
        }
    }
}
