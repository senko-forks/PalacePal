extern alias PalServer;
using System;
using Account;
using Grpc.Net.Client;
using Program = PalServer::Pal.Server.Program;

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

        public PalWebApplicationFactory<Program> Factory { get; }
        public GrpcChannel Channel { get; }
        public AccountService.AccountServiceClient AccountsClient { get; }

        public void Dispose()
        {
            Factory.Dispose();
        }
    }
}
