using System;
using Dalamud.Game.Gui;
using Dalamud.Logging;
using Dalamud.Plugin;
using Pal.Client.Extensions;
using Pal.Client.Properties;

namespace Pal.Client.DependencyInjection
{
    public class RepoVerification
    {
        public RepoVerification(DalamudPluginInterface pluginInterface, ChatGui chatGui)
        {
            PluginLog.Information($"Install source: {pluginInterface.SourceRepository}");
            if (!pluginInterface.IsDev
                && !pluginInterface.SourceRepository.StartsWith("https://raw.githubusercontent.com/carvelli/")
                && !pluginInterface.SourceRepository.StartsWith("https://github.com/carvelli/"))
            {
                chatGui.PalError(string.Format(Localization.Error_WrongRepository,
                    "https://github.com/carvelli/Dalamud-Plugins"));
                throw new InvalidOperationException();
            }
        }
    }
}
