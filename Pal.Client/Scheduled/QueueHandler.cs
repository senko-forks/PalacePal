using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Gui;
using Dalamud.Logging;
using Pal.Client.Configuration;
using Pal.Client.DependencyInjection;
using Pal.Client.Extensions;
using Pal.Client.Net;
using Pal.Client.Properties;
using Pal.Common;

namespace Pal.Client.Scheduled
{
    // TODO The idea was to split this from the queue objects, should be in individual classes tho
    internal sealed class QueueHandler
    {
        private readonly ConfigurationManager _configurationManager;
        private readonly IPalacePalConfiguration _configuration;
        private readonly FloorService _floorService;
        private readonly TerritoryState _territoryState;
        private readonly DebugState _debugState;
        private readonly ChatGui _chatGui;

        public QueueHandler(
            ConfigurationManager configurationManager,
            IPalacePalConfiguration configuration,
            FloorService floorService,
            TerritoryState territoryState,
            DebugState debugState,
            ChatGui chatGui)
        {
            _configurationManager = configurationManager;
            _configuration = configuration;
            _floorService = floorService;
            _territoryState = territoryState;
            _debugState = debugState;
            _chatGui = chatGui;
        }

        public void Handle(IQueueOnFrameworkThread queued, ref bool recreateLayout, ref bool saveMarkers)
        {
            if (queued is QueuedConfigUpdate)
            {
                ConfigUpdate(ref recreateLayout, ref saveMarkers);
            }
            else if (queued is QueuedSyncResponse queuedSyncResponse)
            {
                SyncResponse(queuedSyncResponse);
                recreateLayout = true;
                saveMarkers = true;
            }
            else if (queued is QueuedImport queuedImport)
            {
                Import(queuedImport);
                recreateLayout = true;
                saveMarkers = true;
            }
            else if (queued is QueuedUndoImport queuedUndoImport)
            {
                UndoImport(queuedUndoImport);
                recreateLayout = true;
                saveMarkers = true;
            }
            else
                throw new InvalidOperationException();
        }

        private void ConfigUpdate(ref bool recreateLayout, ref bool saveMarkers)
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
        }

        private void SyncResponse(QueuedSyncResponse queued)
        {
            try
            {
                var remoteMarkers = queued.Markers;
                var currentFloor = _floorService.GetFloorMarkers(queued.TerritoryType);
                if (_configuration.Mode == EMode.Online && queued.Success && remoteMarkers.Count > 0)
                {
                    switch (queued.Type)
                    {
                        case SyncType.Download:
                        case SyncType.Upload:
                            foreach (var remoteMarker in remoteMarkers)
                            {
                                // Both uploads and downloads return the network id to be set, but only the downloaded marker is new as in to-be-saved.
                                Marker? localMarker = currentFloor.Markers.SingleOrDefault(x => x == remoteMarker);
                                if (localMarker != null)
                                {
                                    localMarker.NetworkId = remoteMarker.NetworkId;
                                    continue;
                                }

                                if (queued.Type == SyncType.Download)
                                    currentFloor.Markers.Add(remoteMarker);
                            }

                            break;

                        case SyncType.MarkSeen:
                            var partialAccountId =
                                _configuration.FindAccount(RemoteApi.RemoteUrl)?.AccountId.ToPartialId();
                            if (partialAccountId == null)
                                break;
                            foreach (var remoteMarker in remoteMarkers)
                            {
                                Marker? localMarker = currentFloor.Markers.SingleOrDefault(x => x == remoteMarker);
                                if (localMarker != null)
                                    localMarker.RemoteSeenOn.Add(partialAccountId);
                            }

                            break;
                    }
                }

                // don't modify state for outdated floors
                if (_territoryState.LastTerritory != queued.TerritoryType)
                    return;

                if (queued.Type == SyncType.Download)
                {
                    if (queued.Success)
                        _territoryState.TerritorySyncState = SyncState.Complete;
                    else
                        _territoryState.TerritorySyncState = SyncState.Failed;
                }
            }
            catch (Exception e)
            {
                _debugState.SetFromException(e);
                if (queued.Type == SyncType.Download)
                    _territoryState.TerritorySyncState = SyncState.Failed;
            }
        }

        private void Import(QueuedImport queued)
        {
            try
            {
                if (!queued.Validate(_chatGui))
                    return;

                var oldExportIds = string.IsNullOrEmpty(queued.Export.ServerUrl)
                    ? _configuration.ImportHistory.Where(x => x.RemoteUrl == queued.Export.ServerUrl).Select(x => x.Id)
                        .Where(x => x != Guid.Empty).ToList()
                    : new List<Guid>();

                foreach (var remoteFloor in queued.Export.Floors)
                {
                    ushort territoryType = (ushort)remoteFloor.TerritoryType;
                    var localState = _floorService.GetFloorMarkers(territoryType);

                    localState.UndoImport(oldExportIds);
                    queued.ImportFloor(remoteFloor, localState);

                    localState.Save();
                }

                _configuration.ImportHistory.RemoveAll(hist =>
                    oldExportIds.Contains(hist.Id) || hist.Id == queued.ExportId);
                _configuration.ImportHistory.Add(new ConfigurationV1.ImportHistoryEntry
                {
                    Id = queued.ExportId,
                    RemoteUrl = queued.Export.ServerUrl,
                    ExportedAt = queued.Export.CreatedAt.ToDateTime(),
                    ImportedAt = DateTime.UtcNow,
                });
                _configurationManager.Save(_configuration);

                _chatGui.Print(string.Format(Localization.ImportCompleteStatistics, queued.ImportedTraps,
                    queued.ImportedHoardCoffers));
            }
            catch (Exception e)
            {
                PluginLog.Error(e, "Import failed");
                _chatGui.PalError(string.Format(Localization.Error_ImportFailed, e));
            }
        }

        private void UndoImport(QueuedUndoImport queued)
        {
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
