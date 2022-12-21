using Dalamud.Logging;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using System;

namespace Pal.Client.Net
{
    internal partial class RemoteApi : IDisposable
    {
#if DEBUG
        public static string RemoteUrl { get; } = "http://localhost:5145";
#else
        public static string RemoteUrl { get; } = "https://pal.μ.tv";
#endif
        private readonly string UserAgent = $"{typeof(RemoteApi).Assembly.GetName().Name?.Replace(" ", "")}/{typeof(RemoteApi).Assembly.GetName().Version?.ToString(2)}";

        private readonly ILoggerFactory _grpcToPluginLogLoggerFactory = LoggerFactory.Create(builder => builder.AddProvider(new GrpcLoggerProvider()).AddFilter("Grpc", LogLevel.Trace));

        private GrpcChannel? _channel;
        private LoginInfo _loginInfo = new LoginInfo(null);
        private bool _warnedAboutUpgrade = false;

        public Configuration.AccountInfo? Account
        {
            get => Service.Configuration.Accounts.TryGetValue(RemoteUrl, out Configuration.AccountInfo? accountInfo) ? accountInfo : null;
            set
            {
                if (value != null)
                    Service.Configuration.Accounts[RemoteUrl] = value;
                else
                    Service.Configuration.Accounts.Remove(RemoteUrl);
            }
        }

        public Guid? AccountId => Account?.Id;

        private string PartialAccountId =>
            Account?.Id?.ToString()?.PadRight(14).Substring(0, 13) ?? "[no account id]";

        public void Dispose()
        {
            PluginLog.Debug("Disposing gRPC channel");
            _channel?.Dispose();
            _channel = null;
        }
    }
}
