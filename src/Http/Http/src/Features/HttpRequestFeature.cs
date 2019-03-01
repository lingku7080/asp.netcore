// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.IO.Pipelines;

namespace Microsoft.AspNetCore.Http.Features
{
    public class HttpRequestFeature : IHttpRequestFeature, IRequestBodyPipeFeature
    {
        private Stream _internalStream;
        private PipeReader _internalPipeReader;
        private HttpContext _context;

        public HttpRequestFeature() : this(null)
        {
        }

        public HttpRequestFeature(HttpContext context)
        {
            _context = context;
            Headers = new HeaderDictionary();
            _internalStream = Stream.Null;
            _internalPipeReader = new NullPipeReader();
            Protocol = string.Empty;
            Scheme = string.Empty;
            Method = string.Empty;
            PathBase = string.Empty;
            Path = string.Empty;
            QueryString = string.Empty;
            RawTarget = string.Empty;
        }

        public string Protocol { get; set; }
        public string Scheme { get; set; }
        public string Method { get; set; }
        public string PathBase { get; set; }
        public string Path { get; set; }
        public string QueryString { get; set; }
        public string RawTarget { get; set; }

        public IHeaderDictionary Headers { get; set; }

        public Stream Body
        {
            get
            {
                return _internalStream;
            }
            set
            {
                _internalStream = value;
                var streamPipeReader = new StreamPipeReader(_internalStream);
                _internalPipeReader = streamPipeReader;
                if (_context != null)
                {
                    _context.Response.RegisterForDispose(streamPipeReader);
                }
            }
        }

        public PipeReader BodyPipe
        {
            get
            {
                return _internalPipeReader;
            }
            set
            {
                _internalPipeReader = value;
                _internalStream = new ReadOnlyPipeStream(_internalPipeReader);
            }
        }
    }
}
