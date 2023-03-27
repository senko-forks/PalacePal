extern alias PalServer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Pal.Common;
using Pal.Server.Tests.TestUtils;
using Palace;
using PalServer::Pal.Server.Database;
using Xunit;

namespace Pal.Server.Tests.Services
{
    public sealed class PalaceServiceTests : IClassFixture<AuthorizedGrpc>
    {
        private readonly AuthorizedGrpc _grpc;

        public PalaceServiceTests(AuthorizedGrpc grpc)
        {
            _grpc = grpc;
        }

        [Fact]
        public async Task DownloadFloor()
        {
            IReadOnlyList<ServerLocation> locations = _grpc.WithDb(dbContext =>
            {
                dbContext.ResetToDefaultAccount();
                return CreateEurekaOrthosLocations(dbContext);
            });

            var auth = await _grpc.LoginAsync();

            var downloadReply = await _grpc.PalaceClient.DownloadFloorsAsync(new DownloadFloorsRequest
            {
                TerritoryType = (ushort)ETerritoryType.EurekaOrthos_91_100,
            }, auth);
            downloadReply.Success.Should().BeTrue();
            downloadReply.Objects.Should().HaveCount(locations.Count);
            downloadReply.Objects.Should().Contain(locations.Select(loc => new PalaceObject
            {
                NetworkId = loc.Id.ToString(),
                Type = loc.Type == ServerLocation.EType.Trap ? ObjectType.Trap : ObjectType.Hoard,
                X = loc.X,
                Y = loc.Y,
                Z = loc.Z,
            }));
        }

        [Fact]
        public void AnonymousDownloadFloorShouldFail()
        {
            _grpc.WithDb(dbContext => dbContext.ResetToDefaultAccount());

            Action download = () => _grpc.PalaceClient.DownloadFloors(new DownloadFloorsRequest
            {
                TerritoryType = (ushort)ETerritoryType.Palace_1_10,
            });
            download.Should()
                .Throw<RpcException>()
                .Where(e => e.StatusCode == StatusCode.Unauthenticated);
        }

        [Fact]
        public async Task UploadFloor()
        {
            _grpc.WithDb(dbContext => dbContext.ResetToDefaultAccount());
            var auth = await _grpc.LoginAsync();

            #region Upload markers

            DateTime before = DateTime.UtcNow;
            PalaceObject trap = new()
            {
                Type = ObjectType.Trap,
                X = 1,
                Y = 2,
                Z = 3,
            };
            PalaceObject hoard = new()
            {
                Type = ObjectType.Hoard,
                X = 4,
                Y = 5,
                Z = 6,
            };
            var uploadRequest = new UploadFloorsRequest
            {
                TerritoryType = (ushort)ETerritoryType.HeavenOnHigh_51_60,
                Objects = { trap, hoard },
            };
            var uploadReply = await _grpc.PalaceClient.UploadFloorsAsync(uploadRequest, auth);
            uploadReply.Success.Should().BeTrue();
            uploadReply.Objects.Should().HaveCount(2);
            uploadReply.Objects.Should().ContainSingle(obj => obj.Type == trap.Type)
                .Subject.Should().BeLocatedAt(trap.X, trap.Y, trap.Z);
            uploadReply.Objects.Should().ContainSingle(obj => obj.Type == hoard.Type)
                .Subject.Should().BeLocatedAt(hoard.X, hoard.Y, hoard.Z);

            #endregion

            #region Data is persisted

            IReadOnlyList<ServerLocation> serverLocations = _grpc.WithDb(dbContext =>
                dbContext.Locations.Where(loc => loc.TerritoryType == uploadRequest.TerritoryType).ToList());
            serverLocations.Should().HaveCount(2);
            serverLocations.Should().Contain(loc => loc.Type == ServerLocation.EType.Trap)
                .Subject.Should().BeLocatedAt(trap.X, trap.Y, trap.Z);
            serverLocations.Should().Contain(loc => loc.Type == ServerLocation.EType.Hoard)
                .Subject.Should().BeLocatedAt(hoard.X, hoard.Y, hoard.Z);
            serverLocations.Should().AllSatisfy(loc => loc.AccountId.Should().Be(DbUtils.DefaultAccountId));
            serverLocations.Should().AllSatisfy(loc => loc.CreatedAt.Should().BeAfter(before));

            var networkIds = serverLocations.Select(x => x.Id.ToString()).ToList();

            #endregion

            #region Download returns newly uploaded markers

            var downloadReply = await _grpc.PalaceClient.DownloadFloorsAsync(new DownloadFloorsRequest
            {
                TerritoryType = (ushort)ETerritoryType.HeavenOnHigh_51_60,
            }, auth);
            downloadReply.Success.Should().BeTrue();
            downloadReply.Objects.Should().HaveSameCount(uploadReply.Objects);
            downloadReply.Objects.Should().Contain(uploadReply.Objects);
            downloadReply.Objects.Should().AllSatisfy(obj => obj.NetworkId.Should().BeOneOf(networkIds));

            #endregion
        }

