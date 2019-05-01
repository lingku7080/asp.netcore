// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR.Client.Internal;
using Microsoft.AspNetCore.SignalR.Tests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using Xunit;

namespace Microsoft.AspNetCore.SignalR.Client.Tests
{
    public partial class HubConnectionTests
    {
        [Fact]
        public async Task ReconnectIsNotEnabledByDefault()
        {
            bool ExpectedErrors(WriteContext writeContext)
            {
                return writeContext.LoggerName == typeof(HubConnection).FullName &&
                       (writeContext.EventId.Name == "ShutdownWithError" ||
                        writeContext.EventId.Name == "ServerDisconnectedWithError");
            }

            using (StartVerifiableLog(ExpectedErrors))
            {
                var exception = new Exception();

                var testConnection = new TestConnection();
                await using var hubConnection = CreateHubConnection(testConnection, loggerFactory: LoggerFactory);

                var reconnectingCalled = false;
                var closedErrorTcs = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);

                hubConnection.Reconnecting += error =>
                {
                    reconnectingCalled = true;
                    return Task.CompletedTask;
                };

                hubConnection.Closed += error =>
                {
                    closedErrorTcs.SetResult(error);
                    return Task.CompletedTask;
                };

                await hubConnection.StartAsync().OrTimeout();

                testConnection.CompleteFromTransport(exception);

                Assert.Same(exception, await closedErrorTcs.Task.OrTimeout());
                Assert.False(reconnectingCalled);
            }
        }

        [Fact]
        public async Task ReconnectCanBeOptedInto()
        {
            bool ExpectedErrors(WriteContext writeContext)
            {
                return writeContext.LoggerName == typeof(HubConnection).FullName &&
                       (writeContext.EventId.Name == "foo" ||
                        writeContext.EventId.Name == "bar");
            }

            var failReconnectTcs = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);

            using (StartVerifiableLog(ExpectedErrors))
            {
                var startCallCount = 0;

                Task OnTestConnectionStart()
                {
                    startCallCount++;

                    // Only fail the first reconnect attempt.
                    if (startCallCount == 2)
                    {
                        return failReconnectTcs.Task;
                    }

                    return Task.CompletedTask;
                }

                var testConnection = new TestConnection(OnTestConnectionStart);

                var retryContexts = new List<RetryContext>();
                var mockReconnectPolicy = new Mock<IRetryPolicy>();
                mockReconnectPolicy.Setup(p => p.NextRetryDelay(It.IsAny<RetryContext>())).Returns<RetryContext>(context =>
                {
                    retryContexts.Add(context);
                    return TimeSpan.Zero;
                });

                await using var hubConnection = CreateHubConnection(testConnection, loggerFactory: LoggerFactory, reconnectPolicy: mockReconnectPolicy.Object);

                var closedCalled = false;
                var reconnectingCount = 0;
                var reconnectedCount = 0;
                var reconnectingErrorTcs = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
                var reconnectedConnectionIdTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

                hubConnection.Reconnecting += error =>
                {
                    reconnectingCount++;
                    reconnectingErrorTcs.SetResult(error);
                    return Task.CompletedTask;
                };

                hubConnection.Reconnected += connectionId =>
                {
                    reconnectedCount++;
                    reconnectedConnectionIdTcs.SetResult(connectionId);
                    return Task.CompletedTask;
                };

                hubConnection.Closed += error =>
                {
                    closedCalled = true;
                    return Task.CompletedTask;
                };

                await hubConnection.StartAsync().OrTimeout();

                var firstException = new Exception();
                testConnection.CompleteFromTransport(firstException);

                Assert.Same(firstException, await reconnectingErrorTcs.Task.OrTimeout());
                Assert.Single(retryContexts);
                Assert.Same(firstException, retryContexts[0].RetryReason);
                Assert.Equal(0, retryContexts[0].PreviousRetryCount);
                Assert.Equal(TimeSpan.Zero, retryContexts[0].ElapsedTime);

                var reconnectException = new Exception();
                failReconnectTcs.TrySetResult(reconnectException);

                Assert.Equal("", await reconnectedConnectionIdTcs.Task.OrTimeout());

                Assert.Equal(2, retryContexts.Count);
                Assert.Same(reconnectException, retryContexts[1].RetryReason);
                Assert.Equal(1, retryContexts[1].PreviousRetryCount);
                Assert.True(TimeSpan.Zero <= retryContexts[1].ElapsedTime);

                Assert.False(closedCalled);
            }
        }

        private static HubConnection CreateReconnectingHubConnection(Func<TestConnection> testConnectionFactory, ILoggerFactory loggerFactory = null, IRetryPolicy reconnectPolicy = null)
        {
            var builder = new HubConnectionBuilder();

            var delegateConnectionFactory = new ReconnectingConnectionFactory(testConnectionFactory);

            builder.Services.AddSingleton<IConnectionFactory>(delegateConnectionFactory);

            if (loggerFactory != null)
            {
                builder.WithLoggerFactory(loggerFactory);
            }

            if (reconnectPolicy != null)
            {
                builder.WithAutomaticReconnect(reconnectPolicy);
            }

            return builder.Build();
        }

        private class ReconnectingConnectionFactory : IConnectionFactory
        {
            public readonly Func<TestConnection> _testConnectionFactory;
            public TestConnection CurrentTestConnection { get; private set; }


            public ReconnectingConnectionFactory(Func<TestConnection> testConnectionFactory)
            {
                _testConnectionFactory = testConnectionFactory;
            }

            public Task<ConnectionContext> ConnectAsync(TransferFormat transferFormat, CancellationToken cancellationToken = default)
            {
                CurrentTestConnection = _testConnectionFactory();
                return CurrentTestConnection.StartAsync(transferFormat);
            }

            public Task DisposeAsync(ConnectionContext connection)
            {
                var disposingTestConnection = CurrentTestConnection;
                CurrentTestConnection = null;

                return disposingTestConnection.DisposeAsync();
            }
        }
    }
}
