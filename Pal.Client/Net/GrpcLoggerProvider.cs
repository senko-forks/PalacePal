using Microsoft.Extensions.Logging;
using System;

namespace Pal.Client.Net
{
    internal class GrpcLoggerProvider : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new GrpcLogger(categoryName);

        public void Dispose()
        {
        }
    }
}
