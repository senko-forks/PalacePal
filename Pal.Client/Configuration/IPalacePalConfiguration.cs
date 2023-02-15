using System;
using System.Collections.Generic;
using System.Numerics;
using ImGuiNET;

namespace Pal.Client.Configuration
{
    public interface IVersioned
    {
        int Version { get; set; }
    }
    public interface IConfigurationInConfigDirectory : IVersioned
    {
    }

    public interface IPalacePalConfiguration : IConfigurationInConfigDirectory
    {
        bool FirstUse { get; set; }
        EMode Mode { get; set; }
        string BetaKey { get; }

        DeepDungeonConfiguration DeepDungeons { get; set; }
        RendererConfiguration Renderer { get; set; }

        [Obsolete]
        List<ConfigurationV1.ImportHistoryEntry> ImportHistory { get; }

        IAccountConfiguration CreateAccount(string server, Guid accountId);
        IAccountConfiguration? FindAccount(string server);
        void RemoveAccount(string server);
    }

    public class DeepDungeonConfiguration
    {
        public MarkerConfiguration Traps { get; set; } = new()
        {
            Show = true,
            Color = ImGui.ColorConvertFloat4ToU32(new Vector4(1, 0, 0, 0.4f)),
            OnlyVisibleAfterPomander = true,
            Fill = false
        };

        public MarkerConfiguration HoardCoffers { get; set; } = new()
        {
            Show = true,
            Color = ImGui.ColorConvertFloat4ToU32(new Vector4(0, 1, 1, 0.4f)),
            OnlyVisibleAfterPomander = true,
            Fill = false
        };

        public MarkerConfiguration SilverCoffers { get; set; } = new()
        {
            Show = false,
            Color = ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 1, 0.4f)),
            OnlyVisibleAfterPomander = false,
            Fill = true
        };
    }

    public class MarkerConfiguration
    {
        public bool Show { get; set; }
        public uint Color { get; set; }
        public bool OnlyVisibleAfterPomander { get; set; }
        public bool Fill { get; set; }
    }

    public class RendererConfiguration
    {
        public ERenderer SelectedRenderer { get; set; } = ERenderer.Splatoon;
    }

    public interface IAccountConfiguration
    {
        public bool IsUsable { get; }
        public string Server { get; }
        public Guid AccountId { get; }

        /// <summary>
        /// This is taken from the JWT, and is only refreshed on a successful login.
        ///
        /// If you simply reload the plugin without any server interaction, this doesn't change.
        ///
        /// This has no impact on what roles the JWT actually contains, but is just to make it
        /// easier to draw a consistent UI. The server will still reject unauthorized calls.
        /// </summary>
        public List<string> CachedRoles { get; set; }
    }
}
