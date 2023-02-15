using System.Collections.Concurrent;

namespace Pal.Client.DependencyInjection
{
    internal sealed class FloorService
    {
        public ConcurrentDictionary<ushort, LocalState> FloorMarkers { get; } = new();
        public ConcurrentBag<Marker> EphemeralMarkers { get; set; } = new();

        public LocalState GetFloorMarkers(ushort territoryType)
        {
            return FloorMarkers.GetOrAdd(territoryType, tt => LocalState.Load(tt) ?? new LocalState(tt));
        }
    }
}
