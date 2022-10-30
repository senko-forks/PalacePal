using Account;
using Grpc.Core;
using Grpc.Net.Client;
using Palace;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace Pal.Client
{
    internal class RemoteApi : IDisposable
    {
#if DEBUG
        private const string remoteUrl = "http://localhost:5145";
#else
        private const string remoteUrl = "https://pal.μ.tv";
#endif
        private GrpcChannel? _channel;
        private LoginReply? _lastLoginReply;

        private async Task<bool> Connect(CancellationToken cancellationToken, bool retry = true)
        {
            if (Service.Configuration.Mode != Configuration.EMode.Online)
                return false;

            if (_channel == null || !(_channel.State == ConnectivityState.Ready || _channel.State == ConnectivityState.Idle))
            {
                Dispose();

                _channel = GrpcChannel.ForAddress(remoteUrl, new GrpcChannelOptions
                {
                    HttpHandler = new SocketsHttpHandler
                    {
                        ConnectTimeout = TimeSpan.FromSeconds(5),
                    }
                });
                await _channel.ConnectAsync(cancellationToken);
            }

            var accountClient = new AccountService.AccountServiceClient(_channel);
#if DEBUG
            string? accountId = Service.Configuration.DebugAccountId;
#else
            string? accountId = Service.Configuration.AccountId;
#endif
            if (string.IsNullOrEmpty(accountId))
            {
                var createAccountReply = await accountClient.CreateAccountAsync(new CreateAccountRequest(), deadline: DateTime.UtcNow.AddSeconds(10), cancellationToken: cancellationToken);
                if (createAccountReply.Success)
                {
                    accountId = createAccountReply.AccountId;
#if DEBUG
                    Service.Configuration.DebugAccountId = accountId;
#else
                    Service.Configuration.AccountId = accountId;

#endif
                    Service.Configuration.Save();
                }
            }

            if (string.IsNullOrEmpty(accountId))
                return false;

            if (_lastLoginReply == null || string.IsNullOrEmpty(_lastLoginReply.AuthToken) || _lastLoginReply.ExpiresAt.ToDateTime().ToLocalTime() < DateTime.Now)
            {
                _lastLoginReply = await accountClient.LoginAsync(new LoginRequest { AccountId = accountId }, deadline: DateTime.UtcNow.AddSeconds(10), cancellationToken: cancellationToken);
                if (!_lastLoginReply.Success)
                {
                    if (_lastLoginReply.Error == LoginError.InvalidAccountId)
                    {
                        accountId = null;
#if DEBUG
                        Service.Configuration.DebugAccountId = accountId;
#else
                        Service.Configuration.AccountId = accountId;
#endif
                        Service.Configuration.Save();
                        if (retry)
                            return await Connect(cancellationToken, retry: false);
                        else
                            return false;
                    }
                }
            }

            return !string.IsNullOrEmpty(_lastLoginReply?.AuthToken);
        }

        public async Task<string> VerifyConnection(CancellationToken cancellationToken = default)
        {
            if (!await Connect(cancellationToken))
                return "Could not connect to server";

            var accountClient = new AccountService.AccountServiceClient(_channel);
            await accountClient.VerifyAsync(new VerifyRequest(), headers: AuthorizedHeaders(), deadline: DateTime.UtcNow.AddSeconds(10), cancellationToken: cancellationToken);
            return "Connection successful";
        }

        public async Task<(bool, List<Marker>)> DownloadRemoteMarkers(ushort territoryId, CancellationToken cancellationToken = default)
        {
            if (!await Connect(cancellationToken))
                return (false, new());

            var palaceClient = new PalaceService.PalaceServiceClient(_channel);
            var downloadReply = await palaceClient.DownloadFloorsAsync(new DownloadFloorsRequest { TerritoryType = territoryId }, headers: AuthorizedHeaders(), cancellationToken: cancellationToken);
            return (downloadReply.Success, downloadReply.Objects.Select(o => new Marker((Marker.EType)o.Type, new Vector3(o.X, o.Y, o.Z)) { RemoteSeen = true }).ToList());
        }

        public async Task<bool> UploadMarker(ushort territoryType, IList<Marker> markers, CancellationToken cancellationToken = default)
        {
            if (markers.Count == 0)
                return true;

            if (!await Connect(cancellationToken))
                return false;

            var palaceClient = new PalaceService.PalaceServiceClient(_channel);
            var uploadRequest = new UploadFloorsRequest
            {
                TerritoryType = territoryType,
            };
            uploadRequest.Objects.AddRange(markers.Select(m => new PalaceObject
            {
                Type = (ObjectType)m.Type,
                X = m.Position.X,
                Y = m.Position.Y,
                Z = m.Position.Z
            }));
            var uploadReply = await palaceClient.UploadFloorsAsync(uploadRequest, headers: AuthorizedHeaders(), cancellationToken: cancellationToken);
            return uploadReply.Success;
        }

        public async Task<(bool, List<FloorStatistics>)> FetchStatistics(CancellationToken cancellationToken = default)
        {
            if (!await Connect(cancellationToken))
                return new(false, new List<FloorStatistics>());

            var palaceClient = new PalaceService.PalaceServiceClient(_channel);
            var statisticsReply = await palaceClient.FetchStatisticsAsync(new StatisticsRequest(), headers: AuthorizedHeaders(), deadline: DateTime.UtcNow.AddSeconds(30), cancellationToken: cancellationToken);
            return (statisticsReply.Success, statisticsReply.FloorStatistics.ToList());
        }

        private Metadata AuthorizedHeaders() => new Metadata
        {
            { "Authorization", $"Bearer {_lastLoginReply?.AuthToken}" },
        };

        public void Dispose()
        {
            _channel?.Dispose();
            _channel = null;
        }
    }
}
