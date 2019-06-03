// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Server.IIS.FunctionalTests.Utilities;
using Microsoft.AspNetCore.Server.IIS.FunctionalTests;
using Microsoft.AspNetCore.Server.IntegrationTesting;
using Microsoft.AspNetCore.Testing;
using Microsoft.AspNetCore.Testing.xunit;
using Microsoft.Extensions.Logging;
using Xunit;
using Microsoft.AspNetCore.Server.IntegrationTesting.IIS;
using System.Collections.Generic;

namespace Microsoft.AspNetCore.Server.IIS.FunctionalTests
{
    [Collection(PublishedSitesCollection.Name)]
    public class ShadowCopyTests : IISFunctionalTestBase
    {
        public ShadowCopyTests(PublishedSitesFixture fixture) : base(fixture)
        {
        }

        [ConditionalFact]
        public async Task ShadowCopyWorks()
        {
            var directory = CreateTempDirectory();
            var deploymentParameters = Fixture.GetBaseDeploymentParameters();
            deploymentParameters.HandlerSettings["enableShadowCopying"] = "true";
            deploymentParameters.HandlerSettings["shadowCopyDirectory"] = directory.FullName;
            var deploymentResult = await DeployAsync(deploymentParameters);
            await deploymentResult.HttpClient.GetStringAsync("Wow!");

            // Check if directory can be deleted.
            // Can't delete the folder but can delete all content in it.

            var directoryInfo = new DirectoryInfo(deploymentResult.ContentRoot);
            foreach (var fileInfo in directoryInfo.GetFiles())
            {
                fileInfo.Delete();
            }

            foreach (var dirInfo in directoryInfo.GetDirectories())
            {
                dirInfo.Delete();
            }
        }

        protected static DirectoryInfo CreateTempDirectory()
        {
            var tempPath = Path.GetTempPath() + Guid.NewGuid().ToString("N");
            var target = new DirectoryInfo(tempPath);
            target.Create();
            return target;
        }
    }
}
