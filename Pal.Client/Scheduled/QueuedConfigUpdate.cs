using Microsoft.Extensions.Logging;
using Pal.Client.Configuration;
using Pal.Client.DependencyInjection;
using Pal.Client.Rendering;

namespace Pal.Client.Scheduled
{
    internal sealed class QueuedConfigUpdate : IQueueOnFrameworkThread
    {
        internal sealed class Handler : IQueueOnFrameworkThread.Handler<QueuedConfigUpdate>
        {
            private readonly IPalacePalConfiguration _configuration;
            private readonly FloorService _floorService;
            private readonly TerritoryState _territoryState;
            private readonly RenderAdapter _renderAdapter;

            public Handler(
                ILogger<Handler> logger,
                IPalacePalConfiguration configuration,
                FloorService floorService,
                TerritoryState territoryState,
                RenderAdapter renderAdapter)
                : base(logger)
            {
                _configuration = configuration;
                _floorService = floorService;
                _territoryState = territoryState;
                _renderAdapter = renderAdapter;
            }

            protected override void Run(QueuedConfigUpdate queued, ref bool recreateLayout, ref bool saveMarkers)
            {
                if (_configuration.Mode == EMode.Offline)
                {
                    LocalState.UpdateAll();
                    _floorService.FloorMarkers.Clear();
                    _floorService.EphemeralMarkers.Clear();
                    _territoryState.LastTerritory = 0;

                    recreateLayout = true;
                    saveMarkers = true;
                }

                _renderAdapter.ConfigUpdated();
            }
        }
    }
}
