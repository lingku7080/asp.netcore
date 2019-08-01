// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.AspNetCore.Components.Server.Circuits
{
    internal class CircuitClientConnection : IClientProxy
    {
        public CircuitClientConnection()
        {
            Connected = false;
        }

        public CircuitClientConnection(IClientProxy clientProxy, string connectionId)
        {
            Transfer(clientProxy, connectionId);
        }

        public bool Connected { get; private set; }

        public string ConnectionId { get; private set; }

        public IClientProxy Client { get; private set; }

        public void Transfer(IClientProxy clientProxy, string connectionId)
        {
            Client = clientProxy ?? throw new ArgumentNullException(nameof(clientProxy));
            ConnectionId = connectionId ?? throw new ArgumentNullException(nameof(connectionId));
            Connected = true;
        }

        public void SetDisconnected()
        {
            Connected = false;
            Client = null;
            ConnectionId = null;
        }

        public Task SendCoreAsync(string method, object[] args, CancellationToken cancellationToken = default)
        {
            if (Client == null)
            {
                throw new InvalidOperationException($"{nameof(SendCoreAsync)} cannot be invoked with an offline client.");
            }

            return Client.SendCoreAsync(method, args, cancellationToken);
        }
    }
}
