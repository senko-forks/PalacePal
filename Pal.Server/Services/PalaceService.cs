using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Pal.Common;
using Palace;
using System.Collections.Concurrent;
using static Palace.PalaceService;

namespace Pal.Server.Services
{
    internal class PalaceService : PalaceServiceBase
    {
        private readonly ILogger<PalaceService> _logger;
        private readonly PalContext _dbContext;
        private readonly PalaceLocationCache _cache;


        public PalaceService(ILogger<PalaceService> logger, PalContext dbContext, PalaceLocationCache cache)
        {
            _logger = logger;
            _dbContext = dbContext;
            _cache = cache;
        }

        [Authorize]
        public override async Task<DownloadFloorsReply> DownloadFloors(DownloadFloorsRequest request, ServerCallContext context)
        {
            try
            {
                ushort territoryType = (ushort)request.TerritoryType;
                if (!typeof(ETerritoryType).IsEnumDefined(territoryType))
                {
                    _logger.LogInformation("Skipping download for unknown territory type {TerritoryType}", territoryType);
                    return new DownloadFloorsReply { Success = false };
                }

                if (!_cache.TryGetValue(territoryType, out var objects))
                    objects = await LoadObjects(territoryType, context.CancellationToken);

                var reply = new DownloadFloorsReply { Success = true };
                reply.Objects.AddRange(objects!.Values);
                return reply;
            } 
            catch (Exception e)
            {
                _logger.LogError("Could not download floors for territory {TerritoryType}: {e}", request.TerritoryType, e);
                return new DownloadFloorsReply { Success = false };
            }
        }

        [Authorize]
        public override async Task<UploadFloorsReply> UploadFloors(UploadFloorsRequest request, ServerCallContext context)
        {
            try
            {
                var accountId = context.GetAccountId();
                var territoryType = (ushort)request.TerritoryType;

                if (!typeof(ETerritoryType).IsEnumDefined(territoryType))
                {
                    _logger.LogInformation("Skipping upload for unknown territory type {TerritoryType}", territoryType);
                    return new UploadFloorsReply { Success = false };
                }

                // only happens when the server is being restarted while people are currently doing potd/hoh runs and have downloaded the floor layout prior to the restart
                if (!_cache.TryGetValue(territoryType, out var objects))
                    objects = await LoadObjects(territoryType, context.CancellationToken);

                DateTime createdAt = DateTime.Now;
                var newLocations = request.Objects.Where(o => !objects!.Values.Any(x => CalculateHash(x) == CalculateHash(o)))
                    .Where(o => o.Type != ObjectType.Unknown && o.X != 0 && o.Y != 0 && o.Z != 0)
                    .DistinctBy(o => CalculateHash(o))
                    .Select(o => new PalaceLocation
                    {
                        Id = Guid.NewGuid(),
                        TerritoryType = territoryType,
                        Type = (PalaceLocation.EType)o.Type,
                        X = o.X,
                        Y = o.Y,
                        Z = o.Z,
                        AccountId = accountId,
                        CreatedAt = createdAt,
                    })
                    .ToList();
                var reply = new UploadFloorsReply { Success = true };
                if (newLocations.Count > 0)
                {
                    await _dbContext.AddRangeAsync(newLocations, context.CancellationToken);
                    await _dbContext.SaveChangesAsync(context.CancellationToken);

                    foreach (var location in newLocations)
                    {
                        var palaceObj = new PalaceObject { Type = (ObjectType)location.Type, X = location.X, Y = location.Y, Z = location.Z, NetworkId = location.Id.ToString() };
                        objects![location.Id] = palaceObj;
                        reply.Objects.Add(palaceObj);
                    }

                    _logger.LogInformation("Saved {Count} new locations for {TerritoryName} ({TerritoryType})", newLocations.Count, (ETerritoryType)territoryType, territoryType);
                }
                else
                    _logger.LogInformation("Saved no objects for {TerritoryName} ({TerritoryType}) - all already known", (ETerritoryType)territoryType, territoryType);

                return reply;
            }
            catch (Exception e)
            {
                _logger.LogError("Could not save {Count} new objects for territory type {TerritoryType}: {e}", request.Objects.Count, request.TerritoryType, e);
                return new UploadFloorsReply { Success = false };
            }
        }

