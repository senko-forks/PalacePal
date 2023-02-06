using Dalamud.Configuration;
using Dalamud.Logging;
using ECommons.Schedulers;
using Newtonsoft.Json;
using Pal.Client.Scheduled;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;

namespace Pal.Client
{
    public class Configuration : IPluginConfiguration
    {
        private static readonly byte[] _entropy = { 0x22, 0x4b, 0xe7, 0x21, 0x44, 0x83, 0x69, 0x55, 0x80, 0x38 };

        public int Version { get; set; } = 5;

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

        public List<ImportHistoryEntry> ImportHistory { get; set; } = new();

        public bool ShowTraps { get; set; } = true;
        public Vector4 TrapColor { get; set; } = new Vector4(1, 0, 0, 0.4f);
        public bool OnlyVisibleTrapsAfterPomander { get; set; } = true;

        public bool ShowHoard { get; set; } = true;
        public Vector4 HoardColor { get; set; } = new Vector4(0, 1, 1, 0.4f);
        public bool OnlyVisibleHoardAfterPomander { get; set; } = true;

        public bool ShowSilverCoffers { get; set; } = false;
        public Vector4 SilverCofferColor { get; set; } = new Vector4(1, 1, 1, 0.4f);
        public bool FillSilverCoffers { get; set; } = true;

        /// <summary>
        /// Needs to be manually set.
        /// </summary>
        public string BetaKey { get; set; } = "";
        #endregion

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

            if (Version == 3)
            {
                Version = 4;
                Save();
            }

            if (Version == 4)
            {
                // 2.2 had a bug that would mark chests as traps, there's no easy way to detect this -- or clean this up.
                // Not a problem for online players, but offline players might be fucked.
                bool changedAnyFile = false;
                LocalState.ForEach(s =>
                {
                    foreach (var marker in s.Markers)
                        marker.SinceVersion = "0.0";

                    var lastModified = File.GetLastWriteTimeUtc(s.GetSaveLocation());
                    if (lastModified >= new DateTime(2023, 2, 3, 0, 0, 0, DateTimeKind.Utc))
                    {
                        s.Backup(suffix: "bak");

                        s.Markers = new ConcurrentBag<Marker>(s.Markers.Where(m => m.SinceVersion != "0.0" || m.Type == Marker.EType.Hoard || m.WasImported));
                        s.Save();

                        changedAnyFile = true;
                    }
                    else
                    {
                        // just add version information, nothing else
                        s.Save();
                    }
                });

                // Only notify offline users - we can just re-download the backup markers from the server seamlessly.
                if (Mode == EMode.Offline && changedAnyFile)
                {
                    new TickScheduler(delegate
                    {
                        Service.Chat.PrintError("[Palace Pal] Due to a bug, some coffers were accidentally saved as traps. To fix the related display issue, locally cached data was cleaned up.");
                        Service.Chat.PrintError($"If you have any backup tools installed, please restore the contents of '{Service.PluginInterface.GetPluginConfigDirectory()}' to any backup from February 2, 2023 or before.");
                        Service.Chat.PrintError("You can also manually restore .json.bak files (by removing the '.bak') if you have not been in any deep dungeon since February 2, 2023.");
                    }, 2500);
                }

                Version = 5;
                Save();
            }
        }
#pragma warning restore CS0612 // Type or member is obsolete

        public void Save()
        {
            Service.PluginInterface.SavePluginConfig(this);
            Service.Plugin.EarlyEventQueue.Enqueue(new QueuedConfigUpdate());
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
            [JsonConverter(typeof(AccountIdConverter))]
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

        public class AccountIdConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType) => true;

            public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.String)
                {
                    string? text = reader.Value?.ToString();
                    if (string.IsNullOrEmpty(text))
                        return null;

                    if (Guid.TryParse(text, out Guid guid) && guid != Guid.Empty)
                        return guid;

                    if (text.StartsWith("s:"))
                    {
                        try
                        {
                            byte[] guidBytes = ProtectedData.Unprotect(Convert.FromBase64String(text.Substring(2)), _entropy, DataProtectionScope.CurrentUser);
                            return new Guid(guidBytes);
                        }
                        catch (CryptographicException e)
                        {
                            PluginLog.Error(e, "Could not load account id");
                            return null;
                        }
                    }
                }
                throw new JsonSerializationException();
            }

            public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
            {
                if (value == null)
                {
                    writer.WriteNull();
                    return;
                }

                Guid g = (Guid)value;
                string text;
                try
                {
                    byte[] guidBytes = ProtectedData.Protect(g.ToByteArray(), _entropy, DataProtectionScope.CurrentUser);
                    text = $"s:{Convert.ToBase64String(guidBytes)}";
                }
                catch (CryptographicException)
                {
                    text = g.ToString();
                }

                writer.WriteValue(text);
            }
        }

        public class ImportHistoryEntry
        {
            public Guid Id { get; set; }
            public string? RemoteUrl { get; set; }
            public DateTime ExportedAt { get; set; }

            /// <summary>
            /// Set when the file is imported locally.
            /// </summary>
            public DateTime ImportedAt { get; set; }
        }
    }
}
