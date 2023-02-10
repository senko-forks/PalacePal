using Account;
using Dalamud.Logging;
using Pal.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Pal.Client.Extensions;
using Pal.Client.Properties;

namespace Pal.Client.Scheduled
{
    internal class QueuedImport : IQueueOnFrameworkThread
    {
        private readonly ExportRoot _export;
        private Guid _exportId;
        private int importedTraps;
        private int importedHoardCoffers;

        public QueuedImport(string sourcePath)
        {
            using (var input = File.OpenRead(sourcePath))
                _export = ExportRoot.Parser.ParseFrom(input);
        }

        public void Run(Plugin plugin, ref bool recreateLayout, ref bool saveMarkers)
        {
            try
            {
                if (!Validate())
                    return;

                var config = Service.Configuration;
                var oldExportIds = string.IsNullOrEmpty(_export.ServerUrl) ? config.ImportHistory.Where(x => x.RemoteUrl == _export.ServerUrl).Select(x => x.Id).Where(x => x != Guid.Empty).ToList() : new List<Guid>();

                foreach (var remoteFloor in _export.Floors)
                {
                    ushort territoryType = (ushort)remoteFloor.TerritoryType;
                    var localState = plugin.GetFloorMarkers(territoryType);

                    localState.UndoImport(oldExportIds);
                    ImportFloor(remoteFloor, localState);

                    localState.Save();
                }

                config.ImportHistory.RemoveAll(hist => oldExportIds.Contains(hist.Id) || hist.Id == _exportId);
                config.ImportHistory.Add(new Configuration.ImportHistoryEntry
                {
                    Id = _exportId,
                    RemoteUrl = _export.ServerUrl,
                    ExportedAt = _export.CreatedAt.ToDateTime(),
                    ImportedAt = DateTime.UtcNow,
                });
                config.Save();

                recreateLayout = true;
                saveMarkers = true;

                Service.Chat.Print(string.Format(Localization.ImportCompleteStatistics, importedTraps, importedHoardCoffers));
            }
            catch (Exception e)
            {
                PluginLog.Error(e, "Import failed");
                Service.Chat.PalError(string.Format(Localization.Error_ImportFailed, e));
            }
        }

        private bool Validate()
        {
            if (_export.ExportVersion != ExportConfig.ExportVersion)
            {
                Service.Chat.PrintError(Localization.Error_ImportFailed_IncompatibleVersion);
                return false;
            }

            if (!Guid.TryParse(_export.ExportId, out _exportId) || _exportId == Guid.Empty)
            {
                Service.Chat.PrintError(Localization.Error_ImportFailed_InvalidFile);
                return false;
            }

            if (string.IsNullOrEmpty(_export.ServerUrl))
            {
                // If we allow for backups as import/export, this should be removed
                Service.Chat.PrintError(Localization.Error_ImportFailed_InvalidFile);
                return false;
            }

            return true;
        }

        private void ImportFloor(ExportFloor remoteFloor, LocalState localState)
        {
            var remoteMarkers = remoteFloor.Objects.Select(m => new Marker((Marker.EType)m.Type, new Vector3(m.X, m.Y, m.Z)) { WasImported = true });
            foreach (var remoteMarker in remoteMarkers)
            {
                Marker? localMarker = localState.Markers.SingleOrDefault(x => x == remoteMarker);
                if (localMarker == null)
                {
                    localState.Markers.Add(remoteMarker);
                    localMarker = remoteMarker;

                    if (localMarker.Type == Marker.EType.Trap)
                        importedTraps++;
                    else if (localMarker.Type == Marker.EType.Hoard)
                        importedHoardCoffers++;
                }

                remoteMarker.Imports.Add(_exportId);
            }
        }
    }
}
