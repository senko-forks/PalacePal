using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Pal.Client.Database;
using Pal.Common;

namespace Pal.Client.Floors.Tasks
{
    internal sealed class SaveNewLocations : DbTask
    {
        private readonly MemoryTerritory _territory;
        private readonly List<PersistentLocation> _newLocations;

        public SaveNewLocations(IServiceScopeFactory serviceScopeFactory, MemoryTerritory territory,
            List<PersistentLocation> newLocations)
            : base(serviceScopeFactory)
        {
            _territory = territory;
            _newLocations = newLocations;
        }

        protected override void Run(PalClientContext dbContext)
        {
            Run(_territory, dbContext, _newLocations);
        }

        public static void Run(MemoryTerritory territory, PalClientContext dbContext,
            List<PersistentLocation> locations)
        {
            lock (territory.LockObj)
            {
                Dictionary<PersistentLocation, ClientLocation> mapping =
                    locations.ToDictionary(x => x, x => ToDatabaseLocation(x, territory.TerritoryType));
                dbContext.Locations.AddRange(mapping.Values);
                dbContext.SaveChanges();

                foreach ((PersistentLocation persistentLocation, ClientLocation clientLocation) in mapping)
                {
                    persistentLocation.LocalId = clientLocation.LocalId;
                }
            }
        }

        private static ClientLocation ToDatabaseLocation(PersistentLocation location, ETerritoryType territoryType)
        {
            return new ClientLocation
            {
                TerritoryType = (ushort)territoryType,
                Type = ToDatabaseType(location.Type),
                X = location.Position.X,
                Y = location.Position.Y,
                Z = location.Position.Z,
                Seen = location.Seen,
                SinceVersion = typeof(Plugin).Assembly.GetName().Version!.ToString(2),
            };
        }

        private static ClientLocation.EType ToDatabaseType(MemoryLocation.EType type)
        {
            return type switch
            {
                MemoryLocation.EType.Trap => ClientLocation.EType.Trap,
                MemoryLocation.EType.Hoard => ClientLocation.EType.Hoard,
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };
        }
    }
}
