using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Dalamud.Data;
using Dalamud.Game.Gui;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using Lumina.Excel.GeneratedSheets;
using Pal.Client.Configuration;
using Pal.Client.Floors;
using Pal.Client.Rendering;

namespace Pal.Client.DependencyInjection
{
    internal sealed class ChatService : IDisposable
    {
        private readonly IChatGui _chatGui;
        private readonly FrameworkService _frameworkService;
        private readonly TerritoryState _territoryState;
        private readonly IPalacePalConfiguration _configuration;
        private readonly IPartyList _partyList;
        private readonly IDataManager _dataManager;
        private readonly IObjectTable _objectTable;
        private readonly LocalizedChatMessages _localizedChatMessages;
        private readonly RenderAdapter _renderAdapter;

        public ChatService(IChatGui chatGui, TerritoryState territoryState,
            FrameworkService frameworkService, IPalacePalConfiguration configuration,
            IDataManager dataManager, IObjectTable objectTable, IPartyList partyList,
            RenderAdapter renderAdapter)
        {
            _chatGui = chatGui;
            _territoryState = territoryState;
            _frameworkService = frameworkService;
            _configuration = configuration;
            _dataManager = dataManager;
            _objectTable = objectTable;
            _partyList = partyList;
            _renderAdapter = renderAdapter;

            _localizedChatMessages = LoadLanguageStrings();

            _chatGui.ChatMessage += OnChatMessage;
        }

        public void Dispose()
            => _chatGui.ChatMessage -= OnChatMessage;

        private void OnChatMessage(XivChatType type, uint senderId, ref SeString sender, ref SeString seMessage,
            ref bool isHandled)
        {
            if (_configuration.FirstUse)
                return;

            if (type != (XivChatType)2105)
                return;

            string message = seMessage.ToString();
            var returnToCofferMatch = _localizedChatMessages.ReturnToCoffer.Match(message);
            if (_localizedChatMessages.FloorChanged.IsMatch(message))
            {
                _territoryState.PomanderOfSight = PomanderState.Inactive;

                if (_territoryState.PomanderOfIntuition == PomanderState.FoundOnCurrentFloor)
                    _territoryState.PomanderOfIntuition = PomanderState.Inactive;

                _renderAdapter.ResetLayer(ELayer.CofferLabels);
            }
            else if (returnToCofferMatch.Success)
            {
                ulong fullId = _frameworkService.LastCofferId;
                uint shortId = (uint)_frameworkService.LastCofferId;

                if (!_configuration.DeepDungeons.CofferLabels.Show
                 || shortId == 0
                 || _frameworkService.MarkedCoffers.Contains(shortId))
                    return;

                var chestObj = _objectTable.SearchById(fullId);

                if (chestObj == null
                 || chestObj.ObjectKind == Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Treasure
                 || _partyList.Count >= 2)
                    return;

                string itemName = returnToCofferMatch.Groups[1].Value;

                if (itemName.StartsWith("pomander of "))
                    itemName = itemName.Substring(12);

                if (itemName.StartsWith("protomander of "))
                    itemName = itemName.Substring(15);

                if (itemName.Length > 0)
                    itemName = itemName[0].ToString().ToUpper() + itemName.Substring(1);

                if (itemName == "Onion knight")
                    itemName = "Onion Knight";

                IRenderElement textElement = _renderAdapter.CreateTextElement(
                    (uint)_frameworkService.LastCofferId,
                    itemName,
                    _configuration.DeepDungeons.CofferLabels.Color
                );
                List<IRenderElement> elementList = new(){ textElement };
                _renderAdapter.SetLayer(ELayer.CofferLabels, elementList);
                _frameworkService.MarkedCoffers.Add(shortId);
            }
            else if (message.EndsWith(_localizedChatMessages.MapRevealed))
            {
                _territoryState.PomanderOfSight = PomanderState.Active;
            }
            else if (message.EndsWith(_localizedChatMessages.AllTrapsRemoved))
            {
                _territoryState.PomanderOfSight = PomanderState.PomanderOfSafetyUsed;
            }
            else if (message.EndsWith(_localizedChatMessages.HoardNotOnCurrentFloor) ||
                     message.EndsWith(_localizedChatMessages.HoardOnCurrentFloor))
            {
                // There is no functional difference between these - if you don't open the marked coffer,
                // going to higher floors will keep the pomander active.
                _territoryState.PomanderOfIntuition = PomanderState.Active;
            }
            else if (message.EndsWith(_localizedChatMessages.HoardCofferOpened))
            {
                _territoryState.PomanderOfIntuition = PomanderState.FoundOnCurrentFloor;
            }
        }

        private LocalizedChatMessages LoadLanguageStrings()
        {
            return new LocalizedChatMessages
            {
                MapRevealed = GetLocalizedString(7256),
                AllTrapsRemoved = GetLocalizedString(7255),
                HoardOnCurrentFloor = GetLocalizedString(7272),
                HoardNotOnCurrentFloor = GetLocalizedString(7273),
                HoardCofferOpened = GetLocalizedString(7274),
                FloorChanged = new Regex("^" + GetLocalizedString(7270) + @"(\d+)$"),
                ReturnToCoffer = new Regex("^" + GetLocalizedString(7222).Replace("DeepDungeonItem", @"(.+?)") + "$"),
            };
        }

        private string GetLocalizedString(uint id)
        {
            return _dataManager.GetExcelSheet<LogMessage>()?.GetRow(id)?.Text?.ToString() ?? "Unknown";
        }

        private sealed class LocalizedChatMessages
        {
            public string MapRevealed { get; init; } = "???"; //"The map for this floor has been revealed!";
            public string AllTrapsRemoved { get; init; } = "???"; // "All the traps on this floor have disappeared!";
            public string HoardOnCurrentFloor { get; init; } = "???"; // "You sense the Accursed Hoard calling you...";

            public string HoardNotOnCurrentFloor { get; init; } =
                "???"; // "You do not sense the call of the Accursed Hoard on this floor...";

            public string HoardCofferOpened { get; init; } = "???"; // "You discover a piece of the Accursed Hoard!";

            public Regex FloorChanged { get; init; } =
                new(@"This isn't a game message, but will be replaced"); // new Regex(@"^Floor (\d+)$");

            public Regex ReturnToCoffer { get; init; } =
                new(@"This isn't a game message, but will be replaced");
        }
    }
}
