using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Pal.Client.Configuration;
using Pal.Client.Net;

namespace Pal.Client
{
    public class Service
    {
        [PluginService] public static DalamudPluginInterface PluginInterface { get; private set; } = null!;
        [PluginService] public static ClientState ClientState { get; set; } = null!;
        [PluginService] public static ChatGui Chat { get; private set; } = null!;
        [PluginService] public static ObjectTable ObjectTable { get; private set; } = null!;
        [PluginService] public static Framework Framework { get; set; } = null!;
        [PluginService] public static Condition Condition { get; set; } = null!;
        [PluginService] public static CommandManager CommandManager { get; set; } = null!;
        [PluginService] public static DataManager DataManager { get; set; } = null!;
        [PluginService] public static GameGui GameGui { get; set; } = null!;

        internal static Plugin Plugin { get; set; } = null!;
        internal static WindowSystem WindowSystem { get; } = new(typeof(Service).AssemblyQualifiedName);
        internal static RemoteApi RemoteApi { get; } = new();
        internal static ConfigurationManager ConfigurationManager { get; set; } = null!;
        internal static IPalacePalConfiguration Configuration { get; set; } = null!;
        internal static Hooks Hooks { get; set; } = null!;
    }
}
