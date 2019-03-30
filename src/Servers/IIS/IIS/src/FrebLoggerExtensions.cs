using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Server.IIS
{
    public static class FrebLoggerExtensions
    {
        public static IDisposable RequestScope(this ILogger logger, IntPtr pIntPtr)
        {
            return logger.BeginScope(new FrebLoggingScope(pIntPtr));
        }
    }
}
