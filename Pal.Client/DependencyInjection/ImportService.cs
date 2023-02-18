using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Account;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pal.Client.Database;
using Pal.Client.Floors;
using Pal.Client.Floors.Tasks;
using Pal.Common;

namespace Pal.Client.DependencyInjection
{
    internal sealed class ImportService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly FloorService _floorService;

        public ImportService(IServiceProvider serviceProvider, FloorService floorService)
        {
            _serviceProvider = serviceProvider;
            _floorService = floorService;
        }

        /*
        public void Add(ImportHistory history)
        {
            using var scope = _serviceProvider.CreateScope();
            using var dbContext = scope.ServiceProvider.GetRequiredService<PalClientContext>();

            dbContext.Imports.Add(history);
            dbContext.SaveChanges();
        }
        */

        public async Task<ImportHistory?> FindLast(CancellationToken token = default)
        {
            await using var scope = _serviceProvider.CreateAsyncScope();
            await using var dbContext = scope.ServiceProvider.GetRequiredService<PalClientContext>();

            return await dbContext.Imports.OrderByDescending(x => x.ImportedAt).ThenBy(x => x.Id).FirstOrDefaultAsync(cancellationToken: token);
        }

        /*
        public List<ImportHistory> FindForServer(string server)
        {
            if (string.IsNullOrEmpty(server))
                return new();

            using var scope = _serviceProvider.CreateScope();
            using var dbContext = scope.ServiceProvider.GetRequiredService<PalClientContext>();

            return dbContext.Imports.Where(x => x.RemoteUrl == server).ToList();
        }*/

        public (int traps, int hoard) Import(ExportRoot import)
        {
            using var scope = _serviceProvider.CreateScope();
            using var dbContext = scope.ServiceProvider.GetRequiredService<PalClientContext>();

            dbContext.Imports.RemoveRange(dbContext.Imports.Where(x => x.RemoteUrl == import.ServerUrl).ToList());

            ImportHistory importHistory = new ImportHistory
            {
                Id = Guid.Parse(import.ExportId),
                RemoteUrl = import.ServerUrl,
                ExportedAt = import.CreatedAt.ToDateTime(),
                ImportedAt = DateTime.UtcNow,
            };
            dbContext.Imports.Add(importHistory);

            int traps = 0;
            int hoard = 0;
            foreach (var floor in import.Floors)
            {
                ETerritoryType territoryType = (ETerritoryType)floor.TerritoryType;

                List<PersistentLocation> existingLocations = dbContext.Locations
                    .Where(loc => loc.TerritoryType == floor.TerritoryType)
                    .ToList()
                    .Select(LoadTerritory.ToMemoryLocation)
                    .ToList();
                foreach (var newLocation in floor.Objects)
                {
                    throw new NotImplementedException();
                }
            }
            // TODO filter here, update territories
            dbContext.SaveChanges();

            _floorService.ResetAll();
            return (traps, hoard);
        }

        public void RemoveById(Guid id)
        {
            using var scope = _serviceProvider.CreateScope();
            using var dbContext = scope.ServiceProvider.GetRequiredService<PalClientContext>();

            dbContext.RemoveRange(dbContext.Imports.Where(x => x.Id == id));

            // TODO filter here, update territories
            dbContext.SaveChanges();

            _floorService.ResetAll();
        }
    }
}
