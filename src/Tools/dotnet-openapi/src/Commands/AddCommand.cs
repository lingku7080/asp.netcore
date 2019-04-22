// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace Microsoft.DotNet.OpenApi.Commands
{
    internal class AddCommand : BaseCommand
    {
        private const string CommandName = "add";

        public AddCommand(Application parent)
            : base(parent, CommandName)
        {
            Commands.Add(new AddFileCommand(this));
            //TODO: Add AddprojectComand here: https://github.com/aspnet/AspNetCore/issues/12738
            Commands.Add(new AddURLCommand(this));
        }

        internal new Application Parent => (Application)base.Parent;

        protected override Task<int> ExecuteCoreAsync()
        {
            ShowHelp();
            return Task.FromResult(0);
        }

        protected override bool ValidateArguments()
        {
            return true;
        }
    }
}
