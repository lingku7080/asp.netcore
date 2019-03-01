// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.IO.Pipelines;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.Http.Features
{
    public class HttpResponseFeature : IHttpResponseFeature, IResponseBodyPipeFeature
    {
        private Stream _internalStream;
        private HttpContext _context;
        private PipeWriter _internalPipeWriter;

        public HttpResponseFeature() : this(null)
        {
        }
        public HttpResponseFeature(HttpContext context)
        {
            StatusCode = 200;
            Headers = new HeaderDictionary();
            _internalStream = Stream.Null;
            _internalPipeWriter = new NullPipeWriter();
            _context = context;
        }

        public int StatusCode { get; set; }

        public string ReasonPhrase { get; set; }

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
                var streamPipeWriter = new StreamPipeWriter(_internalStream);
                _internalPipeWriter = streamPipeWriter;

                if (_context != null)
                {
                    _context.Response.RegisterForDispose(streamPipeWriter);
                }
            }
        }

        public virtual bool HasStarted
        {
            get { return false; }
        }

        public PipeWriter BodyPipe
        {
            get
            {
                return _internalPipeWriter;
            }
            set
            {
                _internalPipeWriter = value;
                _internalStream = new WriteOnlyPipeStream(_internalPipeWriter);
            }
        }

        public virtual void OnStarting(Func<object, Task> callback, object state)
        {
        }

        public virtual void OnCompleted(Func<object, Task> callback, object state)
        {
        }
    }
}
