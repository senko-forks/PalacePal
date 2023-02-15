using Dalamud.Logging;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using System;
using Dalamud.Game.Gui;
using Pal.Client.Configuration;

namespace Pal.Client.Net
{
    internal sealed partial class RemoteApi : IDisposable
    {
#if DEBUG
        public const string RemoteUrl = "http://localhost:5145";
#else
        public const string RemoteUrl = "https://pal.liza.sh";
#endif
        private readonly string _userAgent =
            $"{typeof(RemoteApi).Assembly.GetName().Name?.Replace(" ", "")}/{typeof(RemoteApi).Assembly.GetName().Version?.ToString(2)}";

        private readonly ILoggerFactory _grpcToPluginLogLoggerFactory = LoggerFactory.Create(builder =>
            builder.AddProvider(new GrpcLoggerProvider()).AddFilter("Grpc", LogLevel.Trace));

        private readonly ChatGui _chatGui;
        private readonly ConfigurationManager _configurationManager;
        private readonly IPalacePalConfiguration _configuration;

        private GrpcChannel? _channel;
        private LoginInfo _loginInfo = new(null);
        private bool _warnedAboutUpgrade;

        public RemoteApi(ChatGui chatGui, ConfigurationManager configurationManager,
            IPalacePalConfiguration configuration)
        {
            _chatGui = chatGui;
            _configurationManager = configurationManager;
            _configuration = configuration;
        }

        public void Dispose()
        {
            PluginLog.Debug("Disposing gRPC channel");
            _channel?.Dispose();
            _channel = null;
        }
    }
}
