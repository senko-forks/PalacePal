using ECommons.Configuration;
using Pal.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pal.Client.Scheduled
{
    internal class QueuedUndoImport : IQueueOnFrameworkThread
    {
        private readonly Guid _exportId;

        public QueuedUndoImport(Guid exportId)
        {
            _exportId = exportId;
        }

        public void Run(Plugin plugin, ref bool recreateLayout, ref bool saveMarkers)
        {
            recreateLayout = true;
            saveMarkers = true;

            foreach (ETerritoryType territoryType in typeof(ETerritoryType).GetEnumValues())
            {
                var localState = plugin.GetFloorMarkers((ushort)territoryType);
                localState.UndoImport(new List<Guid> { _exportId });
                localState.Save();
            }

            Service.Configuration.ImportHistory.RemoveAll(hist => hist.Id == _exportId);
        }
    }
}
