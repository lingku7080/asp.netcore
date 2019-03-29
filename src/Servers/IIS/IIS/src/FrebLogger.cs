using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Server.IIS
{
    /// <summary>
    /// A logger that writes logs to the current freb context
    /// </summary>
    public class FrebLogger : ILogger
    {
        private string _name;
        private IExternalScopeProvider _externalScopeProvider;

        public FrebLogger(string name, IExternalScopeProvider externalScopeProvider)
        {
            _name = name;
            _externalScopeProvider = externalScopeProvider;
        }


        public IDisposable BeginScope<TState>(TState state)
        {
            return _externalScopeProvider?.Push(state);
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            // TODO filter on request status here?
            // most likely we can just check if we have a warning or error
            return logLevel != LogLevel.None; 
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            if (formatter == null)
            {
                throw new ArgumentNullException(nameof(formatter));
            }

            var message = formatter(state, exception);

            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            // Todo how to log this?
            // do we need native methods? I think so

            NativeMethods.SendFrebLog();
        }
    }
}
