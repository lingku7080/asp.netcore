// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.AspNetCore.Mvc.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class TagHelpersInCodeBlocksAnalyzer : ViewFeatureAnalyzerBase
    {
        public TagHelpersInCodeBlocksAnalyzer()
            : base(DiagnosticDescriptors.MVC1006_FunctionsContainingTagHelpersMustBeAsyncAndReturnTask)
        {
        }

        protected override void InitializeWorker(ViewFeaturesAnalyzerContext analyzerContext)
        {
            analyzerContext.Context.RegisterSyntaxNodeAction(context =>
            {
                var invocationExpression = (InvocationExpressionSyntax)context.Node;
                var symbol = context.SemanticModel.GetSymbolInfo(invocationExpression, context.CancellationToken).Symbol;
                if (symbol == null || symbol.Kind != SymbolKind.Method)
                {
                    return;
                }

                var method = (IMethodSymbol)symbol;
                if (!IsTagHelperRunnerRunAsync(invocationExpression, method))
                {
                    return;
                }

                var containingFunction = context.Node.FirstAncestorOrSelf<SyntaxNode>(node =>
                    node.IsKind(SyntaxKind.ParenthesizedLambdaExpression) ||
                    node.IsKind(SyntaxKind.AnonymousMethodExpression) ||
                    node.IsKind(SyntaxKind.LocalFunctionStatement) ||
                    node.IsKind(SyntaxKind.MethodDeclaration));

                if (containingFunction == null)
                {
                    // In practice should never happen because the Razor bits at the bare minimum should be encompassed by a method declaration.
                    // That being said, if a user were to write malformed code that fulfilled our TagHelper lookup outside of a method block we
                    // would get a null here.
                    return;
                }

                switch (containingFunction)
                {
                    case ParenthesizedLambdaExpressionSyntax parenthesizedLambda:
                        var lambdaSymbol = (IMethodSymbol)context.SemanticModel.GetSymbolInfo(parenthesizedLambda).Symbol;
                        if (!lambdaSymbol.IsAsync ||
                            !analyzerContext.TaskType.IsAssignableFrom(lambdaSymbol.ReturnType))
                        {
                            context.ReportDiagnostic(Diagnostic.Create(
                                SupportedDiagnostic,
                                parenthesizedLambda.ParameterList.GetLocation(),
                                new[] { "lambda" }));
                        }

                        break;
                    case AnonymousMethodExpressionSyntax anonymousMethod:
                        var anonymousMethodSymbol = (IMethodSymbol)context.SemanticModel.GetSymbolInfo(anonymousMethod).Symbol;
                        if (!anonymousMethodSymbol.IsAsync ||
                            !analyzerContext.TaskType.IsAssignableFrom(anonymousMethodSymbol.ReturnType))
                        {
                            context.ReportDiagnostic(Diagnostic.Create(
                                SupportedDiagnostic,
                                anonymousMethod.DelegateKeyword.GetLocation(),
                                new[] { "method" }));
                        }

                        break;
                    case LocalFunctionStatementSyntax localFunction:
                        var localFunctionReturnType = (INamedTypeSymbol)context.SemanticModel.GetSymbolInfo(localFunction.ReturnType).Symbol;
                        if (!analyzerContext.TaskType.IsAssignableFrom(localFunctionReturnType) ||
                            localFunction.Modifiers.IndexOf(SyntaxKind.AsyncKeyword) == -1)
                        {
                            context.ReportDiagnostic(Diagnostic.Create(
                                SupportedDiagnostic,
                                localFunction.Identifier.GetLocation(),
                                new[] { "local function" }));
                        }
                        break;
                    case MethodDeclarationSyntax methodDeclaration:
                        var methodDeclarationReturnType = (INamedTypeSymbol)context.SemanticModel.GetSymbolInfo(methodDeclaration.ReturnType).Symbol;
                        if (!analyzerContext.TaskType.IsAssignableFrom(methodDeclarationReturnType) ||
                            methodDeclaration.Modifiers.IndexOf(SyntaxKind.AsyncKeyword) == -1)
                        {
                            context.ReportDiagnostic(Diagnostic.Create(
                                SupportedDiagnostic,
                                methodDeclaration.Identifier.GetLocation(),
                                new[] { "method" }));
                        }
                        break;
                }

            }, SyntaxKind.InvocationExpression);
        }

        private bool IsTagHelperRunnerRunAsync(InvocationExpressionSyntax parentExpression, IMethodSymbol method)
        {
            if (method.IsGenericMethod)
            {
                return false;
            }

            if (!string.Equals(SymbolNames.RunAsyncMethodName, method.Name, StringComparison.Ordinal))
            {
                return false;
            }

            if (!parentExpression.Expression.IsKind(SyntaxKind.SimpleMemberAccessExpression))
            {
                return false;
            }

            var memberAccessExpressionSyntax = (MemberAccessExpressionSyntax)parentExpression.Expression;
            if (!memberAccessExpressionSyntax.Expression.IsKind(SyntaxKind.IdentifierName))
            {
                return false;
            }

            var identifier = (IdentifierNameSyntax)memberAccessExpressionSyntax.Expression;
            if (!string.Equals(SymbolNames.TagHelperRunnerFieldName, identifier.Identifier.ValueText, StringComparison.Ordinal))
            {
                return false;
            }

            return true;
        }
    }
}
