using System.Collections.Generic;

namespace Pal.Client.Rendering
{
    internal sealed class MarkerConfig
    {
        private static readonly MarkerConfig EmptyConfig = new();
        private static readonly Dictionary<Marker.EType, MarkerConfig> MarkerConfigs = new()
        {
            { Marker.EType.Trap, new MarkerConfig { Radius = 1.7f } },
            { Marker.EType.Hoard, new MarkerConfig { Radius = 1.7f, OffsetY = -0.03f } },
            { Marker.EType.SilverCoffer, new MarkerConfig { Radius = 1f } },
        };

        public float OffsetY { get; private init; }
        public float Radius { get; private init; } = 0.25f;

        public static MarkerConfig ForType(Marker.EType type) => MarkerConfigs.GetValueOrDefault(type, EmptyConfig);
    }
}
