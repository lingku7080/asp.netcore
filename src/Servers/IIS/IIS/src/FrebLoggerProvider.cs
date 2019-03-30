using Microsoft.AspNetCore.Server.IIS;

namespace Microsoft.Extensions.Logging.Freb
{
    public class FrebLoggerProvider : ILoggerProvider, ISupportExternalScope
    {
        private IExternalScopeProvider _scopeProvider;

        public FrebLoggerProvider()
        {

        }

        public ILogger CreateLogger(string name)
        {
            return new FrebLogger(name: name);
        }

        public void Dispose()
        {
        }

        public void SetScopeProvider(IExternalScopeProvider scopeProvider)
        {
            _scopeProvider = scopeProvider;
        }
    }
}
