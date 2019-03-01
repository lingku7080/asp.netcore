// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.IO.Pipelines;
using Microsoft.AspNetCore.Http.Features;
using Xunit;

namespace Microsoft.AspNetCore.Http.Tests.Features
{
    public class HttpResponseFeatureTests
    {
        [Fact]
        public void BodyPipe_GetsWrapped()
        {
            var requestFeature = new HttpRequestFeature(new DefaultHttpContext());
            requestFeature.Body = new MemoryStream();
            var innerStream = (requestFeature.BodyPipe as StreamPipeReader).InnerStream;
            Assert.Equal(requestFeature.Body, innerStream);
        }

        [Fact]
        public void Body_GetsWrapped()
        {
            var requestFeature = new HttpRequestFeature(new DefaultHttpContext());
            requestFeature.BodyPipe = new NullPipeReader();
            var innerPipeReader = (requestFeature.Body as ReadOnlyPipeStream).InnerPipeReader;
            Assert.Equal(requestFeature.BodyPipe, innerPipeReader);
        }
    }
}
