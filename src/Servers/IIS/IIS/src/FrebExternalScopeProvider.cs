using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Server.IIS
{
    public class FrebExternalScopeProvider : IExternalScopeProvider
    {
        public void ForEachScope<TState>(Action<object, TState> callback, TState state)
        {
        }

        public IDisposable Push(object state)
        {
        }
    }
}