        [Authorize]
        public override async Task<MarkObjectsSeenReply> MarkObjectsSeen(MarkObjectsSeenRequest request, ServerCallContext context)
        {
            try
            {
                ushort territoryType = (ushort)request.TerritoryType;
                if (!typeof(ETerritoryType).IsEnumDefined(territoryType))
                {
                    _logger.LogInformation("Skipping mark objects seen for unknown territory type {TerritoryType}", territoryType);
                    return new MarkObjectsSeenReply { Success = false };
                }

                if (!_cache.TryGetValue(territoryType, out var objects))
                    objects = await LoadObjects(territoryType, context.CancellationToken);

                var account = await _dbContext.Accounts.FindAsync(new object[] { context.GetAccountId() }, cancellationToken: context.CancellationToken);
                if (account == null)
                {
                    _logger.LogInformation("Skipping mark objects seen, account not found");
                    return new MarkObjectsSeenReply { Success = false };
                }

                var seenLocations = account.SeenLocations;
                var newLocations = request.NetworkIds.Select(x => Guid.Parse(x))
                    .Where(x => objects!.ContainsKey(x))
                    .Where(x => !seenLocations.Any(seen => seen.PalaceLocationId == x))
                    .Select(x => new SeenLocation(account, x))
                    .ToList();
                if (newLocations.Count > 0)
                {
                    _logger.LogInformation("Mark {} locations as seen for account {} on territory {TerritoryType}", newLocations.Count, account.Id, territoryType);
                    account.SeenLocations.AddRange(newLocations);
                    await _dbContext.SaveChangesAsync(context.CancellationToken);
                }
                return new MarkObjectsSeenReply { Success = true };
            }
            catch (Exception e)
            {
                _logger.LogError("Could not mark objects seen for territory {TerritoryType}: {e}", request.TerritoryType, e);
                return new MarkObjectsSeenReply { Success = false };
            }
        }

        [Authorize(Roles = "statistics:view")]
        public override async Task<StatisticsReply> FetchStatistics(StatisticsRequest request, ServerCallContext context)
        {
            try
            {
                var reply = new StatisticsReply { Success = true };
                foreach (ETerritoryType territoryType in typeof(ETerritoryType).GetEnumValues())
                {
                    if (!_cache.TryGetValue((ushort)territoryType, out var objects))
                        objects = await LoadObjects((ushort)territoryType, context.CancellationToken);

                    reply.FloorStatistics.Add(new FloorStatistics
                    {
                        TerritoryType = (ushort)territoryType,
                        TrapCount = (uint)objects!.Values.Count(x => x.Type == ObjectType.Trap),
                        HoardCount = (uint)objects!.Values.Count(x => x.Type == ObjectType.Hoard),
                    });
                }
                return reply;
            } 
            catch (Exception e)
            {
                _logger.LogError("Could not fetch statistics: {e}", e);
                return new StatisticsReply { Success = false };
            }
        }

        private async Task<ConcurrentDictionary<Guid, PalaceObject>> LoadObjects(ushort territoryType, CancellationToken cancellationToken)
        {
            var objects = await _dbContext.Locations.Where(o => o.TerritoryType == territoryType)
                .ToDictionaryAsync(o => o.Id, o => new PalaceObject { Type = (ObjectType)o.Type, X = o.X, Y = o.Y, Z = o.Z, NetworkId = o.Id.ToString() }, cancellationToken);

            var result = _cache.Add(territoryType, new ConcurrentDictionary<Guid, PalaceObject>(objects));
            return result;
        }

        private int CalculateHash(PalaceObject obj) => HashCode.Combine(obj.Type, (int)obj.X, (int)obj.Y, (int)obj.Z);
    }
}
