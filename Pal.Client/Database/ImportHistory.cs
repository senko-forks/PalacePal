using System;

namespace Pal.Client.Database
{
    internal sealed class ImportHistory
    {
        public Guid Id { get; set; }
        public string? RemoteUrl { get; set; }
        public DateTime ExportedAt { get; set; }
        public DateTime ImportedAt { get; set; }
    }
}
