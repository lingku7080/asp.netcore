using System;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Server.IIS
{
    /// <summary>
    /// A logger that writes logs to the current freb context
    /// </summary>
    public class FrebLogger : ILogger
    {
        private string _name;
        private AsyncLocal<FrebLoggingScope> ScopeProvider;

        public FrebLogger(string name) 
        {
            _name = name;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            ScopeProvider?.Value.Push(state) ?? NullScope.Instance;
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
            //            _logger = _applicationServices.GetRequiredService<ILogger<WebHost>>();


            if (formatter == null)
            {
                throw new ArgumentNullException(nameof(formatter));
            }

            var message = formatter(state, exception);

            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            // Todo how do we get the inprocess handler
            NativeMethods.HttpSetFrebLog(_externalScopeProvider.Value, message);
        }
        private class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new NullScope();

            private NullScope()
            {
            }

            /// <inheritdoc />
            public void Dispose()
            {
            }
        }
    }
}
