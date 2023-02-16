using Account;
using Pal.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Dalamud.Game.Gui;
using Dalamud.Logging;
using Pal.Client.Configuration;
using Pal.Client.DependencyInjection;
using Pal.Client.Extensions;
using Pal.Client.Properties;

namespace Pal.Client.Scheduled
{
    internal sealed class QueuedImport : IQueueOnFrameworkThread
    {
        private ExportRoot Export { get; }
        private Guid ExportId { get; set; }
        private int ImportedTraps { get; set; }
        private int ImportedHoardCoffers { get; set; }

        public QueuedImport(string sourcePath)
        {
            using var input = File.OpenRead(sourcePath);
            Export = ExportRoot.Parser.ParseFrom(input);
        }

        internal sealed class Handler : IQueueOnFrameworkThread.Handler<QueuedImport>
        {
            private readonly ChatGui _chatGui;
            private readonly IPalacePalConfiguration _configuration;
            private readonly ConfigurationManager _configurationManager;
            private readonly FloorService _floorService;

            public Handler(ChatGui chatGui, IPalacePalConfiguration configuration,
                ConfigurationManager configurationManager, FloorService floorService)
            {
                _chatGui = chatGui;
                _configuration = configuration;
                _configurationManager = configurationManager;
                _floorService = floorService;
            }

            protected override void Run(QueuedImport import, ref bool recreateLayout, ref bool saveMarkers)
            {
                recreateLayout = true;
                saveMarkers = true;

                try
                {
                    if (!Validate(import))
                        return;

                    var oldExportIds = string.IsNullOrEmpty(import.Export.ServerUrl)
                        ? _configuration.ImportHistory.Where(x => x.RemoteUrl == import.Export.ServerUrl)
                            .Select(x => x.Id)
                            .Where(x => x != Guid.Empty).ToList()
                        : new List<Guid>();

                    foreach (var remoteFloor in import.Export.Floors)
                    {
                        ushort territoryType = (ushort)remoteFloor.TerritoryType;
                        var localState = _floorService.GetFloorMarkers(territoryType);

                        localState.UndoImport(oldExportIds);
                        ImportFloor(import, remoteFloor, localState);

                        localState.Save();
                    }

                    _configuration.ImportHistory.RemoveAll(hist =>
                        oldExportIds.Contains(hist.Id) || hist.Id == import.ExportId);
                    _configuration.ImportHistory.Add(new ConfigurationV1.ImportHistoryEntry
                    {
                        Id = import.ExportId,
                        RemoteUrl = import.Export.ServerUrl,
                        ExportedAt = import.Export.CreatedAt.ToDateTime(),
                        ImportedAt = DateTime.UtcNow,
                    });
                    _configurationManager.Save(_configuration);

                    _chatGui.Print(string.Format(Localization.ImportCompleteStatistics, import.ImportedTraps,
                        import.ImportedHoardCoffers));
                }
                catch (Exception e)
                {
                    PluginLog.Error(e, "Import failed");
                    _chatGui.PalError(string.Format(Localization.Error_ImportFailed, e));
                }
            }

            private bool Validate(QueuedImport import)
            {
                if (import.Export.ExportVersion != ExportConfig.ExportVersion)
                {
                    _chatGui.PrintError(Localization.Error_ImportFailed_IncompatibleVersion);
                    return false;
                }

                if (!Guid.TryParse(import.Export.ExportId, out Guid exportId) || import.ExportId == Guid.Empty)
                {
                    _chatGui.PrintError(Localization.Error_ImportFailed_InvalidFile);
                    return false;
                }

                import.ExportId = exportId;

                if (string.IsNullOrEmpty(import.Export.ServerUrl))
                {
                    // If we allow for backups as import/export, this should be removed
                    _chatGui.PrintError(Localization.Error_ImportFailed_InvalidFile);
                    return false;
                }

                return true;
            }

            private void ImportFloor(QueuedImport import, ExportFloor remoteFloor, LocalState localState)
            {
                var remoteMarkers = remoteFloor.Objects.Select(m =>
                    new Marker((Marker.EType)m.Type, new Vector3(m.X, m.Y, m.Z)) { WasImported = true });
                foreach (var remoteMarker in remoteMarkers)
                {
                    Marker? localMarker = localState.Markers.SingleOrDefault(x => x == remoteMarker);
                    if (localMarker == null)
                    {
                        localState.Markers.Add(remoteMarker);
                        localMarker = remoteMarker;

                        if (localMarker.Type == Marker.EType.Trap)
                            import.ImportedTraps++;
                        else if (localMarker.Type == Marker.EType.Hoard)
                            import.ImportedHoardCoffers++;
                    }

                    remoteMarker.Imports.Add(import.ExportId);
                }
            }
        }
    }
}
