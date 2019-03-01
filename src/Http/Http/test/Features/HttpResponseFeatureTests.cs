// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.IO.Pipelines;
using Microsoft.AspNetCore.Http.Features;
using Xunit;

namespace Microsoft.AspNetCore.Http.Tests.Features
{
    public class HttpRequestFeatureTests
    {
        [Fact]
        public void BodyPipe_GetsWrapped()
        {
            var responseFeature = new HttpResponseFeature(new DefaultHttpContext());
            responseFeature.Body = new MemoryStream();
            var innerStream = (responseFeature.BodyPipe as StreamPipeWriter).InnerStream;
            Assert.Equal(responseFeature.Body, innerStream);
        }

        [Fact]
        public void Body_GetsWrapped()
        {
            var responseFeature = new HttpResponseFeature(new DefaultHttpContext());
            responseFeature.BodyPipe = new NullPipeWriter();
            var innerPipeWriter = (responseFeature.Body as WriteOnlyPipeStream).InnerPipeWriter;
            Assert.Equal(responseFeature.BodyPipe, innerPipeWriter);
        }
    }
}
