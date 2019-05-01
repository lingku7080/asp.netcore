// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Testing;
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
                       writeContext.EventId.Name == "FailedToSendInvocation";
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
    }
}
