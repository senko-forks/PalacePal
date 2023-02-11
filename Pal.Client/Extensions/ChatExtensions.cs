using Dalamud.Game.Gui;
using Pal.Client.Properties;

namespace Pal.Client.Extensions
{
    public static class ChatExtensions
    {
        public static void PalError(this ChatGui chat, string e)
            => chat.PrintError($"[{Localization.Palace_Pal}] {e}");
    }
}
