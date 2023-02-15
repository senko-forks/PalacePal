using Account;
using Pal.Common;
using System;
using System.IO;
using System.Linq;
using System.Numerics;
using Dalamud.Game.Gui;
using Pal.Client.Properties;

namespace Pal.Client.Scheduled
{
    internal sealed class QueuedImport : IQueueOnFrameworkThread
    {
        public ExportRoot Export { get; }
        public Guid ExportId { get; private set; }
        public int ImportedTraps { get; private set; }
        public int ImportedHoardCoffers { get; private set; }

        public QueuedImport(string sourcePath)
        {
            using var input = File.OpenRead(sourcePath);
            Export = ExportRoot.Parser.ParseFrom(input);
        }

        public bool Validate(ChatGui chatGui)
        {
            if (Export.ExportVersion != ExportConfig.ExportVersion)
            {
                chatGui.PrintError(Localization.Error_ImportFailed_IncompatibleVersion);
                return false;
            }

            if (!Guid.TryParse(Export.ExportId, out Guid exportId) || ExportId == Guid.Empty)
            {
                chatGui.PrintError(Localization.Error_ImportFailed_InvalidFile);
                return false;
            }

            ExportId = exportId;

            if (string.IsNullOrEmpty(Export.ServerUrl))
            {
                // If we allow for backups as import/export, this should be removed
                chatGui.PrintError(Localization.Error_ImportFailed_InvalidFile);
                return false;
            }

            return true;
        }

        public void ImportFloor(ExportFloor remoteFloor, LocalState localState)
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
                        ImportedTraps++;
                    else if (localMarker.Type == Marker.EType.Hoard)
                        ImportedHoardCoffers++;
                }

                remoteMarker.Imports.Add(ExportId);
            }
        }
    }
}
