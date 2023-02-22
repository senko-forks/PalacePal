using Microsoft.Extensions.Logging;
using System;

namespace Pal.Client.DependencyInjection.Logging
{
    internal sealed class DalamudLoggerProvider : ILoggerProvider, ISupportExternalScope
    {
        private IExternalScopeProvider? _scopeProvider;

        public ILogger CreateLogger(string categoryName) => new DalamudLogger(categoryName, _scopeProvider);

        /// <summary>
        /// Manual logger creation, doesn't handle scopes.
        /// </summary>
        public ILogger CreateLogger(Type type) => CreateLogger(type.FullName ?? type.ToString());

        /// <summary>
        /// Manual logger creation, doesn't handle scopes.
        /// </summary>
        public ILogger CreateLogger<T>() => CreateLogger(typeof(T));

        public void SetScopeProvider(IExternalScopeProvider scopeProvider)
        {
            _scopeProvider = scopeProvider;
        }

        public void Dispose()
        {
        }
    }
}
