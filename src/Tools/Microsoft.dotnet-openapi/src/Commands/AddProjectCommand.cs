// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.OpenApi.Commands
{
    internal class AddProjectCommand :  BaseCommand
    {
        private const string CommandName = "project";

        private const string SourceProjectArgName = "source-project";

        public AddProjectCommand(BaseCommand parent)
            : base(parent, CommandName)
        {
            _sourceProjectArg = Argument(SourceProjectArgName, $"The OpenAPI project to add. This must be the path to project file(s) containing OpenAPI endpoints", multipleValues: true);
        }

        internal readonly CommandArgument _sourceProjectArg;

        protected override Task<int> ExecuteCoreAsync()
        {
            var projectFilePath = ResolveProjectFile(ProjectFileOption);

            foreach (var sourceFile in _sourceProjectArg.Values)
            {
                var codeGenerator = CodeGenerator.NSwagCSharp;
                EnsurePackagesInProject(projectFilePath, codeGenerator);

                AddServiceReference(OpenApiProjectReference, projectFilePath, sourceFile);
            }

            return Task.FromResult(0);
        }

        protected override bool ValidateArguments()
        {
            foreach(var sourceFile in _sourceProjectArg.Values)
            {
                if(!IsProjectFile(sourceFile))
                {
                    throw new ArgumentException($"{SourceProjectArgName} of '{sourceFile}' was not valid. Valid values must be project file(s)");
                }
            }

            Ensure.NotNullOrEmpty(_sourceProjectArg.Value, SourceProjectArgName);
            return true;
        }
    }
}
