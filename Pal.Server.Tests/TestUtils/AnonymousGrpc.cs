using System;
using Account;
using Grpc.Net.Client;

namespace Pal.Server.Tests.TestUtils
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public sealed class AnonymousGrpc : IDisposable
    {
        public AnonymousGrpc()
        {
            Factory = new PalWebApplicationFactory<Program>();
            var options = new GrpcChannelOptions { HttpHandler = Factory.Server.CreateHandler() };
            Channel = GrpcChannel.ForAddress(Factory.Server.BaseAddress, options);

            AccountsClient = new AccountService.AccountServiceClient(Channel);
        }

        public void Dispose()
        {
            Factory.Dispose();
        }

        public PalWebApplicationFactory<Program> Factory { get; }
        public GrpcChannel Channel { get; }
        public AccountService.AccountServiceClient AccountsClient { get; }
    }
}
