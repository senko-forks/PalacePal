using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pal.Client.Database;

namespace Pal.Client.Floors.Tasks
{
    internal sealed class MarkAsSeen : DbTask
    {
        private readonly MemoryTerritory _territory;
        private readonly IReadOnlyList<PersistentLocation> _locations;

        public MarkAsSeen(IServiceScopeFactory serviceScopeFactory, MemoryTerritory territory,
            IReadOnlyList<PersistentLocation> locations)
            : base(serviceScopeFactory)
        {
            _territory = territory;
            _locations = locations;
        }

        protected override void Run(PalClientContext dbContext)
        {
            lock (_territory.LockObj)
            {
                dbContext.Locations
                    .Where(loc => _locations.Any(l => l.LocalId == loc.LocalId))
                    .ExecuteUpdate(loc => loc.SetProperty(l => l.Seen, true));
                dbContext.SaveChanges();
            }
        }
    }
}
