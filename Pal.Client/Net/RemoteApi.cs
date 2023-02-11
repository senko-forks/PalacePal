using Dalamud.Logging;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using System;
using Pal.Client.Extensions;

namespace Pal.Client.Net
{
    internal partial class RemoteApi : IDisposable
    {
#if DEBUG
        public static string RemoteUrl { get; } = "http://localhost:5145";
#else
        public static string RemoteUrl { get; } = "https://pal.μ.tv";
#endif
        private readonly string _userAgent = $"{typeof(RemoteApi).Assembly.GetName().Name?.Replace(" ", "")}/{typeof(RemoteApi).Assembly.GetName().Version?.ToString(2)}";

        private readonly ILoggerFactory _grpcToPluginLogLoggerFactory = LoggerFactory.Create(builder => builder.AddProvider(new GrpcLoggerProvider()).AddFilter("Grpc", LogLevel.Trace));

        private GrpcChannel? _channel;
        private LoginInfo _loginInfo = new(null);
        private bool _warnedAboutUpgrade;

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

        public string? PartialAccountId => Account?.Id?.ToPartialId();

        private string FormattedPartialAccountId => PartialAccountId ?? "[no account id]";

        public void Dispose()
        {
            PluginLog.Debug("Disposing gRPC channel");
            _channel?.Dispose();
            _channel = null;
        }
    }
}
