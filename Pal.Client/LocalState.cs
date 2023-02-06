using Pal.Common;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Pal.Client
{
    /// <summary>
    /// JSON for a single floor set (e.g. 51-60).
    /// </summary>
    internal class LocalState
    {
        private static readonly JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions { IncludeFields = true };
        private static readonly int _currentVersion = 4;

        public uint TerritoryType { get; set; }
        public ConcurrentBag<Marker> Markers { get; set; } = new();

        public LocalState(uint territoryType)
        {
            TerritoryType = territoryType;
        }

        private void ApplyFilters()
        {
            if (Service.Configuration.Mode == Configuration.EMode.Offline)
                Markers = new ConcurrentBag<Marker>(Markers.Where(x => x.Seen || (x.WasImported && x.Imports.Count > 0)));
            else
                // ensure old import markers are removed if they are no longer part of a "current" import
                // this MAY remove markers the server sent you (and that you haven't seen), but this should be fixed the next time you enter the zone
                Markers = new ConcurrentBag<Marker>(Markers.Where(x => x.Seen || !x.WasImported || x.Imports.Count > 0));
        }

        public static LocalState? Load(uint territoryType)
        {
            string path = GetSaveLocation(territoryType);
            if (!File.Exists(path))
                return null;

            string content = File.ReadAllText(path);
            if (content.Length == 0)
                return null;

            LocalState localState;
            int version = 1;
            if (content[0] == '[')
            {
                // v1 only had a list of markers, not a JSON object as root
                localState = new LocalState(territoryType)
                {
                    Markers = new ConcurrentBag<Marker>(JsonSerializer.Deserialize<HashSet<Marker>>(content, _jsonSerializerOptions) ?? new()),
                };
            }
            else
            {
                var save = JsonSerializer.Deserialize<SaveFile>(content, _jsonSerializerOptions);
                if (save == null)
                    return null;

                localState = new LocalState(territoryType)
                {
                    Markers = new ConcurrentBag<Marker>(save.Markers.Where(o => o.Type != Marker.EType.Debug)),
                };
                version = save.Version;
            }

            localState.ApplyFilters();

            if (version <= 3)
            {
                foreach (var marker in localState.Markers)
                    marker.RemoteSeenOn = marker.RemoteSeenOn.Select(x => x.PadRight(14).Substring(0, 13)).ToList();
            }

            if (version < _currentVersion)
                localState.Save();

            return localState;
        }

        public void Save()
        {
            string path = GetSaveLocation(TerritoryType);

            ApplyFilters();
            SaveImpl(path);
        }

        public void Backup(string suffix)
        {
            string path = $"{GetSaveLocation(TerritoryType)}.{suffix}";
            if (!File.Exists(path))
            {
                SaveImpl(path);
            }
        }

        private void SaveImpl(string path)
        {
            foreach (var marker in Markers)
            {
                if (string.IsNullOrEmpty(marker.SinceVersion))
                    marker.SinceVersion = typeof(Plugin).Assembly.GetName().Version!.ToString(2);
            }

            if (Markers.Count == 0)
                File.Delete(path);
            else
            {
                File.WriteAllText(path, JsonSerializer.Serialize(new SaveFile
                {
                    Version = _currentVersion,
                    Markers = new HashSet<Marker>(Markers)
                }, _jsonSerializerOptions));
            }
        }

        public string GetSaveLocation() => GetSaveLocation(TerritoryType);

        private static string GetSaveLocation(uint territoryType) => Path.Join(Service.PluginInterface.GetPluginConfigDirectory(), $"{territoryType}.json");

        public static void ForEach(Action<LocalState> action)
        {
            foreach (ETerritoryType territory in typeof(ETerritoryType).GetEnumValues())
            {
                LocalState? localState = Load((ushort)territory);
                if (localState != null)
                    action(localState);
            }
        }

        public static void UpdateAll()
        {
            ForEach(s => s.Save());
        }

        public void UndoImport(List<Guid> importIds)
        {
            // When saving a floor state, any markers not seen, not remote seen, and not having an import id are removed;
            // so it is possible to remove "wrong" markers by not having them be in the current import.
            foreach (var marker in Markers)
                marker.Imports.RemoveAll(id => importIds.Contains(id));
        }

        public class SaveFile
        {
            public int Version { get; set; }
            public HashSet<Marker> Markers { get; set; } = new();
        }
    }
}
