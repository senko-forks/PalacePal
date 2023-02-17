using Account;
using Pal.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Dalamud.Game.Gui;
using Dalamud.Logging;
using Microsoft.Extensions.Logging;
using Pal.Client.Database;
using Pal.Client.DependencyInjection;
using Pal.Client.Extensions;
using Pal.Client.Properties;
using Pal.Client.Windows;

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
            private readonly Chat _chat;
            private readonly FloorService _floorService;
            private readonly ImportService _importService;
            private readonly ConfigWindow _configWindow;

            public Handler(
                ILogger<Handler> logger,
                Chat chat,
                FloorService floorService,
                ImportService importService,
                ConfigWindow configWindow)
                : base(logger)
            {
                _chat = chat;
                _floorService = floorService;
                _importService = importService;
                _configWindow = configWindow;
            }

            protected override void Run(QueuedImport import, ref bool recreateLayout, ref bool saveMarkers)
            {
                recreateLayout = true;
                saveMarkers = true;

                try
                {
                    if (!Validate(import))
                        return;

                    List<Guid> oldExportIds = _importService.FindForServer(import.Export.ServerUrl)
                        .Select(x => x.Id)
                        .ToList();

                    foreach (var remoteFloor in import.Export.Floors)
                    {
                        ushort territoryType = (ushort)remoteFloor.TerritoryType;
                        var localState = _floorService.GetFloorMarkers(territoryType);

                        localState.UndoImport(oldExportIds);
                        ImportFloor(import, remoteFloor, localState);

                        localState.Save();
                    }

                    _importService.RemoveAllByIds(oldExportIds);
                    _importService.RemoveById(import.ExportId);
                    _importService.Add(new ImportHistory
                    {
                        Id = import.ExportId,
                        RemoteUrl = import.Export.ServerUrl,
                        ExportedAt = import.Export.CreatedAt.ToDateTime(),
                        ImportedAt = DateTime.UtcNow,
                    });
                    _configWindow.UpdateLastImport();

                    _logger.LogInformation(
                        $"Imported {import.ExportId} for {import.ImportedTraps} traps, {import.ImportedHoardCoffers} hoard coffers");
                    _chat.Message(string.Format(Localization.ImportCompleteStatistics, import.ImportedTraps,
                        import.ImportedHoardCoffers));
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Import failed");
                    _chat.Error(string.Format(Localization.Error_ImportFailed, e));
                }
            }

            private bool Validate(QueuedImport import)
            {
                if (import.Export.ExportVersion != ExportConfig.ExportVersion)
                {
                    _logger.LogError(
                        "Import: Different version in export file, {ExportVersion} != {ConfiguredVersion}", import.Export.ExportVersion, ExportConfig.ExportVersion);
                    _chat.Error(Localization.Error_ImportFailed_IncompatibleVersion);
                    return false;
                }

                if (!Guid.TryParse(import.Export.ExportId, out Guid exportId) || exportId == Guid.Empty)
                {
                    _logger.LogError("Import: Invalid export id '{Id}'", import.Export.ExportId);
                    _chat.Error(Localization.Error_ImportFailed_InvalidFile);
                    return false;
                }

                import.ExportId = exportId;

                if (string.IsNullOrEmpty(import.Export.ServerUrl))
                {
                    // If we allow for backups as import/export, this should be removed
                    _logger.LogError("Import: No server URL");
                    _chat.Error(Localization.Error_ImportFailed_InvalidFile);
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
