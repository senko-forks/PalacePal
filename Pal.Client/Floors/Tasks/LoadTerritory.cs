using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pal.Client.Database;

namespace Pal.Client.Floors.Tasks
{
    internal sealed class LoadTerritory : DbTask
    {
        private readonly MemoryTerritory _territory;

        public LoadTerritory(IServiceScopeFactory serviceScopeFactory, MemoryTerritory territory)
            : base(serviceScopeFactory)
        {
            _territory = territory;
        }

        protected override void Run(PalClientContext dbContext)
        {
            lock (_territory.LockObj)
            {
                if (_territory.IsReady)
                    return;

                List<ClientLocation> locations = dbContext.Locations
                    .Where(o => o.TerritoryType == (ushort)_territory.TerritoryType)
                    .Include(o => o.ImportedBy)
                    .Include(o => o.RemoteEncounters)
                    .ToList();
                _territory.Initialize(locations.Select(ToMemoryLocation));
            }
        }

        public static PersistentLocation ToMemoryLocation(ClientLocation location)
        {
            return new PersistentLocation
            {
                LocalId = location.LocalId,
                Type = ToMemoryLocationType(location.Type),
                Position = new Vector3(location.X, location.Y, location.Z),
                Seen = location.Seen,
                RemoteSeenOn = location.RemoteEncounters.Select(o => o.AccountId).ToList(),
            };
        }

        private static MemoryLocation.EType ToMemoryLocationType(ClientLocation.EType type)
        {
            return type switch
            {
                ClientLocation.EType.Trap => MemoryLocation.EType.Trap,
                ClientLocation.EType.Hoard => MemoryLocation.EType.Hoard,
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };
        }
    }
}
