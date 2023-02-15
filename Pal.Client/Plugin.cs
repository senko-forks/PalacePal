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
using Grpc.Core;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using Pal.Client.Rendering;
using Pal.Client.Scheduled;
using Pal.Client.Windows;
using Pal.Common;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Pal.Client.Extensions;
using Pal.Client.Properties;
using ECommons;
using ECommons.Schedulers;
using Pal.Client.Configuration;
using Pal.Client.Net;

namespace Pal.Client
{
    public class Plugin : IDalamudPlugin
    {
        internal const uint ColorInvisible = 0;

        private LocalizedChatMessages _localizedChatMessages = new();

        internal ConcurrentDictionary<ushort, LocalState> FloorMarkers { get; } = new();
        internal ConcurrentBag<Marker> EphemeralMarkers { get; set; } = new();
        internal ushort LastTerritory { get; set; }
        internal SyncState TerritorySyncState { get; set; }
        internal PomanderState PomanderOfSight { get; private set; } = PomanderState.Inactive;
        internal PomanderState PomanderOfIntuition { get; private set; } = PomanderState.Inactive;
        internal string? DebugMessage { get; set; }
        internal Queue<IQueueOnFrameworkThread> EarlyEventQueue { get; } = new();
        internal Queue<IQueueOnFrameworkThread> LateEventQueue { get; } = new();
        internal ConcurrentQueue<nint> NextUpdateObjects { get; } = new();
        internal IRenderer Renderer { get; private set; } = null!;

        public string Name => Localization.Palace_Pal;

        public Plugin(DalamudPluginInterface pluginInterface, ChatGui chat)
        {
            LanguageChanged(pluginInterface.UiLanguage);

            PluginLog.Information($"Install source: {pluginInterface.SourceRepository}");

#if RELEASE
            // You're welcome to remove this code in your fork, as long as:
            // - none of the links accessible within FFXIV open the original repo (e.g. in the plugin installer), and
            // - you host your own server instance
            if (!pluginInterface.IsDev
                && !pluginInterface.SourceRepository.StartsWith("https://raw.githubusercontent.com/carvelli/") 
                && !pluginInterface.SourceRepository.StartsWith("https://github.com/carvelli/"))
            {
                chat.PalError(string.Format(Localization.Error_WrongRepository, "https://github.com/carvelli/Dalamud-Plugins"));
                throw new InvalidOperationException();
            }
#endif

            pluginInterface.Create<Service>();
            Service.Plugin = this;

            Service.ConfigurationManager = new(pluginInterface);
            Service.ConfigurationManager.Migrate();
            Service.Configuration = Service.ConfigurationManager.Load();

            ResetRenderer();

            Service.Hooks = new Hooks();

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

            pluginInterface.UiBuilder.Draw += Draw;
            pluginInterface.UiBuilder.OpenConfigUi += OpenConfigUi;
            pluginInterface.LanguageChanged += LanguageChanged;
            Service.Framework.Update += OnFrameworkUpdate;
            Service.Chat.ChatMessage += OnChatMessage;
            Service.CommandManager.AddHandler("/pal", new CommandInfo(OnCommand)
            {
                HelpMessage = Localization.Command_pal_HelpText
            });

            ReloadLanguageStrings();
        }

        private void OpenConfigUi()
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
                Service.Chat.PalError(Localization.Error_FirstTimeSetupRequired);
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
                        var _ = new TickScheduler(() => configWindow.TestConnection());
                        break;

#if DEBUG
                    case "update-saves":
                        LocalState.UpdateAll();
                        Service.Chat.Print(Localization.Command_pal_updatesaves);
                        break;
#endif

                    case "":
                    case "config":
                        Service.WindowSystem.GetWindow<ConfigWindow>()?.Toggle();
                        break;

                    case "near":
                        DebugNearest(_ => true);
                        break;

                    case "tnear":
                        DebugNearest(m => m.Type == Marker.EType.Trap);
                        break;

                    case "hnear":
                        DebugNearest(m => m.Type == Marker.EType.Hoard);
                        break;

