using Dalamud.Configuration;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Graphics;
using System.Numerics;

namespace Pal.Client
{
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; }

        #region Saved configuration values
        public bool FirstUse { get; set; } = true;
        public EMode Mode { get; set; } = EMode.Offline;
        public string AccountId { get; set; }

        public bool ShowTraps { get; set; } = true;
        public Vector4 TrapColor { get; set; } = new Vector4(1, 0, 0, 0.4f);
        public bool ShowHoard { get; set; } = true;
        public Vector4 HoardColor { get; set; } = new Vector4(0, 1, 1, 0.4f);
        #endregion

        public delegate void OnSaved();
        public event OnSaved Saved;

        public void Save()
        {
            Version = 1;
            Service.PluginInterface.SavePluginConfig(this);
            Saved?.Invoke();
        }

        public enum EMode
        {
            /// <summary>
            /// Fetches trap locations from remote server.
            /// </summary>
            Online = 1,

            /// <summary>
            /// Only shows traps found by yourself uisng a pomander of sight.
            /// </summary>
            Offline = 2,
        }
    }
}
