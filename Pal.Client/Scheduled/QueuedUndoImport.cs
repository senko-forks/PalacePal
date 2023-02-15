using System;

namespace Pal.Client.Scheduled
{
    internal sealed class QueuedUndoImport : IQueueOnFrameworkThread
    {
        public QueuedUndoImport(Guid exportId)
        {
            ExportId = exportId;
        }

        public Guid ExportId { get; }
    }
}
