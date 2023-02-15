using System;
using Dalamud.IoC;
using Dalamud.Plugin;
using Pal.Client.Configuration;

namespace Pal.Client
{
    [Obsolete]
    public class Service
    {
        [PluginService] public static DalamudPluginInterface PluginInterface { get; private set; } = null!;

        internal static IPalacePalConfiguration Configuration { get; set; } = null!;
    }
}
