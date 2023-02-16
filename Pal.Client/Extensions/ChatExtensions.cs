using Dalamud.Game.Gui;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Pal.Client.Properties;

namespace Pal.Client.Extensions
{
    public static class ChatExtensions
    {
        public static void PalError(this ChatGui chat, string e)
        {
            chat.PrintChat(new XivChatEntry
            {
                Message = new SeStringBuilder()
                    .AddUiForeground($"[{Localization.Palace_Pal}] ", 16)
                    .AddText(e).Build(),
                Type = XivChatType.Urgent
            });
        }

        public static void PalMessage(this ChatGui chat, string message)
        {
            chat.Print(new SeStringBuilder()
                .AddUiForeground($"[{Localization.Palace_Pal}] ", 57)
                .AddText(message).Build());
        }
    }
}
