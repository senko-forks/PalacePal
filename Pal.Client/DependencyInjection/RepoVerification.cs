using System;
using Dalamud.Game.Gui;
using Dalamud.Logging;
using Dalamud.Plugin;
using Microsoft.Extensions.Logging;
using Pal.Client.Extensions;
using Pal.Client.Properties;

namespace Pal.Client.DependencyInjection
{
    internal sealed class RepoVerification
    {
        public RepoVerification(ILogger<RepoVerification> logger, DalamudPluginInterface pluginInterface, Chat chat)
        {
            logger.LogInformation("Install source: {Repo}", pluginInterface.SourceRepository);
        }

        internal sealed class RepoVerificationFailedException : Exception
        {
        }
    }
}
