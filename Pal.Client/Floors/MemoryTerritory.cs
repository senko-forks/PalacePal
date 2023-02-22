using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Pal.Client.Configuration;
using Pal.Client.Scheduled;
using Pal.Common;

namespace Pal.Client.Floors
{
    /// <summary>
    /// A single set of floors loaded entirely in memory, can be e.g. POTD 51-60.
    /// </summary>
    internal sealed class MemoryTerritory
    {
        public MemoryTerritory(ETerritoryType territoryType)
        {
            TerritoryType = territoryType;
        }

        public ETerritoryType TerritoryType { get; }
        public bool IsReady { get; set; }
        public bool IsLoading { get; set; } // probably merge this with IsReady as enum
        public ESyncState SyncState { get; set; } = ESyncState.NotAttempted;

        public ConcurrentBag<PersistentLocation> Locations { get; } = new();
        public object LockObj { get; } = new();

        public void Initialize(IEnumerable<PersistentLocation> locations)
        {
            Locations.Clear();
            foreach (var location in locations)
                Locations.Add(location);

            IsReady = true;
            IsLoading = false;
        }

        public IEnumerable<PersistentLocation> GetRemovableLocations(EMode mode)
        {
            // TODO there was better logic here;
            return Locations.Where(x => !x.Seen);
        }

        public void Reset()
        {
            Locations.Clear();
            IsReady = false;
            IsLoading = false;
        }
    }
}
