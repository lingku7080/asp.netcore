using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.Logging
{
    public class FrebLoggerFactoryExtensions
    {
        public static ILoggingBuilder AddFrebLog(this Microsoft.Extensions.Logging.ILoggingBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, FrebLoggerProvider>());

            return builder;
        }
    }
}
