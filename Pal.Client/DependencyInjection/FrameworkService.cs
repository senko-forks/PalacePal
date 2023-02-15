using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Types;
using ImGuiNET;
using Pal.Client.Configuration;
using Pal.Client.Extensions;
using Pal.Client.Net;
using Pal.Client.Rendering;
using Pal.Client.Scheduled;

namespace Pal.Client.DependencyInjection
{
    internal sealed class FrameworkService : IDisposable
    {
        private readonly Framework _framework;
        private readonly ConfigurationManager _configurationManager;
        private readonly IPalacePalConfiguration _configuration;
        private readonly ClientState _clientState;
        private readonly TerritoryState _territoryState;
        private readonly FloorService _floorService;
        private readonly DebugState _debugState;
        private readonly RenderAdapter _renderAdapter;
        private readonly QueueHandler _queueHandler;
        private readonly ObjectTable _objectTable;
        private readonly RemoteApi _remoteApi;

        internal Queue<IQueueOnFrameworkThread> EarlyEventQueue { get; } = new();
        internal Queue<IQueueOnFrameworkThread> LateEventQueue { get; } = new();
        internal ConcurrentQueue<nint> NextUpdateObjects { get; } = new();

        public FrameworkService(Framework framework,
            ConfigurationManager configurationManager,
            IPalacePalConfiguration configuration,
            ClientState clientState,
            TerritoryState territoryState,
            FloorService floorService,
            DebugState debugState,
            RenderAdapter renderAdapter,
            QueueHandler queueHandler,
            ObjectTable objectTable,
            RemoteApi remoteApi)
        {
            _framework = framework;
            _configurationManager = configurationManager;
            _configuration = configuration;
            _clientState = clientState;
            _territoryState = territoryState;
            _floorService = floorService;
            _debugState = debugState;
            _renderAdapter = renderAdapter;
            _queueHandler = queueHandler;
            _objectTable = objectTable;
            _remoteApi = remoteApi;

            _framework.Update += OnUpdate;
            _configurationManager.Saved += OnSaved;
        }

        public void Dispose()
        {
            _framework.Update -= OnUpdate;
            _configurationManager.Saved -= OnSaved;
        }

        private void OnSaved(object? sender, IPalacePalConfiguration? config)
            => EarlyEventQueue.Enqueue(new QueuedConfigUpdate());

        private void OnUpdate(Framework framework)
        {
            if (_configuration.FirstUse)
                return;

            try
            {
                bool recreateLayout = false;
                bool saveMarkers = false;

                while (EarlyEventQueue.TryDequeue(out IQueueOnFrameworkThread? queued))
                    _queueHandler.Handle(queued, ref recreateLayout, ref saveMarkers);

                if (_territoryState.LastTerritory != _clientState.TerritoryType)
                {
                    _territoryState.LastTerritory = _clientState.TerritoryType;
                    _territoryState.TerritorySyncState = SyncState.NotAttempted;
                    NextUpdateObjects.Clear();

                    if (_territoryState.IsInDeepDungeon())
                        _floorService.GetFloorMarkers(_territoryState.LastTerritory);
                    _floorService.EphemeralMarkers.Clear();
                    _territoryState.PomanderOfSight = PomanderState.Inactive;
                    _territoryState.PomanderOfIntuition = PomanderState.Inactive;
                    recreateLayout = true;
                    _debugState.Reset();
                }

                if (!_territoryState.IsInDeepDungeon())
                    return;

                if (_configuration.Mode == EMode.Online && _territoryState.TerritorySyncState == SyncState.NotAttempted)
                {
                    _territoryState.TerritorySyncState = SyncState.Started;
                    Task.Run(async () => await DownloadMarkersForTerritory(_territoryState.LastTerritory));
                }

                while (LateEventQueue.TryDequeue(out IQueueOnFrameworkThread? queued))
                    _queueHandler.Handle(queued, ref recreateLayout, ref saveMarkers);

                var currentFloor = _floorService.GetFloorMarkers(_territoryState.LastTerritory);

                IList<Marker> visibleMarkers = GetRelevantGameObjects();
                HandlePersistentMarkers(currentFloor, visibleMarkers.Where(x => x.IsPermanent()).ToList(), saveMarkers, recreateLayout);
                HandleEphemeralMarkers(visibleMarkers.Where(x => !x.IsPermanent()).ToList(), recreateLayout);
            }
            catch (Exception e)
            {
                _debugState.SetFromException(e);
            }
        }

        #region Render Markers
        private void HandlePersistentMarkers(LocalState currentFloor, IList<Marker> visibleMarkers, bool saveMarkers, bool recreateLayout)
        {
            var currentFloorMarkers = currentFloor.Markers;

            bool updateSeenMarkers = false;
            var partialAccountId = _configuration.FindAccount(RemoteApi.RemoteUrl)?.AccountId.ToPartialId();
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

            if (!recreateLayout && currentFloorMarkers.Count > 0 && (_configuration.DeepDungeons.Traps.OnlyVisibleAfterPomander || _configuration.DeepDungeons.HoardCoffers.OnlyVisibleAfterPomander))
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
                    _debugState.SetFromException(e);
                    recreateLayout = true;
                }
            }

            if (updateSeenMarkers && partialAccountId != null)
            {
                var markersToUpdate = currentFloorMarkers.Where(x => x is { Seen: true, NetworkId: { }, RemoteSeenRequested: false } && !x.RemoteSeenOn.Contains(partialAccountId)).ToList();
                foreach (var marker in markersToUpdate)
                    marker.RemoteSeenRequested = true;
                Task.Run(async () => await SyncSeenMarkersForTerritory(_territoryState.LastTerritory, markersToUpdate));
            }

