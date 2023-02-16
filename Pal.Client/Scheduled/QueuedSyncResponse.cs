using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Pal.Client.Configuration;
using Pal.Client.DependencyInjection;
using Pal.Client.Extensions;
using Pal.Client.Net;

namespace Pal.Client.Scheduled
{
    internal sealed class QueuedSyncResponse : IQueueOnFrameworkThread
    {
        public required SyncType Type { get; init; }
        public required ushort TerritoryType { get; init; }
        public required bool Success { get; init; }
        public required List<Marker> Markers { get; init; }

        internal sealed class Handler : IQueueOnFrameworkThread.Handler<QueuedSyncResponse>
        {
            private readonly IPalacePalConfiguration _configuration;
            private readonly FloorService _floorService;
            private readonly TerritoryState _territoryState;
            private readonly DebugState _debugState;

            public Handler(
                ILogger<Handler> logger,
                IPalacePalConfiguration configuration,
                FloorService floorService,
                TerritoryState territoryState,
                DebugState debugState)
                : base(logger)
            {
                _configuration = configuration;
                _floorService = floorService;
                _territoryState = territoryState;
                _debugState = debugState;
            }

            protected override void Run(QueuedSyncResponse queued, ref bool recreateLayout, ref bool saveMarkers)
            {
                recreateLayout = true;
                saveMarkers = true;

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
        }
    }

    public enum SyncState
    {
        NotAttempted,
        NotNeeded,
        Started,
        Complete,
        Failed,
    }

    public enum SyncType
    {
        Upload,
        Download,
        MarkSeen,
    }
}
