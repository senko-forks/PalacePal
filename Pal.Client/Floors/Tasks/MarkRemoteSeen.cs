using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Pal.Client.Database;

namespace Pal.Client.Floors.Tasks
{
    internal sealed class MarkRemoteSeen : DbTask
    {
        private readonly MemoryTerritory _territory;
        private readonly IReadOnlyList<PersistentLocation> _locations;
        private readonly string _accountId;

        public MarkRemoteSeen(IServiceScopeFactory serviceScopeFactory,
            MemoryTerritory territory,
            IReadOnlyList<PersistentLocation> locations,
            string accountId)
            : base(serviceScopeFactory)
        {
            _territory = territory;
            _locations = locations;
            _accountId = accountId;
        }

        protected override void Run(PalClientContext dbContext)
        {
            lock (_territory.LockObj)
            {
                List<ClientLocation> locationsToUpdate = dbContext.Locations
                    .Where(loc => _locations.Any(l =>
                        l.LocalId == loc.LocalId && loc.RemoteEncounters.All(r => r.AccountId != _accountId)))
                    .ToList();
                foreach (var clientLocation in locationsToUpdate)
                    clientLocation.RemoteEncounters.Add(new RemoteEncounter(clientLocation, _accountId));
                dbContext.SaveChanges();
            }
        }
    }
}
