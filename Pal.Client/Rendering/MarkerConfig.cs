using System.Collections.Generic;

namespace Pal.Client.Rendering
{
    internal class MarkerConfig
    {

        private readonly static Dictionary<Marker.EType, MarkerConfig> _markerConfig = new Dictionary<Marker.EType, MarkerConfig>
        {
            { Marker.EType.Trap, new MarkerConfig { Radius = 1.7f } },
            { Marker.EType.Hoard, new MarkerConfig { Radius = 1.7f, OffsetY = -0.03f } },
            { Marker.EType.SilverCoffer, new MarkerConfig { Radius = 1f } },
            { Marker.EType.Debug, new MarkerConfig { Radius = 1.7f, OffsetY = 0.1f } },
        };

        public float OffsetY { get; set; } = 0;
        public float Radius { get; set; } = 0.25f;

        public static MarkerConfig ForType(Marker.EType type) => _markerConfig[type] ?? new MarkerConfig();
    }
}
