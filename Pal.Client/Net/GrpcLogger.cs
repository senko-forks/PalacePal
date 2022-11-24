using Dalamud.Logging;
using Microsoft.Extensions.Logging;
using System;
using System.Runtime.CompilerServices;

namespace Pal.Client.Net
{
    internal class GrpcLogger : ILogger
    {
        private readonly string _name;

        public GrpcLogger(string name)
        {
            _name = name;
        }

        public IDisposable BeginScope<TState>(TState state) => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        [MethodImpl(MethodImplOptions.NoInlining)] // PluginLog detects the plugin name as `Microsoft.Extensions.Logging` if inlined
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            if (formatter == null)
                throw new ArgumentNullException(nameof(formatter));

            string message = $"gRPC[{_name}] {formatter(state, null)}";
            if (string.IsNullOrEmpty(message))
                return;

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

        private class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new NullScope();

            private NullScope()
            {
            }

            public void Dispose()
            {
            }
        }
    }
}
