using System;
using System.Linq;
using Dalamud.Game.ClientState;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using ECommons.Schedulers;
using Pal.Client.Configuration;
using Pal.Client.DependencyInjection;
using Pal.Client.Extensions;
using Pal.Client.Properties;
using Pal.Client.Rendering;
using Pal.Client.Windows;

namespace Pal.Client.Commands
{
    // should restructure this when more commands exist, if that ever happens
    // this command is more-or-less a debug/troubleshooting command, if anything
    internal sealed class PalCommand : IDisposable
    {
        private readonly IPalacePalConfiguration _configuration;
        private readonly CommandManager _commandManager;
        private readonly ChatGui _chatGui;
        private readonly StatisticsService _statisticsService;
        private readonly ConfigWindow _configWindow;
        private readonly TerritoryState _territoryState;
        private readonly FloorService _floorService;
        private readonly ClientState _clientState;

        public PalCommand(
            IPalacePalConfiguration configuration,
            CommandManager commandManager,
            ChatGui chatGui,
            StatisticsService statisticsService,
            ConfigWindow configWindow,
            TerritoryState territoryState,
            FloorService floorService,
            ClientState clientState)
        {
            _configuration = configuration;
            _commandManager = commandManager;
            _chatGui = chatGui;
            _statisticsService = statisticsService;
            _configWindow = configWindow;
            _territoryState = territoryState;
            _floorService = floorService;
            _clientState = clientState;

            _commandManager.AddHandler("/pal", new CommandInfo(OnCommand)
            {
                HelpMessage = Localization.Command_pal_HelpText
            });
        }

        public void Dispose()
        {
            _commandManager.RemoveHandler("/pal");
        }

        private void OnCommand(string command, string arguments)
        {
            if (_configuration.FirstUse)
            {
                _chatGui.PalError(Localization.Error_FirstTimeSetupRequired);
                return;
            }

            try
            {
                arguments = arguments.Trim();
                switch (arguments)
                {
                    case "stats":
                        _statisticsService.ShowGlobalStatistics();
                        break;

                    case "test-connection":
                    case "tc":
                        _configWindow.IsOpen = true;
                        var _ = new TickScheduler(() => _configWindow.TestConnection());
                        break;

#if DEBUG
                    case "update-saves":
                        LocalState.UpdateAll();
                        Service.Chat.Print(Localization.Command_pal_updatesaves);
                        break;
#endif

                    case "":
                    case "config":
                        _configWindow.Toggle();
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
                        _chatGui.PalError(string.Format(Localization.Command_pal_UnknownSubcommand, arguments,
                            command));
                        break;
                }
            }
            catch (Exception e)
            {
                _chatGui.PalError(e.ToString());
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
            _chatGui.Print($"[Palace Pal] {playerPosition}");

            var nearbyMarkers = state.Markers
                .Where(m => predicate(m))
                .Where(m => m.RenderElement != null && m.RenderElement.Color != RenderData.ColorInvisible)
                .Select(m => new { m, distance = (playerPosition - m.Position)?.Length() ?? float.MaxValue })
                .OrderBy(m => m.distance)
                .Take(5)
                .ToList();
            foreach (var nearbyMarker in nearbyMarkers)
                _chatGui.Print(
                    $"{nearbyMarker.distance:F2} - {nearbyMarker.m.Type} {nearbyMarker.m.NetworkId?.ToPartialId(length: 8)} - {nearbyMarker.m.Position}");
        }
    }
}