        [Fact]
        public async Task ShouldIgnoreDuplicateUploads()
        {
            IReadOnlyList<ServerLocation> existingLocations = _grpc.WithDb(dbContext =>
            {
                dbContext.ResetToDefaultAccount();
                return CreateEurekaOrthosLocations(dbContext);
            });
            var auth = await _grpc.LoginAsync();

            var existingLocation = existingLocations.First();
            var uploadRequest = new UploadFloorsRequest
            {
                TerritoryType = existingLocation.TerritoryType,
                Objects =
                {
                    new PalaceObject
                    {
                        Type = existingLocation.Type == ServerLocation.EType.Trap ? ObjectType.Trap : ObjectType.Hoard,
                        X = existingLocation.X,
                        Y = existingLocation.Y,
                        Z = existingLocation.Z + 0.001f,
                    }
                }
            };
            var uploadReply = await _grpc.PalaceClient.UploadFloorsAsync(uploadRequest, auth);
            uploadReply.Success.Should().BeTrue();
            uploadReply.Objects.Should().BeEmpty();

            IReadOnlyList<ServerLocation> locationsAfterUpdate =
                _grpc.WithDb(dbContext =>
                    dbContext.Locations.Where(loc => loc.TerritoryType == (ushort)ETerritoryType.EurekaOrthos_91_100)
                        .ToList());
            locationsAfterUpdate.Should().HaveSameCount(existingLocations);
        }

        [Fact]
        public void AnonymousUploadFloorShouldFail()
        {
            _grpc.WithDb(dbContext => dbContext.ResetToDefaultAccount());

            Action upload = () => _grpc.PalaceClient.UploadFloors(new UploadFloorsRequest
            {
                TerritoryType = (ushort)ETerritoryType.Palace_11_20,
            });
            upload.Should()
                .Throw<RpcException>()
                .Where(e => e.StatusCode == StatusCode.Unauthenticated);
        }

        [Fact]
        public async Task MarkExistingObjectSeen()
        {
            IReadOnlyList<ServerLocation> locations = _grpc.WithDb(dbContext =>
            {
                dbContext.ResetToDefaultAccount();
                return CreateEurekaOrthosLocations(dbContext);
            });
            var auth = await _grpc.LoginAsync();

            var newlySeenIds = locations.Take(2).Select(x => x.Id).ToList();
            var markSeenReply = await _grpc.PalaceClient.MarkObjectsSeenAsync(new MarkObjectsSeenRequest
            {
                TerritoryType = (ushort)ETerritoryType.EurekaOrthos_91_100,
                NetworkIds = { newlySeenIds.Select(x => x.ToString()) },
            }, auth);
            markSeenReply.Success.Should().BeTrue();

            var seenLocations = _grpc.WithDb(dbContext => dbContext.Accounts
                .Include(x => x.SeenLocations)
                .AsSplitQuery()
                .First(x => x.Id == DbUtils.DefaultAccountId)
                .SeenLocations
                .Select(x => x.PalaceLocationId)
                .ToList());
            seenLocations.Should().Contain(newlySeenIds);
        }

        [Fact]
        public async Task MarkNonExistingObjectSeen()
        {
            IList<SeenLocation> existingSeenLocations = _grpc.WithDb(dbContext =>
            {
                dbContext.ResetToDefaultAccount();
                return dbContext.Accounts
                    .Include(x => x.SeenLocations)
                    .AsSplitQuery()
                    .First(x => x.Id == DbUtils.DefaultAccountId)
                    .SeenLocations.ToList();
            });
            existingSeenLocations.Should().NotBeEmpty();
            var auth = await _grpc.LoginAsync();

            var markSeenReply = await _grpc.PalaceClient.MarkObjectsSeenAsync(new MarkObjectsSeenRequest
            {
                TerritoryType = (ushort)ETerritoryType.EurekaOrthos_91_100,
                NetworkIds = { Guid.NewGuid().ToString() }
            }, auth);
            markSeenReply.Success.Should().BeTrue();

            IList<SeenLocation> newSeenLocations = _grpc.WithDb(dbContext => dbContext.Accounts
                .Include(x => x.SeenLocations)
                .AsSplitQuery()
                .First(x => x.Id == DbUtils.DefaultAccountId)
                .SeenLocations.ToList());
            newSeenLocations.Should().HaveSameCount(existingSeenLocations);
        }

