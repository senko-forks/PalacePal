using Dalamud.Logging;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using Pal.Client.Extensions;
using Pal.Client.Configuration;

namespace Pal.Client.Net
{
    internal partial class RemoteApi : IDisposable
    {
#if DEBUG
        public const string RemoteUrl = "http://localhost:5145";
#else
        public const string RemoteUrl = "https://pal.μ.tv";
#endif
        private readonly string _userAgent = $"{typeof(RemoteApi).Assembly.GetName().Name?.Replace(" ", "")}/{typeof(RemoteApi).Assembly.GetName().Version?.ToString(2)}";

        private readonly ILoggerFactory _grpcToPluginLogLoggerFactory = LoggerFactory.Create(builder => builder.AddProvider(new GrpcLoggerProvider()).AddFilter("Grpc", LogLevel.Trace));

        private GrpcChannel? _channel;
        private LoginInfo _loginInfo = new(null);
        private bool _warnedAboutUpgrade;

        public void Dispose()
        {
            PluginLog.Debug("Disposing gRPC channel");
            _channel?.Dispose();
            _channel = null;
        }
    }
}
