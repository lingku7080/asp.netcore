// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace http2cat
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");

            var host = new HostBuilder()
                .ConfigureLogging(loggingBuilder =>
                {
                    loggingBuilder.AddConsole();
                })
                .ConfigureServices(services =>
                {
                    services.AddSingleton<IConnectionFactory, SocketConnectionFactory>();
                    services.AddHostedService<Http2CatHostedService>();
                })
                .Build();

            host.Run();
        }

        private class Http2CatHostedService : IHostedService
        {
            private readonly IConnectionFactory _connectionFactory;
            private readonly ILogger<Http2CatHostedService> _logger;

            public Http2CatHostedService(IConnectionFactory connectionFactory, ILogger<Http2CatHostedService> logger)
            {
                _connectionFactory = connectionFactory;
                _logger = logger;
            }

            public async Task StartAsync(CancellationToken cancellationToken)
            {
                var endpoint = new IPEndPoint(IPAddress.Loopback, 5005);

                _logger.LogInformation($"Connecting to '{endpoint}'.");

                var connectionContext = await _connectionFactory.ConnectAsync(endpoint);

                _logger.LogInformation($"Connected to '{endpoint}'.");

                var http2Utilites = new Http2Utilities();
                await http2Utilites.InitializeConnectionAsync(connectionContext);

                _logger.LogInformation($"Initialized http2 connection.");
                // ..
            }

            public Task StopAsync(CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }
        }
    }
}
