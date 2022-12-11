using Dalamud.Configuration;
using Dalamud.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Pal.Client
{
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 3;

        #region Saved configuration values
        public bool FirstUse { get; set; } = true;
        public EMode Mode { get; set; } = EMode.Offline;

        [Obsolete]
        public string? DebugAccountId { private get; set; }

        [Obsolete]
        public string? AccountId { private get; set; }

        [Obsolete]
        public Dictionary<string, Guid> AccountIds { private get; set; } = new();
        public Dictionary<string, AccountInfo> Accounts { get; set; } = new();

        public bool ShowTraps { get; set; } = true;
        public Vector4 TrapColor { get; set; } = new Vector4(1, 0, 0, 0.4f);
        public bool OnlyVisibleTrapsAfterPomander { get; set; } = true;

        public bool ShowHoard { get; set; } = true;
        public Vector4 HoardColor { get; set; } = new Vector4(0, 1, 1, 0.4f);
        public bool OnlyVisibleHoardAfterPomander { get; set; } = true;

        public bool ShowSilverCoffers { get; set; } = false;
        public Vector4 SilverCofferColor { get; set; } = new Vector4(1, 1, 1, 0.4f);
        public bool FillSilverCoffers { get; set; } = true;
        #endregion

        public delegate void OnSaved();
        public event OnSaved? Saved;

#pragma warning disable CS0612 // Type or member is obsolete
        public void Migrate()
        {
            if (Version == 1)
            {
                PluginLog.Information("Updating config to version 2");

                if (DebugAccountId != null && Guid.TryParse(DebugAccountId, out Guid debugAccountId))
                    AccountIds["http://localhost:5145"] = debugAccountId;

                if (AccountId != null && Guid.TryParse(AccountId, out Guid accountId))
                    AccountIds["https://pal.μ.tv"] = accountId;

                Version = 2;
                Save();
            }

            if (Version == 2)
            {
                PluginLog.Information("Updating config to version 3");

                Accounts = AccountIds.ToDictionary(x => x.Key, x => new AccountInfo
                {
                    Id = x.Value
                });
                Version = 3;
                Save();
            }
        }
#pragma warning restore CS0612 // Type or member is obsolete

        public void Save()
        {
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

        public class AccountInfo
        {
            public Guid? Id { get; set; }

            /// <summary>
            /// This is taken from the JWT, and is only refreshed on a successful login.
            /// 
            /// If you simply reload the plugin without any server interaction, this doesn't change.
            /// 
            /// This has no impact on what roles the JWT actually contains, but is just to make it 
            /// easier to draw a consistent UI. The server will still reject unauthorized calls.
            /// </summary>
            public List<string> CachedRoles { get; set; } = new List<string>();
        }
    }
}