        [Fact]
        public async Task MarkObjectSeenTwiceIgnoresSecondCall()
        {
            IReadOnlyList<ServerLocation> locations = _grpc.WithDb(dbContext =>
            {
                dbContext.ResetToDefaultAccount();
                return CreateEurekaOrthosLocations(dbContext);
            });
            var auth = await _grpc.LoginAsync();

            var newlySeenId = locations.Select(x => x.Id).First();
            var markSeenReply = await _grpc.PalaceClient.MarkObjectsSeenAsync(new MarkObjectsSeenRequest
            {
                TerritoryType = (ushort)ETerritoryType.EurekaOrthos_91_100,
                NetworkIds = { newlySeenId.ToString() },
            }, auth);
            markSeenReply.Success.Should().BeTrue();

            var seenLocations = _grpc.WithDb(dbContext => dbContext.Accounts
                .Include(x => x.SeenLocations)
                .AsSplitQuery()
                .First(x => x.Id == DbUtils.DefaultAccountId)
                .SeenLocations
                .Select(x => x.PalaceLocationId)
                .ToList());
            seenLocations.Should().Contain(newlySeenId);

            // second call should succeed, but has no effect due to unique db constraints
            markSeenReply = await _grpc.PalaceClient.MarkObjectsSeenAsync(new MarkObjectsSeenRequest
            {
                TerritoryType = (ushort)ETerritoryType.EurekaOrthos_91_100,
                NetworkIds = { newlySeenId.ToString() },
            }, auth);
            markSeenReply.Success.Should().BeTrue();
        }

        [Fact]
        public void AnonymousMarkObjectSeenShouldFail()
        {
            _grpc.WithDb(dbContext => dbContext.ResetToDefaultAccount());

            Action markSeen = () => _grpc.PalaceClient.MarkObjectsSeen(new MarkObjectsSeenRequest
            {
                TerritoryType = (ushort)ETerritoryType.Palace_11_20,
                NetworkIds = { Guid.NewGuid().ToString() },
            });
            markSeen.Should()
                .Throw<RpcException>()
                .Where(e => e.StatusCode == StatusCode.Unauthenticated);
        }

        [Fact]
        public async Task FetchStatistics()
        {
            _grpc.WithDb(dbContext =>
            {
                dbContext.ResetToDefaultAccount(new List<string> { "statistics:view" });
                CreateEurekaOrthosLocations(dbContext);
            });
            var auth = await _grpc.LoginAsync();

            var statisticsReply = await _grpc.PalaceClient.FetchStatisticsAsync(new StatisticsRequest(), auth);
            statisticsReply.Success.Should().BeTrue();
            statisticsReply.FloorStatistics.Should().HaveSameCount(Enum.GetValues<ETerritoryType>());

            var floor = statisticsReply.FloorStatistics.Should()
                .ContainSingle(floor => floor.TerritoryType == (ushort)ETerritoryType.EurekaOrthos_91_100);
            floor.Which.TrapCount.Should().Be(3);
            floor.Which.HoardCount.Should().Be(2);
        }

        [Fact]
        public async Task FetchStatisticsWithoutRoleShouldFail()
        {
            _grpc.WithDb(dbContext => dbContext.ResetToDefaultAccount());
            var auth = await _grpc.LoginAsync();

            Action markSeen = () => _grpc.PalaceClient.FetchStatistics(new StatisticsRequest(), auth);
            markSeen.Should()
                .Throw<RpcException>()
                .Where(e => e.StatusCode == StatusCode.PermissionDenied);
        }

        [Fact]
        public void AnonymousFetchStatisticsShouldFail()
        {
            _grpc.WithDb(dbContext => dbContext.ResetToDefaultAccount());

            Action markSeen = () => _grpc.PalaceClient.FetchStatistics(new StatisticsRequest());
            markSeen.Should()
                .Throw<RpcException>()
                .Where(e => e.StatusCode == StatusCode.Unauthenticated);
        }

        private IReadOnlyList<ServerLocation> CreateEurekaOrthosLocations(PalServerContext dbContext)
        {
            List<ServerLocation> locations = Enumerable.Range(0, 5).Select(x => new ServerLocation
            {
                Id = Guid.NewGuid(),
                TerritoryType = (ushort)ETerritoryType.EurekaOrthos_91_100,
                Type = x % 2 == 0 ? ServerLocation.EType.Trap : ServerLocation.EType.Hoard,
                X = x,
                Y = 2.5f,
                Z = 34,
                AccountId = Guid.Empty,
                CreatedAt = DateTime.UtcNow,
            }).ToList();
            dbContext.Locations.AddRange(locations);
            return locations.AsReadOnly();
        }
    }
}
