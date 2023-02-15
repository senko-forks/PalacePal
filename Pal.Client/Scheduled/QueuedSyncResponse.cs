using System.Collections.Generic;

namespace Pal.Client.Scheduled
{
    internal sealed class QueuedSyncResponse : IQueueOnFrameworkThread
    {
        public required SyncType Type { get; init; }
        public required ushort TerritoryType { get; init; }
        public required bool Success { get; init; }
        public required List<Marker> Markers { get; init; }
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