                    default:
                        Service.Chat.PalError(string.Format(Localization.Command_pal_UnknownSubcommand, arguments, command));
                        break;
                }
            }
            catch (Exception e)
            {
                Service.Chat.PalError(e.ToString());
            }
        }

        #region IDisposable Support
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;

            Service.CommandManager.RemoveHandler("/pal");
            Service.PluginInterface.UiBuilder.Draw -= Draw;
            Service.PluginInterface.UiBuilder.OpenConfigUi -= OpenConfigUi;
            Service.PluginInterface.LanguageChanged -= LanguageChanged;
            Service.Framework.Update -= OnFrameworkUpdate;
            Service.Chat.ChatMessage -= OnChatMessage;

            Service.WindowSystem.GetWindow<ConfigWindow>()?.Dispose();
            Service.WindowSystem.RemoveAllWindows();

            Service.RemoteApi.Dispose();
            Service.Hooks.Dispose();

            if (Renderer is IDisposable disposable)
                disposable.Dispose();
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
        }

        private void LanguageChanged(string langcode)
        {
            Localization.Culture = new CultureInfo(langcode);
            Service.WindowSystem.Windows.OfType<ILanguageChanged>().Each(w => w.LanguageChanged());
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
                    queued.Run(this, ref recreateLayout, ref saveMarkers);

                if (LastTerritory != Service.ClientState.TerritoryType)
                {
                    LastTerritory = Service.ClientState.TerritoryType;
                    TerritorySyncState = SyncState.NotAttempted;
                    NextUpdateObjects.Clear();

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
                    queued.Run(this, ref recreateLayout, ref saveMarkers);

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

        #region Rendering markers
        private void HandlePersistentMarkers(LocalState currentFloor, IList<Marker> visibleMarkers, bool saveMarkers, bool recreateLayout)
        {
            var config = Service.Configuration;
            var currentFloorMarkers = currentFloor.Markers;

            bool updateSeenMarkers = false;
            var partialAccountId = Service.Configuration.FindAccount(RemoteApi.RemoteUrl)?.AccountId.ToPartialId();
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
                    if (partialAccountId != null && knownMarker is { NetworkId: { }, RemoteSeenRequested: false } && !knownMarker.RemoteSeenOn.Contains(partialAccountId))
                        updateSeenMarkers = true;

                    continue;
                }

                currentFloorMarkers.Add(visibleMarker);
                recreateLayout = true;
                saveMarkers = true;
            }

            if (!recreateLayout && currentFloorMarkers.Count > 0 && (config.DeepDungeons.Traps.OnlyVisibleAfterPomander || config.DeepDungeons.HoardCoffers.OnlyVisibleAfterPomander))
            {

                try
                {
                    foreach (var marker in currentFloorMarkers)
                    {
                        uint desiredColor = DetermineColor(marker, visibleMarkers);
                        if (marker.RenderElement == null || !marker.RenderElement.IsValid)
                        {
                            recreateLayout = true;
                            break;
                        }

                        if (marker.RenderElement.Color != desiredColor)
                            marker.RenderElement.Color = desiredColor;
                    }
                }
                catch (Exception e)
                {
                    DebugMessage = $"{DateTime.Now}\n{e}";
                    recreateLayout = true;
                }
            }

            if (updateSeenMarkers && partialAccountId != null)
            {
                var markersToUpdate = currentFloorMarkers.Where(x => x is { Seen: true, NetworkId: { }, RemoteSeenRequested: false } && !x.RemoteSeenOn.Contains(partialAccountId)).ToList();
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
                Renderer.ResetLayer(ELayer.TrapHoard);

                List<IRenderElement> elements = new();
                foreach (var marker in currentFloorMarkers)
                {
                    if (marker.Seen || config.Mode == EMode.Online || marker is { WasImported: true, Imports.Count: > 0 })
                    {
                        if (marker.Type == Marker.EType.Trap)
                        {
                            CreateRenderElement(marker, elements, DetermineColor(marker, visibleMarkers), config.DeepDungeons.Traps);
                        }
                        else if (marker.Type == Marker.EType.Hoard)
                        {
                            CreateRenderElement(marker, elements, DetermineColor(marker, visibleMarkers), config.DeepDungeons.HoardCoffers);
                        }
                    }
                }

                if (elements.Count == 0)
                    return;

                Renderer.SetLayer(ELayer.TrapHoard, elements);
            }
        }

        private void HandleEphemeralMarkers(IList<Marker> visibleMarkers, bool recreateLayout)
        {
            recreateLayout |= EphemeralMarkers.Any(existingMarker => visibleMarkers.All(x => x != existingMarker));
            recreateLayout |= visibleMarkers.Any(visibleMarker => EphemeralMarkers.All(x => x != visibleMarker));

            if (recreateLayout)
            {
                Renderer.ResetLayer(ELayer.RegularCoffers);
                EphemeralMarkers.Clear();

                var config = Service.Configuration;

                List<IRenderElement> elements = new();
                foreach (var marker in visibleMarkers)
                {
                    EphemeralMarkers.Add(marker);

                    if (marker.Type == Marker.EType.SilverCoffer && config.DeepDungeons.SilverCoffers.Show)
                    {
                        CreateRenderElement(marker, elements, DetermineColor(marker, visibleMarkers), config.DeepDungeons.SilverCoffers);
                    }
                }

                if (elements.Count == 0)
                    return;

                Renderer.SetLayer(ELayer.RegularCoffers, elements);
            }
        }

        private uint DetermineColor(Marker marker, IList<Marker> visibleMarkers)
        {
            switch (marker.Type)
            {
                case Marker.EType.Trap when PomanderOfSight == PomanderState.Inactive || !Service.Configuration.DeepDungeons.Traps.OnlyVisibleAfterPomander || visibleMarkers.Any(x => x == marker):
                    return Service.Configuration.DeepDungeons.Traps.Color;
                case Marker.EType.Hoard when PomanderOfIntuition == PomanderState.Inactive || !Service.Configuration.DeepDungeons.HoardCoffers.OnlyVisibleAfterPomander || visibleMarkers.Any(x => x == marker):
                    return Service.Configuration.DeepDungeons.HoardCoffers.Color;
                case Marker.EType.SilverCoffer:
                    return Service.Configuration.DeepDungeons.SilverCoffers.Color;
                case Marker.EType.Trap:
                case Marker.EType.Hoard:
                    return ColorInvisible;
                default:
                    return ImGui.ColorConvertFloat4ToU32(new Vector4(1, 0.5f, 1, 0.4f));
            }
        }

        private void CreateRenderElement(Marker marker, List<IRenderElement> elements, uint color, MarkerConfiguration config)
        {
            if (!config.Show)
                return;

            var element = Renderer.CreateElement(marker.Type, marker.Position, color, config.Fill);
            marker.RenderElement = element;
            elements.Add(element);
        }
        #endregion

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
                Service.Chat.PalError(Localization.Command_pal_stats_CurrentFloor);
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
                    Service.Chat.PalError(Localization.Command_pal_stats_UnableToFetchStatistics);
                }
            }
            catch (RpcException e) when (e.StatusCode == StatusCode.PermissionDenied)
            {
                Service.Chat.Print(Localization.Command_pal_stats_CurrentFloor);
            }
            catch (Exception e)
            {
                Service.Chat.PalError(e.ToString());
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
            Service.Chat.Print($"[Palace Pal] {playerPosition}");

            var nearbyMarkers = state.Markers
                .Where(m => predicate(m))
                .Where(m => m.RenderElement != null && m.RenderElement.Color != ColorInvisible)
                .Select(m => new { m, distance = (playerPosition - m.Position)?.Length() ?? float.MaxValue })
                .OrderBy(m => m.distance)
                .Take(5)
                .ToList();
            foreach (var nearbyMarker in nearbyMarkers)
                Service.Chat.Print($"{nearbyMarker.distance:F2} - {nearbyMarker.m.Type} {nearbyMarker.m.NetworkId?.ToPartialId(length: 8)} - {nearbyMarker.m.Position}");
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

            while (NextUpdateObjects.TryDequeue(out nint address))
            {
                var obj = Service.ObjectTable.FirstOrDefault(x => x.Address == address);
                if (obj != null && obj.Position.Length() > 0.1)
                    result.Add(new Marker(Marker.EType.Trap, obj.Position) { Seen = true });
            }

            return result;
        }

        internal bool IsInDeepDungeon() =>
            Service.ClientState.IsLoggedIn
            && Service.Condition[ConditionFlag.InDeepDungeon]
            && typeof(ETerritoryType).IsEnumDefined(Service.ClientState.TerritoryType);

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

        internal void ResetRenderer()
        {
            if (Renderer is SplatoonRenderer && Service.Configuration.Renderer.SelectedRenderer == ERenderer.Splatoon)
                return;
            else if (Renderer is SimpleRenderer && Service.Configuration.Renderer.SelectedRenderer == ERenderer.Simple)
                return;

            if (Renderer is IDisposable disposable)
                disposable.Dispose();

            if (Service.Configuration.Renderer.SelectedRenderer == ERenderer.Splatoon)
                Renderer = new SplatoonRenderer(Service.PluginInterface, this);
            else
                Renderer = new SimpleRenderer();
        }

        private void Draw()
        {
            if (Renderer is SimpleRenderer sr)
                sr.DrawLayers();

            Service.WindowSystem.Draw();
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

        private class LocalizedChatMessages
        {
            public string MapRevealed { get; init; } = "???"; //"The map for this floor has been revealed!";
            public string AllTrapsRemoved { get; init; } = "???"; // "All the traps on this floor have disappeared!";
            public string HoardOnCurrentFloor { get; init; } = "???"; // "You sense the Accursed Hoard calling you...";
            public string HoardNotOnCurrentFloor { get; init; } = "???"; // "You do not sense the call of the Accursed Hoard on this floor...";
            public string HoardCofferOpened { get; init; } = "???"; // "You discover a piece of the Accursed Hoard!";
            public Regex FloorChanged { get; init; } = new(@"This isn't a game message, but will be replaced"); // new Regex(@"^Floor (\d+)$");
        }
    }
}
