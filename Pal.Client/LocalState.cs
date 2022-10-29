using Pal.Common;
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
        private static readonly int _currentVersion = 2;
     
        public uint TerritoryType { get; set; }
        public ConcurrentBag<Marker> Markers { get; set; } = new();

        public LocalState(uint territoryType)
        {
            TerritoryType = territoryType;
        }

        private void ApplyFilters()
        {
            if (Service.Configuration.Mode == Configuration.EMode.Offline)
                Markers = new ConcurrentBag<Marker>(Markers.Where(x => x.Seen));
        }

        public static LocalState Load(uint territoryType)
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
                    Markers = new ConcurrentBag<Marker>(JsonSerializer.Deserialize<HashSet<Marker>>(content, _jsonSerializerOptions)),
                };
            }
            else
            {
                var save = JsonSerializer.Deserialize<SaveFile>(content, _jsonSerializerOptions);
                localState = new LocalState(territoryType)
                {
                    Markers = new ConcurrentBag<Marker>(save.Markers),
                };
                version = save.Version;
            }

            localState.ApplyFilters();

            if (version < _currentVersion)
                localState.Save();

            return localState;
        }

        public void Save()
        {
            string path = GetSaveLocation(TerritoryType);

            ApplyFilters();
            File.WriteAllText(path, JsonSerializer.Serialize(new SaveFile
            {
                Version = _currentVersion,
                Markers = new HashSet<Marker>(Markers)
            }, _jsonSerializerOptions));
        }

        private static string GetSaveLocation(uint territoryType) => Path.Join(Service.PluginInterface.GetPluginConfigDirectory(), $"{territoryType}.json");

        public static void UpdateAll()
        {
            foreach (ETerritoryType territory in typeof(ETerritoryType).GetEnumValues())
            {
                LocalState localState = Load((ushort)territory);
                if (localState != null)
                    localState.Save();
            }
        }

        public class SaveFile
        {
            public int Version { get; set; }
            public HashSet<Marker> Markers { get; set; } = new();
        }
    }
}
