using Dalamud.Logging;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace Pal.Client.DependencyInjection.Logging
{
    internal sealed class DalamudLogger : ILogger
    {
        private readonly string _name;
        private readonly IExternalScopeProvider? _scopeProvider;

        public DalamudLogger(string name, IExternalScopeProvider? scopeProvider)
        {
            _name = name;
            _scopeProvider = scopeProvider;
        }

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
            => _scopeProvider?.Push(state) ?? NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        // PluginLog detects the plugin name as `Microsoft.Extensions.Logging` if inlined
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            if (formatter == null)
                throw new ArgumentNullException(nameof(formatter));

            StringBuilder sb = new StringBuilder();
            _scopeProvider?.ForEachScope((scope, builder) =>
                {
                    if (scope is IEnumerable<KeyValuePair<string, object>> properties)
                    {
                        foreach (KeyValuePair<string, object> pair in properties)
                        {
                            builder.Append('<').Append(pair.Key).Append('=').Append(pair.Value)
                                .Append("> ");
                        }
                    }
                    else if (scope != null)
                        builder.Append('<').Append(scope).Append("> ");
                },
                sb);
            sb.Append('[').Append(_name).Append("] ").Append(formatter(state, null));
            string message = sb.ToString();

#pragma warning disable CS8604 // the nullability on PluginLog methods is wrong and allows nulls for exceptions, WriteLog even declares the parameter as `Exception? exception = null`
            switch (logLevel)
            {
                case LogLevel.Critical:
                    PluginLog.Fatal(exception, message);
                    break;

                case LogLevel.Error:
                    PluginLog.Error(exception, message);
                    break;

                case LogLevel.Warning:
                    PluginLog.Warning(exception, message);
                    break;

                case LogLevel.Information:
                    PluginLog.Information(exception, message);
                    break;

                case LogLevel.Debug:
                    PluginLog.Debug(exception, message);
                    break;

                case LogLevel.Trace:
                    PluginLog.Verbose(exception, message);
                    break;
            }
#pragma warning restore CS8604
        }

        private sealed class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new();

            private NullScope()
            {
            }

            public void Dispose()
            {
            }
        }
    }
}
