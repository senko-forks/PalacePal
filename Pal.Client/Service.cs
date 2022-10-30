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

        public static Plugin Plugin { get; set; } = null!;
        public static WindowSystem WindowSystem { get; set; } = new(typeof(Service).AssemblyQualifiedName);
        internal static RemoteApi RemoteApi { get; set; } = new RemoteApi();
        public static Configuration Configuration { get; set; } = null!;
    }
}
