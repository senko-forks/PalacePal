using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using Serilog.Events;

namespace Pal.Client.DependencyInjection.Logging
{
    internal sealed class DalamudLogger : ILogger
    {
        private static readonly string AssemblyName = typeof(Plugin).Assembly.GetName().Name!;
        private static readonly Serilog.ILogger PluginLogDelegate = Serilog.Log.ForContext("SourceContext", AssemblyName);
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

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None && PluginLogDelegate.IsEnabled(ToSerilogLevel(logLevel));

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            if (formatter == null)
                throw new ArgumentNullException(nameof(formatter));

            StringBuilder sb = new StringBuilder();
            sb.Append('[').Append(AssemblyName).Append("] ");
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
            sb.Append(_name).Append(": ").Append(formatter(state, null));
            PluginLogDelegate.Write(ToSerilogLevel(logLevel), exception, sb.ToString());
        }

        private LogEventLevel ToSerilogLevel(LogLevel logLevel)
        {
            return logLevel switch
            {
                LogLevel.Critical => LogEventLevel.Fatal,
                LogLevel.Error => LogEventLevel.Error,
                LogLevel.Warning => LogEventLevel.Warning,
                LogLevel.Information => LogEventLevel.Information,
                LogLevel.Debug => LogEventLevel.Debug,
                LogLevel.Trace => LogEventLevel.Verbose,
                _ => throw new ArgumentOutOfRangeException(nameof(logLevel), logLevel, null)
            };
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
