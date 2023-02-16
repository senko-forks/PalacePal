using System;
using System.Collections.Generic;
using Pal.Client.Configuration;
using Pal.Client.DependencyInjection;
using Pal.Client.Windows;
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
            private readonly ImportService _importService;
            private readonly FloorService _floorService;
            private readonly ConfigWindow _configWindow;

            public Handler(ImportService importService, FloorService floorService, ConfigWindow configWindow)
            {
                _importService = importService;
                _floorService = floorService;
                _configWindow = configWindow;
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

                _importService.RemoveById(queued.ExportId);
                _configWindow.UpdateLastImport();
            }
        }
    }
}
