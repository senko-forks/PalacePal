using System;
using System.Collections.Generic;
using Pal.Client.Configuration;
using Pal.Client.DependencyInjection;
using Pal.Common;

namespace Pal.Client.Scheduled
{
    internal sealed class QueuedUndoImport : IQueueOnFrameworkThread
    {
        public QueuedUndoImport(Guid exportId)
        {
            ExportId = exportId;
        }

        private Guid ExportId { get; }

        internal sealed class Handler : IQueueOnFrameworkThread.Handler<QueuedUndoImport>
        {
            private readonly IPalacePalConfiguration _configuration;
            private readonly FloorService _floorService;

            public Handler(IPalacePalConfiguration configuration, FloorService floorService)
            {
                _configuration = configuration;
                _floorService = floorService;
            }

            protected override void Run(QueuedUndoImport queued, ref bool recreateLayout, ref bool saveMarkers)
            {
                recreateLayout = true;
                saveMarkers = true;

                foreach (ETerritoryType territoryType in typeof(ETerritoryType).GetEnumValues())
                {
                    var localState = _floorService.GetFloorMarkers((ushort)territoryType);
                    localState.UndoImport(new List<Guid> { queued.ExportId });
                    localState.Save();
                }

                _configuration.ImportHistory.RemoveAll(hist => hist.Id == queued.ExportId);
            }
        }
    }
}
