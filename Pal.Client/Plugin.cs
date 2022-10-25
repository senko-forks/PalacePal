using Dalamud.Game;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using ECommons;
using ECommons.Schedulers;
using ECommons.SplatoonAPI;
using ImGuiNET;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;

namespace Pal.Client
{
    public class Plugin : IDalamudPlugin
    {
        private const long ON_TERRITORY_CHANGE = -2;

        private readonly ConcurrentQueue<(ushort territoryId, bool success, IList<Marker> markers)> _remoteDownloads = new();
        private readonly static Dictionary<Marker.EType, MarkerConfig> _markerConfig = new Dictionary<Marker.EType, MarkerConfig>
        {
            { Marker.EType.Trap, new MarkerConfig { Radius = 1.7f } },
            { Marker.EType.Hoard, new MarkerConfig { Radius = 1.7f, OffsetY = -0.03f } },
            { Marker.EType.SilverCoffer, new MarkerConfig { Radius = 1f } },
        };
        private bool _configUpdated = false;

        internal ConcurrentDictionary<ushort, ConcurrentBag<Marker>> FloorMarkers { get; } = new();
        internal ConcurrentBag<Marker> EphemeralMarkers { get; set; } = new();
        internal ushort LastTerritory { get; private set; }
        public SyncState TerritorySyncState { get; set; }
        public string DebugMessage { get; set; }

        public string Name => "Palace Pal";

        public Plugin(DalamudPluginInterface pluginInterface)
        {

            ECommons.ECommons.Init(pluginInterface, this, Module.SplatoonAPI);

            pluginInterface.Create<Service>();
            Service.Plugin = this;
            Service.Configuration = (Configuration)pluginInterface.GetPluginConfig() ?? pluginInterface.Create<Configuration>();

            var agreementWindow = pluginInterface.Create<AgreementWindow>();
            if (agreementWindow is not null)
            {
                agreementWindow.IsOpen = Service.Configuration.FirstUse;
                Service.WindowSystem.AddWindow(agreementWindow);
            }

            var configWindow = pluginInterface.Create<ConfigWindow>();
            if (configWindow is not null)
            {
                Service.WindowSystem.AddWindow(configWindow);
            }

            pluginInterface.UiBuilder.Draw += Service.WindowSystem.Draw;
            pluginInterface.UiBuilder.OpenConfigUi += OnOpenConfigUi;
            Service.Framework.Update += OnFrameworkUpdate;
            Service.Configuration.Saved += OnConfigSaved;
            Service.CommandManager.AddHandler("/pal", new CommandInfo(OnCommand)
            {
                HelpMessage = "Open the configuration/debug window"
            });
        }

        public void OnOpenConfigUi()
        {
            Window configWindow;
            if (Service.Configuration.FirstUse)
                configWindow = Service.WindowSystem.GetWindow<AgreementWindow>();
            else
                configWindow = Service.WindowSystem.GetWindow<ConfigWindow>();

            if (configWindow != null)
                configWindow.IsOpen = true;
        }

        private void OnCommand(string command, string arguments)
        {
            if (Service.Configuration.FirstUse)
            {
                Service.Chat.PrintError("[Palace Pal] Please finish the first-time setup first.");
                return;
            }

            Service.WindowSystem.GetWindow<ConfigWindow>()?.Toggle();
        }

        #region IDisposable Support
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;

            Service.CommandManager.RemoveHandler("/pal");
            Service.PluginInterface.UiBuilder.Draw -= Service.WindowSystem.Draw;
            Service.PluginInterface.UiBuilder.OpenConfigUi -= OnOpenConfigUi;
            Service.Framework.Update -= OnFrameworkUpdate;
            Service.Configuration.Saved -= OnConfigSaved;

            Service.WindowSystem.RemoveAllWindows();

            Service.RemoteApi.Dispose();
            ECommons.ECommons.Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion

        private void OnConfigSaved()
        {
            _configUpdated = true;
        }

