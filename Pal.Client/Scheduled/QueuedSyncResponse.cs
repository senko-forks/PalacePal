using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Pal.Client.Extensions;
using Pal.Client.Net;
using static Pal.Client.Plugin;

namespace Pal.Client.Scheduled
{
    internal class QueuedSyncResponse : IQueueOnFrameworkThread
    {
        public required SyncType Type { get; init; }
        public required ushort TerritoryType { get; init; }
        public required bool Success { get; init; }
        public required List<Marker> Markers { get; init; }

        public void Run(Plugin plugin, ref bool recreateLayout, ref bool saveMarkers)
        {
            recreateLayout = true;
            saveMarkers = true;

            try
            {
                var remoteMarkers = Markers;
                var currentFloor = plugin.GetFloorMarkers(TerritoryType);
                if (Service.Configuration.Mode == Configuration.EMode.Online && Success && remoteMarkers.Count > 0)
                {
                    switch (Type)
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

                                if (Type == SyncType.Download)
                                    currentFloor.Markers.Add(remoteMarker);
                            }
                            break;

                        case SyncType.MarkSeen:
                            var partialAccountId = Service.Configuration.FindAccount(RemoteApi.RemoteUrl)?.AccountId.ToPartialId();
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
                if (plugin.LastTerritory != TerritoryType)
                    return;

                if (Type == SyncType.Download)
                {
                    if (Success)
                        plugin.TerritorySyncState = SyncState.Complete;
                    else
                        plugin.TerritorySyncState = SyncState.Failed;
                }
            }
            catch (Exception e)
            {
                plugin.DebugMessage = $"{DateTime.Now}\n{e}";
                if (Type == SyncType.Download)
                    plugin.TerritorySyncState = SyncState.Failed;
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