            if (saveMarkers)
            {
                currentFloor.Save();

                if (_territoryState.TerritorySyncState == SyncState.Complete)
                {
                    var markersToUpload = currentFloorMarkers.Where(x => x.IsPermanent() && x.NetworkId == null && !x.UploadRequested).ToList();
                    if (markersToUpload.Count > 0)
                    {
                        foreach (var marker in markersToUpload)
                            marker.UploadRequested = true;
                        Task.Run(async () => await UploadMarkersForTerritory(_territoryState.LastTerritory, markersToUpload));
                    }
                }
            }

            if (recreateLayout)
            {
                _renderAdapter.ResetLayer(ELayer.TrapHoard);

                List<IRenderElement> elements = new();
                foreach (var marker in currentFloorMarkers)
                {
                    if (marker.Seen || _configuration.Mode == EMode.Online || marker is { WasImported: true, Imports.Count: > 0 })
                    {
                        if (marker.Type == Marker.EType.Trap)
                        {
                            CreateRenderElement(marker, elements, DetermineColor(marker, visibleMarkers), _configuration.DeepDungeons.Traps);
                        }
                        else if (marker.Type == Marker.EType.Hoard)
                        {
                            CreateRenderElement(marker, elements, DetermineColor(marker, visibleMarkers), _configuration.DeepDungeons.HoardCoffers);
                        }
                    }
                }

                if (elements.Count == 0)
                    return;

                _renderAdapter.SetLayer(ELayer.TrapHoard, elements);
            }
        }

        private void HandleEphemeralMarkers(IList<Marker> visibleMarkers, bool recreateLayout)
        {
            recreateLayout |= _floorService.EphemeralMarkers.Any(existingMarker => visibleMarkers.All(x => x != existingMarker));
            recreateLayout |= visibleMarkers.Any(visibleMarker => _floorService.EphemeralMarkers.All(x => x != visibleMarker));

            if (recreateLayout)
            {
                _renderAdapter.ResetLayer(ELayer.RegularCoffers);
                _floorService.EphemeralMarkers.Clear();

                List<IRenderElement> elements = new();
                foreach (var marker in visibleMarkers)
                {
                    _floorService.EphemeralMarkers.Add(marker);

                    if (marker.Type == Marker.EType.SilverCoffer && _configuration.DeepDungeons.SilverCoffers.Show)
                    {
                        CreateRenderElement(marker, elements, DetermineColor(marker, visibleMarkers), _configuration.DeepDungeons.SilverCoffers);
                    }
                }

                if (elements.Count == 0)
                    return;

                _renderAdapter.SetLayer(ELayer.RegularCoffers, elements);
            }
        }

        private uint DetermineColor(Marker marker, IList<Marker> visibleMarkers)
        {
            switch (marker.Type)
            {
                case Marker.EType.Trap when _territoryState.PomanderOfSight == PomanderState.Inactive || !_configuration.DeepDungeons.Traps.OnlyVisibleAfterPomander || visibleMarkers.Any(x => x == marker):
                    return _configuration.DeepDungeons.Traps.Color;
                case Marker.EType.Hoard when _territoryState.PomanderOfIntuition == PomanderState.Inactive || !_configuration.DeepDungeons.HoardCoffers.OnlyVisibleAfterPomander || visibleMarkers.Any(x => x == marker):
                    return _configuration.DeepDungeons.HoardCoffers.Color;
                case Marker.EType.SilverCoffer:
                    return _configuration.DeepDungeons.SilverCoffers.Color;
                case Marker.EType.Trap:
                case Marker.EType.Hoard:
                    return RenderData.ColorInvisible;
                default:
                    return ImGui.ColorConvertFloat4ToU32(new Vector4(1, 0.5f, 1, 0.4f));
            }
        }

        private void CreateRenderElement(Marker marker, List<IRenderElement> elements, uint color, MarkerConfiguration config)
        {
            if (!config.Show)
                return;

            var element = _renderAdapter.CreateElement(marker.Type, marker.Position, color, config.Fill);
            marker.RenderElement = element;
            elements.Add(element);
        }
        #endregion

        #region Up-/Download
        private async Task DownloadMarkersForTerritory(ushort territoryId)
        {
            try
            {
                var (success, downloadedMarkers) = await _remoteApi.DownloadRemoteMarkers(territoryId);
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
                _debugState.SetFromException(e);
            }
        }

        private async Task UploadMarkersForTerritory(ushort territoryId, List<Marker> markersToUpload)
        {
            try
            {
                var (success, uploadedMarkers) = await _remoteApi.UploadMarker(territoryId, markersToUpload);
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
                _debugState.SetFromException(e);
            }
        }

        private async Task SyncSeenMarkersForTerritory(ushort territoryId, List<Marker> markersToUpdate)
        {
            try
            {
                var success = await _remoteApi.MarkAsSeen(territoryId, markersToUpdate);
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
                _debugState.SetFromException(e);
            }
        }
        #endregion

        private IList<Marker> GetRelevantGameObjects()
        {
            List<Marker> result = new();
            for (int i = 246; i < _objectTable.Length; i++)
            {
                GameObject? obj = _objectTable[i];
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
                var obj = _objectTable.FirstOrDefault(x => x.Address == address);
                if (obj != null && obj.Position.Length() > 0.1)
                    result.Add(new Marker(Marker.EType.Trap, obj.Position) { Seen = true });
            }

            return result;
        }
    }
}