        private void OnFrameworkUpdate(Framework framework)
        {
            try
            {
                bool recreateLayout = false;
                if (_configUpdated)
                {
                    if (Service.Configuration.Mode == Configuration.EMode.Offline)
                    {
                        foreach (var path in Directory.GetFiles(Service.PluginInterface.GetPluginConfigDirectory()))
                        {
                            if (path.EndsWith(".json"))
                            {
                                var markers = JsonSerializer.Deserialize<List<Marker>>(File.ReadAllText(path), new JsonSerializerOptions { IncludeFields = true }).Where(x => x.Seen).ToList();
                                File.WriteAllText(path, JsonSerializer.Serialize(markers, new JsonSerializerOptions { IncludeFields = true }));
                            }
                        }

                        FloorMarkers.Clear();
                        EphemeralMarkers.Clear();
                        LastTerritory = 0;
                    }
                    _configUpdated = false;
                    recreateLayout = true;
                }
                
                bool saveMarkers = false;
                if (LastTerritory != Service.ClientState.TerritoryType)
                {
                    LastTerritory = Service.ClientState.TerritoryType;
                    TerritorySyncState = SyncState.NotAttempted;

                    if (IsInPotdOrHoh())
                        FloorMarkers[LastTerritory] = new ConcurrentBag<Marker>(LoadSavedMarkers());
                    EphemeralMarkers.Clear();
                    recreateLayout = true;
                    DebugMessage = null;
                }

                if (!IsInPotdOrHoh())
                    return;

                if (Service.Configuration.Mode == Configuration.EMode.Online && TerritorySyncState == SyncState.NotAttempted)
                {
                    TerritorySyncState = SyncState.Started;
                    Task.Run(async () => await DownloadMarkersForTerritory(LastTerritory));
                }

                if (_remoteDownloads.Count > 0)
                {
                    HandleRemoteDownloads();
                    recreateLayout = true;
                    saveMarkers = true;
                }

                if (!FloorMarkers.TryGetValue(LastTerritory, out var currentFloorMarkers))
                    FloorMarkers[LastTerritory] = currentFloorMarkers = new ConcurrentBag<Marker>();

                IList<Marker> visibleMarkers = GetRelevantGameObjects();
                HandlePersistentMarkers(currentFloorMarkers, visibleMarkers.Where(x => x.IsPermanent()).ToList(), saveMarkers, recreateLayout);
                HandleEphemeralMarkers(visibleMarkers.Where(x => !x.IsPermanent()).ToList(), recreateLayout);
            }
            catch (Exception e)
            {
                DebugMessage = $"{DateTime.Now}\n{e}";
            }
        }

        private void HandlePersistentMarkers(ConcurrentBag<Marker> currentFloorMarkers, IList<Marker> visibleMarkers, bool saveMarkers, bool recreateLayout)
        {

            foreach (var visibleMarker in visibleMarkers)
            {
                Marker knownMarker = currentFloorMarkers.SingleOrDefault(x => x == visibleMarker);
                if (knownMarker != null)
                {
                    if (!knownMarker.Seen)
                    {
                        knownMarker.Seen = true;
                        saveMarkers = true;
                    }
                    continue;
                }

                currentFloorMarkers.Add(visibleMarker);
                recreateLayout = true;
                saveMarkers = true;
            }

            if (saveMarkers)
            {
                SaveMarkers();

                if (TerritorySyncState == SyncState.Complete)
                {
                    var markersToUpload = currentFloorMarkers.Where(x => x.IsPermanent() && !x.RemoteSeen).ToList();
                    Task.Run(async () => await Service.RemoteApi.UploadMarker(LastTerritory, markersToUpload));
                }
            }

            if (recreateLayout)
            {
                Splatoon.RemoveDynamicElements("PalacePal.TrapHoard");

                var config = Service.Configuration;

                List<Element> elements = new List<Element>();
                foreach (var marker in currentFloorMarkers)
                {
                    if (marker.Seen || config.Mode == Configuration.EMode.Online)
                    {
                        if (marker.Type == Marker.EType.Trap && config.ShowTraps)
                        {
                            var element = CreateSplatoonElement(marker.Type, marker.Position, config.TrapColor);
                            marker.SplatoonElement = element;
                            elements.Add(element);
                        }
                        else if (marker.Type == Marker.EType.Hoard && config.ShowHoard)
                        {
                            var element = CreateSplatoonElement(marker.Type, marker.Position, config.HoardColor);
                            marker.SplatoonElement = element;
                            elements.Add(element);
                        }
                    }
                }

                if (elements.Count == 0)
                    return;

                // we need to delay this, as the current framework update could be before splatoon's, in which case it would immediately delete the layout
                new TickScheduler(delegate
                {
                    try
                    {
                        Splatoon.AddDynamicElements("PalacePal.TrapHoard", elements.ToArray(), new long[] { Environment.TickCount64 + 60 * 60 * 1000, ON_TERRITORY_CHANGE });
                    }
                    catch (Exception e)
                    {
                        DebugMessage = $"{DateTime.Now}\n{e}";
                    }
                });
            }
        }

        private void HandleEphemeralMarkers(IList<Marker> visibleMarkers, bool recreateLayout)
        {
            recreateLayout |= EphemeralMarkers.Any(existingMarker => !visibleMarkers.Any(x => x == existingMarker));
            recreateLayout |= visibleMarkers.Any(visibleMarker => !EphemeralMarkers.Any(x => x == visibleMarker));

            if (recreateLayout)
            {
                Splatoon.RemoveDynamicElements("PalacePal.RegularCoffers");
                EphemeralMarkers.Clear();

                var config = Service.Configuration;

                List<Element> elements = new List<Element>();
                foreach (var marker in visibleMarkers) 
                { 
                    EphemeralMarkers.Add(marker);

                    if (marker.Type == Marker.EType.SilverCoffer && config.ShowSilverCoffers)
                    {
                        var element = CreateSplatoonElement(marker.Type, marker.Position, config.SilverCofferColor, config.FillSilverCoffers);
                        marker.SplatoonElement = element;
                        elements.Add(element);
                    }
                }

                if (elements.Count == 0)
                    return;

                new TickScheduler(delegate
                {
                    try
                    {
                        Splatoon.AddDynamicElements("PalacePal.RegularCoffers", elements.ToArray(), new long[] { Environment.TickCount64 + 60 * 60 * 1000, ON_TERRITORY_CHANGE });
                    }
                    catch (Exception e)
                    {
                        DebugMessage = $"{DateTime.Now}\n{e}";
                    }
                });
            }
        }

