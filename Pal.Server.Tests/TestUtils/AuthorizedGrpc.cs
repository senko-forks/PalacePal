﻿extern alias PalServer;
using System;
using System.Threading.Tasks;
using Account;
using FluentAssertions;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.DependencyInjection;
using PalServer::Pal.Server.Database;
using PalServer::Pal.Server.Services;
using AccountService = Account.AccountService;
using ExportService = Export.ExportService;
using PalaceService = Palace.PalaceService;
using Program = PalServer::Pal.Server.Program;

namespace Pal.Server.Tests.TestUtils
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public sealed class AuthorizedGrpc : IDisposable
    {
        public AuthorizedGrpc()
        {
            Factory = new PalWebApplicationFactory<Program>();
            var options = new GrpcChannelOptions { HttpHandler = Factory.Server.CreateHandler() };
            Channel = GrpcChannel.ForAddress(Factory.Server.BaseAddress, options);

            AccountsClient = new AccountService.AccountServiceClient(Channel);
            PalaceClient = new PalaceService.PalaceServiceClient(Channel);
            ExportClient = new ExportService.ExportServiceClient(Channel);
        }

        public PalWebApplicationFactory<Program> Factory { get; }
        public GrpcChannel Channel { get; }
        public AccountService.AccountServiceClient AccountsClient { get; }
        public PalaceService.PalaceServiceClient PalaceClient { get; }
        public ExportService.ExportServiceClient ExportClient { get; }

        public void Dispose()
        {
            Channel.Dispose();
            Factory.Dispose();
        }

        public async Task<Metadata> LoginAsync()
        {
            var loginReply = await AccountsClient.LoginAsync(new LoginRequest
            {
                AccountId = DbUtils.DefaultAccountId.ToString()
            });
            loginReply.Error.Should().Be(LoginError.None);
            loginReply.AuthToken.Should().NotBeEmpty();

            return new Metadata
            {
                { "Authorization", $"Bearer {loginReply.AuthToken}" }
            };
        }

        public void WithDb(Action<PalServerContext> func)
        {
            using var scope = Factory.Services.CreateScope();

            var dbContext = scope.ServiceProvider.GetRequiredService<PalServerContext>();
            func.Invoke(dbContext);
            dbContext.SaveChanges();

            scope.ServiceProvider.GetRequiredService<PalaceLocationCache>().Clear();
        }

        public T WithDb<T>(Func<PalServerContext, T> func)
        {
            using var scope = Factory.Services.CreateScope();

            var dbContext = scope.ServiceProvider.GetRequiredService<PalServerContext>();
            T result = func.Invoke(dbContext);
            dbContext.SaveChanges();

            scope.ServiceProvider.GetRequiredService<PalaceLocationCache>().Clear();

            return result;
        }
    }
}
