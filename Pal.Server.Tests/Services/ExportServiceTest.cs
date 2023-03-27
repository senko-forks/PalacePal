extern alias PalServer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Export;
using FluentAssertions;
using Grpc.Core;
using Pal.Common;
using Pal.Server.Tests.TestUtils;
using PalServer::Pal.Server.Database;
using Xunit;

namespace Pal.Server.Tests.Services
{
    public sealed class ExportServiceTest : IClassFixture<AuthorizedGrpc>
    {
        private readonly AuthorizedGrpc _grpc;

        public ExportServiceTest(AuthorizedGrpc grpc)
        {
            _grpc = grpc;
        }

        [Fact]
        public async Task ExportData()
        {
            _grpc.WithDb(dbContext =>
            {
                dbContext.ResetToDefaultAccount(new List<string> { "export:run" });
                MakeLocationsSeen(dbContext);
            });

            var auth = await _grpc.LoginAsync();

            string serverUrl = $"https://{Guid.NewGuid()}.local";
            DateTime beforeExport = DateTime.UtcNow;
            var exportReply = await _grpc.ExportClient.ExportAsync(new ExportRequest
            {
                ServerUrl = serverUrl
            }, headers: auth);
            exportReply.Error.Should().Be(ExportError.None);
            exportReply.Success.Should().BeTrue();

            var root = exportReply.Data;
            root.ServerUrl.Should().Be(serverUrl);
            root.CreatedAt.ToDateTime().Should().BeAfter(beforeExport);
            root.Floors.Should().HaveCount(1);

            var floor = root.Floors.Single(x => x.TerritoryType == (uint)ETerritoryType.Palace_1_10);
            floor.Objects.Where(x => x.Type == ExportObjectType.Trap).Should().HaveCount(3);
            floor.Objects.Where(x => x.Type == ExportObjectType.Hoard).Should().HaveCount(2);
        }

        [Fact]
        public async Task ExportWithoutRoleShouldFail()
        {
            _grpc.WithDb(dbContext => dbContext.ResetToDefaultAccount());
            var auth = await _grpc.LoginAsync();

            Action action = () => _grpc.ExportClient.Export(new ExportRequest
            {
                ServerUrl = "https://bla.bla"
            }, headers: auth);
            action.Should()
                .Throw<RpcException>()
                .Where(e => e.StatusCode == StatusCode.PermissionDenied);
        }

        [Fact]
        public void AnonymousExportShouldFail()
        {
            _grpc.WithDb(dbContext => dbContext.ResetToDefaultAccount());

            Action action = () => _grpc.ExportClient.Export(new ExportRequest
            {
                ServerUrl = "https://bla.bla"
            });
            action.Should()
                .Throw<RpcException>()
                .Where(e => e.StatusCode == StatusCode.Unauthenticated);
        }

        private void MakeLocationsSeen(PalServerContext dbContext)
        {
            var trapsToSee = dbContext.Locations
                .Where(x => x.Type == ServerLocation.EType.Trap)
                .Take(3);
            var hoardToSee = dbContext.Locations
                .Where(x => x.Type == ServerLocation.EType.Hoard)
                .Take(2);
            var locationsToSee = trapsToSee.Concat(hoardToSee).ToList();

            for (int i = 1; i <= 10; ++i)
            {
                var account = new PalServer::Pal.Server.Database.Account
                {
                    Id = Guid.NewGuid(),
                    CreatedAt = DateTime.UtcNow
                };
                dbContext.Accounts.Add(account);

                foreach (var loc in locationsToSee)
                {
                    account.SeenLocations.Add(new SeenLocation(account, loc.Id, DateTime.UtcNow));
                }
            }
        }
    }
}
