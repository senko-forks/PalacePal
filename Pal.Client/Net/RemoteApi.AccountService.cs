using Account;
using Dalamud.Logging;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Pal.Client.Net
{
    internal partial class RemoteApi
    {
        private async Task<(bool Success, string Error)> TryConnect(CancellationToken cancellationToken, ILoggerFactory? loggerFactory = null, bool retry = true)
        {
            if (Service.Configuration.Mode != Configuration.EMode.Online)
            {
                PluginLog.Debug("TryConnect: Not Online, not attempting to establish a connection");
                return (false, "You are not online.");
            }

            if (_channel == null || !(_channel.State == ConnectivityState.Ready || _channel.State == ConnectivityState.Idle))
            {
                Dispose();

                PluginLog.Information("TryConnect: Creating new gRPC channel");
                _channel = GrpcChannel.ForAddress(RemoteUrl, new GrpcChannelOptions
                {
                    HttpHandler = new SocketsHttpHandler
                    {
                        ConnectTimeout = TimeSpan.FromSeconds(5),
                        SslOptions = GetSslClientAuthenticationOptions(),
                    },
                    LoggerFactory = loggerFactory,
                });

                PluginLog.Information($"TryConnect: Connecting to upstream service at {RemoteUrl}");
                await _channel.ConnectAsync(cancellationToken);
            }

            var accountClient = new AccountService.AccountServiceClient(_channel);
            if (AccountId == null)
            {
                PluginLog.Information($"TryConnect: No account information saved for {RemoteUrl}, creating new account");
                var createAccountReply = await accountClient.CreateAccountAsync(new CreateAccountRequest(), headers: UnauthorizedHeaders(), deadline: DateTime.UtcNow.AddSeconds(10), cancellationToken: cancellationToken);
                if (createAccountReply.Success)
                {
                    Account = new Configuration.AccountInfo
                    {
                        Id = Guid.Parse(createAccountReply.AccountId),
                    };
                    PluginLog.Information($"TryConnect: Account created with id {FormattedPartialAccountId}");

                    Service.Configuration.Save();
                }
                else
                {
                    PluginLog.Error($"TryConnect: Account creation failed with error {createAccountReply.Error}");
                    if (createAccountReply.Error == CreateAccountError.UpgradeRequired && !_warnedAboutUpgrade)
                    {
                        Service.Chat.PrintError("[Palace Pal] Your version of Palace Pal is outdated, please update the plugin using the Plugin Installer.");
                        _warnedAboutUpgrade = true;
                    }
                    return (false, $"Could not create account ({createAccountReply.Error}).");
                }
            }

            if (AccountId == null)
            {
                PluginLog.Warning("TryConnect: No account id to login with");
                return (false, "No account-id after account was attempted to be created.");
            }

            if (!_loginInfo.IsValid)
            {
                PluginLog.Information($"TryConnect: Logging in with account id {FormattedPartialAccountId}");
                LoginReply loginReply = await accountClient.LoginAsync(new LoginRequest { AccountId = AccountId?.ToString() }, headers: UnauthorizedHeaders(), deadline: DateTime.UtcNow.AddSeconds(10), cancellationToken: cancellationToken);
                if (loginReply.Success)
                {
                    PluginLog.Information($"TryConnect: Login successful with account id: {FormattedPartialAccountId}");
                    _loginInfo = new LoginInfo(loginReply.AuthToken);

                    var account = Account;
                    if (account != null)
                    {
                        account.CachedRoles = _loginInfo.Claims?.Roles?.ToList() ?? new List<string>();
                        Service.Configuration.Save();
                    }
                }
                else
                {
                    PluginLog.Error($"TryConnect: Login failed with error {loginReply.Error}");
                    _loginInfo = new LoginInfo(null);
                    if (loginReply.Error == LoginError.InvalidAccountId)
                    {
                        Account = null;
                        Service.Configuration.Save();
                        if (retry)
                        {
                            PluginLog.Information("TryConnect: Attempting connection retry without account id");
                            return await TryConnect(cancellationToken, retry: false);
                        }
                        else
                            return (false, "Invalid account id.");
                    }
                    if (loginReply.Error == LoginError.UpgradeRequired && !_warnedAboutUpgrade)
                    {
                        Service.Chat.PrintError("[Palace Pal] Your version of Palace Pal is outdated, please update the plugin using the Plugin Installer.");
                        _warnedAboutUpgrade = true;
                    }
                    return (false, $"Could not log in ({loginReply.Error}).");
                }
            }

            if (!_loginInfo.IsValid)
            {
                PluginLog.Error($"TryConnect: Login state is loggedIn={_loginInfo.IsLoggedIn}, expired={_loginInfo.IsExpired}");
                return (false, "No login information available.");
            }

            return (true, string.Empty);
        }

        private async Task<bool> Connect(CancellationToken cancellationToken)
        {
            var result = await TryConnect(cancellationToken);
            return result.Success;
        }

        public async Task<string> VerifyConnection(CancellationToken cancellationToken = default)
        {
            _warnedAboutUpgrade = false;

            var connectionResult = await TryConnect(cancellationToken, loggerFactory: _grpcToPluginLogLoggerFactory);
            if (!connectionResult.Success)
                return $"Could not connect to server: {connectionResult.Error}";

            PluginLog.Information("VerifyConnection: Connection established, trying to verify auth token");
            var accountClient = new AccountService.AccountServiceClient(_channel);
            await accountClient.VerifyAsync(new VerifyRequest(), headers: AuthorizedHeaders(), deadline: DateTime.UtcNow.AddSeconds(10), cancellationToken: cancellationToken);

            PluginLog.Information("VerifyConnection: Verification returned no errors.");
            return "Connection successful.";
        }

        internal class LoginInfo
        {
            public LoginInfo(string? authToken)
            {
                if (!string.IsNullOrEmpty(authToken))
                {
                    IsLoggedIn = true;
                    AuthToken = authToken;
                    Claims = JwtClaims.FromAuthToken(authToken!);
                }
                else
                    IsLoggedIn = false;
            }

            public bool IsLoggedIn { get; }
            public string? AuthToken { get; }
            public JwtClaims? Claims { get; }
            public DateTimeOffset ExpiresAt => Claims?.ExpiresAt.Subtract(TimeSpan.FromMinutes(5)) ?? DateTimeOffset.MinValue;
            public bool IsExpired => ExpiresAt < DateTimeOffset.UtcNow;

            public bool IsValid => IsLoggedIn && !IsExpired;
        }
    }
}
