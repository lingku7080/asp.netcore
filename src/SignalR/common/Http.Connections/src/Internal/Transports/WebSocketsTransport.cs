// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http.Features;
using System.IO;

namespace Microsoft.AspNetCore.Http.Connections.Internal.Transports
{
    public partial class WebSocketsTransport : IHttpTransport
    {
        private readonly WebSocketOptions _options;
        private readonly ILogger _logger;
        private readonly IDuplexPipe _application;
        private readonly HttpConnectionContext _connection;
        //private volatile bool _aborted;
        private Stream _stream;

        public WebSocketsTransport(WebSocketOptions options, IDuplexPipe application, HttpConnectionContext connection, ILoggerFactory loggerFactory)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (application == null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            _options = options;
            _application = application;
            _connection = connection;
            _logger = loggerFactory.CreateLogger<WebSocketsTransport>();
        }

        public async Task Start(HttpContext context)
        {
            Debug.Assert(context.WebSockets.IsWebSocketRequest, "Not a websocket request");

            var subProtocol = _options.SubProtocolSelector?.Invoke(context.WebSockets.WebSocketRequestedProtocols);
            var upgradeFeature = context.Features.Get<IHttpUpgradeFeature>();
            _stream = await upgradeFeature.UpgradeAsync();
            Log.SocketOpened(_logger, subProtocol);

            var pipe = new Pipe();
            var transportPipeReader = new StreamPipeReader(_stream);
            var transportPipeWriter = new StreamPipeWriter(_stream);
            var transportToApplication = new DuplexPipe(transportPipeReader, pipe.Writer);
            var applicationToTransport = new DuplexPipe(pipe.Reader, transportPipeWriter);
            var pair = new DuplexPipe.DuplexPipePair(applicationToTransport, transportToApplication);
            _connection.Transport = pair.Application;
            _connection.Application = pair.Transport;
        }

        public Task ProcessRequestAsync(HttpContext context, CancellationToken token)
        {
            return Task.Delay(100000);
        }

        private static bool WebSocketCanSend(WebSocket ws)
        {
            return !(ws.State == WebSocketState.Aborted ||
                   ws.State == WebSocketState.Closed ||
                   ws.State == WebSocketState.CloseSent);
        }
    }
}
