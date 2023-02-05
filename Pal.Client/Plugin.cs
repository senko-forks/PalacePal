using Dalamud.Game;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using Dalamud.Plugin;
using ECommons;
using ECommons.Schedulers;
using ECommons.SplatoonAPI;
using Grpc.Core;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using Pal.Client.Net;
using Pal.Client.Scheduled;
using Pal.Client.Windows;
using Pal.Common;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Pal.Client
{
    public class Plugin : IDalamudPlugin
    {
        private const long ON_TERRITORY_CHANGE = -2;
        private const uint COLOR_INVISIBLE = 0;
        private const string SPLATOON_TRAP_HOARD = "PalacePal.TrapHoard";
        private const string SPLATOON_REGULAR_COFFERS = "PalacePal.RegularCoffers";

        private readonly static Dictionary<Marker.EType, MarkerConfig> _markerConfig = new Dictionary<Marker.EType, MarkerConfig>
        {
            { Marker.EType.Trap, new MarkerConfig { Radius = 1.7f } },
            { Marker.EType.Hoard, new MarkerConfig { Radius = 1.7f, OffsetY = -0.03f } },
            { Marker.EType.SilverCoffer, new MarkerConfig { Radius = 1f } },
        };
        private LocalizedChatMessages _localizedChatMessages = new();

        internal ConcurrentDictionary<ushort, LocalState> FloorMarkers { get; } = new();
        internal ConcurrentBag<Marker> EphemeralMarkers { get; set; } = new();
        internal ushort LastTerritory { get; set; }
        public SyncState TerritorySyncState { get; set; }
        public PomanderState PomanderOfSight { get; set; } = PomanderState.Inactive;
        public PomanderState PomanderOfIntuition { get; set; } = PomanderState.Inactive;
        public string? DebugMessage { get; set; }
        internal Queue<IQueueOnFrameworkThread> EarlyEventQueue { get; } = new();
        internal Queue<IQueueOnFrameworkThread> LateEventQueue { get; } = new();

        public string Name => "Palace Pal";

        public Plugin(DalamudPluginInterface pluginInterface, ChatGui chat)
        {
            PluginLog.Information($"Install source: {pluginInterface.SourceRepository}");

#if RELEASE
            // You're welcome to remove this code in your fork, as long as:
            // - none of the links accessible within FFXIV open the original repo (e.g. in the plugin installer), and
            // - you host your own server instance
            if (!pluginInterface.IsDev
                && !pluginInterface.SourceRepository.StartsWith("https://raw.githubusercontent.com/carvelli/") 
                && !pluginInterface.SourceRepository.StartsWith("https://github.com/carvelli/"))
            {
                chat.PrintError("[Palace Pal] Please install this plugin from the official repository at https://github.com/carvelli/Dalamud-Plugins to continue using it.");
                throw new InvalidOperationException();
            }
#endif

            ECommonsMain.Init(pluginInterface, this, Module.SplatoonAPI);

            pluginInterface.Create<Service>();
            Service.Plugin = this;
            Service.Configuration = (Configuration?)pluginInterface.GetPluginConfig() ?? pluginInterface.Create<Configuration>()!;
            Service.Configuration.Migrate();

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

            var statisticsWindow = pluginInterface.Create<StatisticsWindow>();
            if (statisticsWindow is not null)
            {
                Service.WindowSystem.AddWindow(statisticsWindow);
            }

            pluginInterface.UiBuilder.Draw += Service.WindowSystem.Draw;
            pluginInterface.UiBuilder.OpenConfigUi += OnOpenConfigUi;
            Service.Framework.Update += OnFrameworkUpdate;
            Service.Chat.ChatMessage += OnChatMessage;
            Service.CommandManager.AddHandler("/pal", new CommandInfo(OnCommand)
            {
                HelpMessage = "Open the configuration/debug window"
            });

            ReloadLanguageStrings();
        }

        public void OnOpenConfigUi()
        {
            Window? configWindow;
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

            try
            {
                arguments = arguments.Trim();
                switch (arguments)
                {
                    case "stats":
                        Task.Run(async () => await FetchFloorStatistics());
                        break;

                    case "test-connection":
                    case "tc":
                        var configWindow = Service.WindowSystem.GetWindow<ConfigWindow>();
                        if (configWindow == null)
                            return;

                        configWindow.IsOpen = true;
                        configWindow.TestConnection();
                        break;

#if DEBUG
                    case "update-saves":
                        LocalState.UpdateAll();
                        Service.Chat.Print("Updated all locally cached marker files to latest version.");
                        break;
#endif

                    case "":
                    case "config":
                        Service.WindowSystem.GetWindow<ConfigWindow>()?.Toggle();
                        break;

                    case "near":
                        DebugNearest(m => true);
                        break;

                    case "tnear":
                        DebugNearest(m => m.Type == Marker.EType.Trap);
                        break;

                    case "hnear":
                        DebugNearest(m => m.Type == Marker.EType.Hoard);
                        break;

                    default:
                        Service.Chat.PrintError($"[Palace Pal] Unknown sub-command '{arguments}' for '{command}'.");
                        break;
                }
            }
            catch (Exception e)
            {
                Service.Chat.PrintError($"[Palace Pal] {e}");
            }
        }

        #region IDisposable Support
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;

            Service.CommandManager.RemoveHandler("/pal");
            Service.PluginInterface.UiBuilder.Draw -= Service.WindowSystem.Draw;
            Service.PluginInterface.UiBuilder.OpenConfigUi -= OnOpenConfigUi;
            Service.Framework.Update -= OnFrameworkUpdate;
            Service.Chat.ChatMessage -= OnChatMessage;

            Service.WindowSystem.RemoveAllWindows();

            Service.RemoteApi.Dispose();

            try
            {
                Splatoon.RemoveDynamicElements(SPLATOON_TRAP_HOARD);
                Splatoon.RemoveDynamicElements(SPLATOON_REGULAR_COFFERS);
            }
            catch
            {
                // destroyed on territory change either way
            }
            ECommonsMain.Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion

        private void OnChatMessage(XivChatType type, uint senderId, ref SeString sender, ref SeString seMessage, ref bool isHandled)
        {
            if (Service.Configuration.FirstUse)
                return;

            if (type != (XivChatType)2105)
                return;

            string message = seMessage.ToString();
            if (_localizedChatMessages.FloorChanged.IsMatch(message))
            {
                PomanderOfSight = PomanderState.Inactive;

                if (PomanderOfIntuition == PomanderState.FoundOnCurrentFloor)
                    PomanderOfIntuition = PomanderState.Inactive;
            }
            else if (message.EndsWith(_localizedChatMessages.MapRevealed))
            {
                PomanderOfSight = PomanderState.Active;
            }
            else if (message.EndsWith(_localizedChatMessages.AllTrapsRemoved))
            {
                PomanderOfSight = PomanderState.PomanderOfSafetyUsed;
            }
            else if (message.EndsWith(_localizedChatMessages.HoardNotOnCurrentFloor) || message.EndsWith(_localizedChatMessages.HoardOnCurrentFloor))
            {
                // There is no functional difference between these - if you don't open the marked coffer,
                // going to higher floors will keep the pomander active.
                PomanderOfIntuition = PomanderState.Active;
            }
            else if (message.EndsWith(_localizedChatMessages.HoardCofferOpened))
            {
                PomanderOfIntuition = PomanderState.FoundOnCurrentFloor;
            }
            else
                return;
        }

        private void OnFrameworkUpdate(Framework framework)
        {
            if (Service.Configuration.FirstUse)
                return;

            try
            {
                bool recreateLayout = false;
                bool saveMarkers = false;

                while (EarlyEventQueue.TryDequeue(out IQueueOnFrameworkThread? queued))
                    queued?.Run(this, ref recreateLayout, ref saveMarkers);

                if (LastTerritory != Service.ClientState.TerritoryType)
                {
                    LastTerritory = Service.ClientState.TerritoryType;
                    TerritorySyncState = SyncState.NotAttempted;

                    if (IsInDeepDungeon())
                        GetFloorMarkers(LastTerritory);
                    EphemeralMarkers.Clear();
                    PomanderOfSight = PomanderState.Inactive;
                    PomanderOfIntuition = PomanderState.Inactive;
                    recreateLayout = true;
                    DebugMessage = null;
                }

                if (!IsInDeepDungeon())
                    return;

                if (Service.Configuration.Mode == Configuration.EMode.Online && TerritorySyncState == SyncState.NotAttempted)
                {
                    TerritorySyncState = SyncState.Started;
                    Task.Run(async () => await DownloadMarkersForTerritory(LastTerritory));
                }

                while (LateEventQueue.TryDequeue(out IQueueOnFrameworkThread? queued))
                    queued?.Run(this, ref recreateLayout, ref saveMarkers);

                var currentFloor = GetFloorMarkers(LastTerritory);

                IList<Marker> visibleMarkers = GetRelevantGameObjects();
                HandlePersistentMarkers(currentFloor, visibleMarkers.Where(x => x.IsPermanent()).ToList(), saveMarkers, recreateLayout);
                HandleEphemeralMarkers(visibleMarkers.Where(x => !x.IsPermanent()).ToList(), recreateLayout);
            }
            catch (Exception e)
            {
                DebugMessage = $"{DateTime.Now}\n{e}";
            }
        }

        internal LocalState GetFloorMarkers(ushort territoryType)
        {
            return FloorMarkers.GetOrAdd(territoryType, tt => LocalState.Load(tt) ?? new LocalState(tt));
        }

        private void HandlePersistentMarkers(LocalState currentFloor, IList<Marker> visibleMarkers, bool saveMarkers, bool recreateLayout)
        {
            var config = Service.Configuration;
            var currentFloorMarkers = currentFloor.Markers;

            bool updateSeenMarkers = false;
            var accountId = Service.RemoteApi.AccountId;
            foreach (var visibleMarker in visibleMarkers)
            {
                Marker? knownMarker = currentFloorMarkers.SingleOrDefault(x => x == visibleMarker);
                if (knownMarker != null)
                {
                    if (!knownMarker.Seen)
                    {
                        knownMarker.Seen = true;
                        saveMarkers = true;
                    }

                    // This requires you to have seen a trap/hoard marker once per floor to synchronize this for older local states,
                    // markers discovered afterwards are automatically marked seen.
                    if (accountId != null && knownMarker.NetworkId != null && !knownMarker.RemoteSeenRequested && !knownMarker.RemoteSeenOn.Contains(accountId.Value))
                        updateSeenMarkers = true;

                    continue;
                }

                currentFloorMarkers.Add(visibleMarker);
                recreateLayout = true;
                saveMarkers = true;
            }

            if (!recreateLayout && currentFloorMarkers.Count > 0 && (config.OnlyVisibleTrapsAfterPomander || config.OnlyVisibleHoardAfterPomander))
            {

                try
                {
                    foreach (var marker in currentFloorMarkers)
                    {
                        uint desiredColor = DetermineColor(marker, visibleMarkers);
                        if (marker.SplatoonElement == null || !marker.SplatoonElement.IsValid())
                        {
                            recreateLayout = true;
                            break;
                        }

                        if (marker.SplatoonElement.color != desiredColor)
                            marker.SplatoonElement.color = desiredColor;
                    }
                }
                catch (Exception e)
                {
                    DebugMessage = $"{DateTime.Now}\n{e}";
                    recreateLayout = true;
                }
            }

            if (updateSeenMarkers && accountId != null)
            {
                var markersToUpdate = currentFloorMarkers.Where(x => x.Seen && x.NetworkId != null && !x.RemoteSeenRequested && !x.RemoteSeenOn.Contains(accountId.Value)).ToList();
                foreach (var marker in markersToUpdate)
                    marker.RemoteSeenRequested = true;
                Task.Run(async () => await SyncSeenMarkersForTerritory(LastTerritory, markersToUpdate));
            }

            if (saveMarkers)
            {
                currentFloor.Save();

                if (TerritorySyncState == SyncState.Complete)
                {
                    var markersToUpload = currentFloorMarkers.Where(x => x.IsPermanent() && x.NetworkId == null && !x.UploadRequested).ToList();
                    if (markersToUpload.Count > 0)
                    {
                        foreach (var marker in markersToUpload)
                            marker.UploadRequested = true;
                        Task.Run(async () => await UploadMarkersForTerritory(LastTerritory, markersToUpload));
                    }
                }
            }

            if (recreateLayout)
            {
                Splatoon.RemoveDynamicElements(SPLATOON_TRAP_HOARD);

                List<Element> elements = new List<Element>();
                foreach (var marker in currentFloorMarkers)
                {
                    if (marker.Seen || config.Mode == Configuration.EMode.Online || (marker.WasImported && marker.Imports.Count > 0))
                    {
                        if (marker.Type == Marker.EType.Trap && config.ShowTraps)
                        {
                            var element = CreateSplatoonElement(marker.Type, marker.Position, DetermineColor(marker, visibleMarkers));
                            marker.SplatoonElement = element;
                            elements.Add(element);
                        }
                        else if (marker.Type == Marker.EType.Hoard && config.ShowHoard)
                        {
                            var element = CreateSplatoonElement(marker.Type, marker.Position, DetermineColor(marker, visibleMarkers));
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
                        Splatoon.AddDynamicElements(SPLATOON_TRAP_HOARD, elements.ToArray(), new long[] { Environment.TickCount64 + 60 * 60 * 1000, ON_TERRITORY_CHANGE });
                    }
                    catch (Exception e)
                    {
                        DebugMessage = $"{DateTime.Now}\n{e}";
                    }
                });
            }
        }

        private uint DetermineColor(Marker marker, IList<Marker> visibleMarkers)
        {
            if (marker.Type == Marker.EType.Trap)
            {
                if (PomanderOfSight == PomanderState.Inactive || !Service.Configuration.OnlyVisibleTrapsAfterPomander || visibleMarkers.Any(x => x == marker))
                    return ImGui.ColorConvertFloat4ToU32(Service.Configuration.TrapColor);
                else
                    return COLOR_INVISIBLE;
            }
            else
            {
                if (PomanderOfIntuition == PomanderState.Inactive || !Service.Configuration.OnlyVisibleHoardAfterPomander || visibleMarkers.Any(x => x == marker))
                    return ImGui.ColorConvertFloat4ToU32(Service.Configuration.HoardColor);
                else
                    return COLOR_INVISIBLE;
            }
        }

        private void HandleEphemeralMarkers(IList<Marker> visibleMarkers, bool recreateLayout)
        {
            recreateLayout |= EphemeralMarkers.Any(existingMarker => !visibleMarkers.Any(x => x == existingMarker));
            recreateLayout |= visibleMarkers.Any(visibleMarker => !EphemeralMarkers.Any(x => x == visibleMarker));

            if (recreateLayout)
            {
                Splatoon.RemoveDynamicElements(SPLATOON_REGULAR_COFFERS);
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
                        Splatoon.AddDynamicElements(SPLATOON_REGULAR_COFFERS, elements.ToArray(), new long[] { Environment.TickCount64 + 60 * 60 * 1000, ON_TERRITORY_CHANGE });
                    }
                    catch (Exception e)
                    {
                        DebugMessage = $"{DateTime.Now}\n{e}";
                    }
                });
            }
        }

        #region Up-/Download
        private async Task DownloadMarkersForTerritory(ushort territoryId)
        {
            try
            {
                var (success, downloadedMarkers) = await Service.RemoteApi.DownloadRemoteMarkers(territoryId);
                LateEventQueue.Enqueue(new QueuedSyncResponse
                {
                    Type = SyncType.Download,
                    TerritoryType = territoryId,
                    Success = success,
                    Markers = downloadedMarkers
                });
            }
            catch (Exception e)
            {
                DebugMessage = $"{DateTime.Now}\n{e}";
            }
        }

        private async Task UploadMarkersForTerritory(ushort territoryId, List<Marker> markersToUpload)
        {
            try
            {
                var (success, uploadedMarkers) = await Service.RemoteApi.UploadMarker(territoryId, markersToUpload);
                LateEventQueue.Enqueue(new QueuedSyncResponse
                {
                    Type = SyncType.Upload,
                    TerritoryType = territoryId,
                    Success = success,
                    Markers = uploadedMarkers
                });
            }
            catch (Exception e)
            {
                DebugMessage = $"{DateTime.Now}\n{e}";
            }
        }

        private async Task SyncSeenMarkersForTerritory(ushort territoryId, List<Marker> markersToUpdate)
        {
            try
            {
                var success = await Service.RemoteApi.MarkAsSeen(territoryId, markersToUpdate);
                LateEventQueue.Enqueue(new QueuedSyncResponse
                {
                    Type = SyncType.MarkSeen,
                    TerritoryType = territoryId,
                    Success = success,
                    Markers = markersToUpdate,
                });
            }
            catch (Exception e)
            {
                DebugMessage = $"{DateTime.Now}\n{e}";
            }
        }
        #endregion

        #region Command Handling
        private async Task FetchFloorStatistics()
        {
            if (!Service.RemoteApi.HasRoleOnCurrentServer("statistics:view"))
            {
                Service.Chat.Print("[Palace Pal] You can view statistics for the floor you're currently on by opening the 'Debug' tab in the configuration window.");
                return;
            }

            try
            {
                var (success, floorStatistics) = await Service.RemoteApi.FetchStatistics();
                if (success)
                {
                    var statisticsWindow = Service.WindowSystem.GetWindow<StatisticsWindow>()!;
                    statisticsWindow.SetFloorData(floorStatistics);
                    statisticsWindow.IsOpen = true;
                }
                else
                {
                    Service.Chat.PrintError("[Palace Pal] Unable to fetch statistics.");
                }
            }
            catch (RpcException e) when (e.StatusCode == StatusCode.PermissionDenied)
            {
                Service.Chat.Print("[Palace Pal] You can view statistics for the floor you're currently on by opening the 'Debug' tab in the configuration window.");
            }
            catch (Exception e)
            {
                Service.Chat.PrintError($"[Palace Pal] {e}");
            }
        }

        private void DebugNearest(Predicate<Marker> predicate)
        {
            if (!IsInDeepDungeon())
                return;

            var state = GetFloorMarkers(Service.ClientState.TerritoryType);
            var playerPosition = Service.ClientState.LocalPlayer?.Position;
            if (playerPosition == null)
                return;
            Service.Chat.Print($"[Pal] {playerPosition}");

            var nearbyMarkers = state.Markers
                .Where(m => predicate(m))
                .Where(m => m.SplatoonElement != null && m.SplatoonElement.color != COLOR_INVISIBLE)
                .Select(m => new { m = m, distance = (playerPosition - m.Position)?.Length() ?? float.MaxValue })
                .OrderBy(m => m.distance)
                .Take(5)
                .ToList();
            foreach (var nearbyMarker in nearbyMarkers)
                Service.Chat.Print($"{nearbyMarker.distance:F2} - {nearbyMarker.m.Type} {nearbyMarker.m.NetworkId?.ToString()?.Substring(0, 8)} - {nearbyMarker.m.Position}");
        }
        #endregion

        private IList<Marker> GetRelevantGameObjects()
        {
            List<Marker> result = new();
            for (int i = 246; i < Service.ObjectTable.Length; i++)
            {
                GameObject? obj = Service.ObjectTable[i];
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

        internal bool IsInDeepDungeon() =>
            Service.ClientState.IsLoggedIn
            && Service.Condition[ConditionFlag.InDeepDungeon]
            && typeof(ETerritoryType).IsEnumDefined(Service.ClientState.TerritoryType);

        internal static Element CreateSplatoonElement(Marker.EType type, Vector3 pos, Vector4 color, bool fill = false)
            => CreateSplatoonElement(type, pos, ImGui.ColorConvertFloat4ToU32(color), fill);

        internal static Element CreateSplatoonElement(Marker.EType type, Vector3 pos, uint color, bool fill = false)
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
                color = color,
                thicc = 2,
            };
        }

        private void ReloadLanguageStrings()
        {
            _localizedChatMessages = new LocalizedChatMessages
            {
                MapRevealed = GetLocalizedString(7256),
                AllTrapsRemoved = GetLocalizedString(7255),
                HoardOnCurrentFloor = GetLocalizedString(7272),
                HoardNotOnCurrentFloor = GetLocalizedString(7273),
                HoardCofferOpened = GetLocalizedString(7274),
                FloorChanged = new Regex("^" + GetLocalizedString(7270).Replace("\u0002 \u0003\ufffd\u0002\u0003", @"(\d+)") + "$"),
            };
        }

        private string GetLocalizedString(uint id)
        {
            return Service.DataManager.GetExcelSheet<LogMessage>()?.GetRow(id)?.Text?.ToString() ?? "Unknown";
        }

        public enum PomanderState
        {
            Inactive,
            Active,
            FoundOnCurrentFloor,
            PomanderOfSafetyUsed,
        }

        private class MarkerConfig
        {
            public float OffsetY { get; set; } = 0;
            public float Radius { get; set; } = 0.25f;
        }

        private class LocalizedChatMessages
        {
            public string MapRevealed { get; set; } = "???"; //"The map for this floor has been revealed!";
            public string AllTrapsRemoved { get; set; } = "???"; // "All the traps on this floor have disappeared!";
            public string HoardOnCurrentFloor { get; set; } = "???"; // "You sense the Accursed Hoard calling you...";
            public string HoardNotOnCurrentFloor { get; set; } = "???"; // "You do not sense the call of the Accursed Hoard on this floor...";
            public string HoardCofferOpened { get; set; } = "???"; // "You discover a piece of the Accursed Hoard!";
            public Regex FloorChanged { get; set; } = new Regex(@"This isn't a game message, but will be replaced"); // new Regex(@"^Floor (\d+)$");
        }
    }
}
