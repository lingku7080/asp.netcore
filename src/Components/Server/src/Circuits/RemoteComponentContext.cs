// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Microsoft.AspNetCore.Components.Server.Circuits
{
    internal class RemoteComponentContext : IComponentContext
    {
        private CircuitClientConnection connection;

        public bool IsConnected => connection != null && connection.Connected;

        internal void Initialize(CircuitClientConnection clientProxy)
        {
            connection = clientProxy ?? throw new ArgumentNullException(nameof(clientProxy));
        }
    }
}
