using System;
using System.Linq;
using Dalamud.Game.ClientState;
using Pal.Client.DependencyInjection;
using Pal.Client.Extensions;
using Pal.Client.Rendering;

namespace Pal.Client.Commands
{
    internal sealed class PalNearCommand
    {
        private readonly Chat _chat;
        private readonly ClientState _clientState;
        private readonly TerritoryState _territoryState;
        private readonly FloorService _floorService;

        public PalNearCommand(Chat chat, ClientState clientState, TerritoryState territoryState,
            FloorService floorService)
        {
            _chat = chat;
            _clientState = clientState;
            _territoryState = territoryState;
            _floorService = floorService;
        }

        public void Execute(string arguments)
        {
            switch (arguments)
            {
                default:
                    DebugNearest(_ => true);
                    break;

                case "tnear":
                    DebugNearest(m => m.Type == Marker.EType.Trap);
                    break;

                case "hnear":
                    DebugNearest(m => m.Type == Marker.EType.Hoard);
                    break;
            }
        }

        private void DebugNearest(Predicate<Marker> predicate)
        {
            if (!_territoryState.IsInDeepDungeon())
                return;

            var state = _floorService.GetFloorMarkers(_clientState.TerritoryType);
            var playerPosition = _clientState.LocalPlayer?.Position;
            if (playerPosition == null)
                return;
            _chat.Message($"{playerPosition}");

            var nearbyMarkers = state.Markers
                .Where(m => predicate(m))
                .Where(m => m.RenderElement != null && m.RenderElement.Color != RenderData.ColorInvisible)
                .Select(m => new { m, distance = (playerPosition - m.Position)?.Length() ?? float.MaxValue })
                .OrderBy(m => m.distance)
                .Take(5)
                .ToList();
            foreach (var nearbyMarker in nearbyMarkers)
                _chat.UnformattedMessage(
                    $"{nearbyMarker.distance:F2} - {nearbyMarker.m.Type} {nearbyMarker.m.NetworkId?.ToPartialId(length: 8)} - {nearbyMarker.m.Position}");
        }
    }
}