        public string GetSaveForCurrentTerritory() => Path.Join(Service.PluginInterface.GetPluginConfigDirectory(), $"{LastTerritory}.json");

        private List<Marker> LoadSavedMarkers()
        {
            string path = GetSaveForCurrentTerritory();
            if (File.Exists(path))
                return JsonSerializer.Deserialize<List<Marker>>(File.ReadAllText(path), new JsonSerializerOptions { IncludeFields = true }).Where(x => x.Seen || Service.Configuration.Mode == Configuration.EMode.Online).ToList();
            else
                return new List<Marker>();
        }

        private void SaveMarkers()
        {
            string path = GetSaveForCurrentTerritory();
            File.WriteAllText(path, JsonSerializer.Serialize(FloorMarkers[LastTerritory], new JsonSerializerOptions { IncludeFields = true }));
        }

        private async Task DownloadMarkersForTerritory(ushort territoryId)
        {
            try
            {
                var (success, downloadedMarkers) = await Service.RemoteApi.DownloadRemoteMarkers(territoryId);
                _remoteDownloads.Enqueue((territoryId, success, downloadedMarkers));
            }
            catch (Exception e)
            {
                DebugMessage = $"{DateTime.Now}\n{e}";
            }
        }

        private void HandleRemoteDownloads()
        {
            while (_remoteDownloads.TryDequeue(out var download))
            {
                var (territoryId, success, downloadedMarkers) = download;
                if (Service.Configuration.Mode == Configuration.EMode.Online && success && FloorMarkers.TryGetValue(territoryId, out var currentFloorMarkers) && downloadedMarkers.Count > 0)
                {
                    foreach (var downloadedMarker in downloadedMarkers)
                    {
                        Marker seenMarker = currentFloorMarkers.SingleOrDefault(x => x == downloadedMarker);
                        if (seenMarker != null)
                        {
                            seenMarker.RemoteSeen = true;
                            continue;
                        }

                        currentFloorMarkers.Add(downloadedMarker);
                    }
                }

                // don't modify state for outdated floors
                if (LastTerritory != territoryId) 
                    continue;

                if (success)
                    TerritorySyncState = SyncState.Complete;
                else
                    TerritorySyncState = SyncState.Failed;
            }
        }

        private IList<Marker> GetRelevantGameObjects()
        {
            List<Marker> result = new();
            for (int i = 246; i < Service.ObjectTable.Length; i++)
            {
                GameObject obj = Service.ObjectTable[i];
                if (obj == null)
                    continue;

                switch ((uint)Marshal.ReadInt32(obj.Address + 128))
                {
                    case 2007182:
                    case 2007183:
                    case 2007184:
                    case 2007185:
                    case 2007186:
                    case 2009504:
                        result.Add(new Marker(Marker.EType.Trap, obj.Position) { Seen = true });
                        break;
                    
                    case 2007542:
                    case 2007543:
                        result.Add(new Marker(Marker.EType.Hoard, obj.Position) { Seen = true });
                        break;

                    case 2007357:
                        result.Add(new Marker(Marker.EType.SilverCoffer, obj.Position) { Seen = true });
                        break;
                }
            }

            return result;
        }

        internal bool IsInPotdOrHoh() => Service.ClientState.IsLoggedIn && Service.Condition[ConditionFlag.InDeepDungeon];

        internal static Element CreateSplatoonElement(Marker.EType type, Vector3 pos, Vector4 color, bool fill = false)
        {
            return new Element(ElementType.CircleAtFixedCoordinates)
            {
                refX = pos.X,
                refY = pos.Z, // z and y are swapped
                refZ = pos.Y,
                offX = 0,
                offY = 0,
                offZ = _markerConfig[type].OffsetY,
                Filled = fill,
                radius = _markerConfig[type].Radius,
                FillStep = 1,
                color = ImGui.ColorConvertFloat4ToU32(color),
                thicc = 2,
            };
        }

        public enum SyncState
        {
            NotAttempted,
            Started,
            Complete,
            Failed,
        }

        private class MarkerConfig
        {
            public float OffsetY { get; set; } = 0;
            public float Radius { get; set; } = 0.25f;
        }
    }
}
