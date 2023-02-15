using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Dalamud.Logging;
using Dalamud.Plugin;
using ImGuiNET;
using Pal.Client.DependencyInjection;
using Pal.Client.Scheduled;
using NJson = Newtonsoft.Json;

namespace Pal.Client.Configuration
{
    internal class ConfigurationManager
    {
        private readonly DalamudPluginInterface _pluginInterface;

        public event EventHandler<IPalacePalConfiguration>? Saved;

        public ConfigurationManager(DalamudPluginInterface pluginInterface)
        {
            _pluginInterface = pluginInterface;

            Migrate();
        }

        private string ConfigPath => Path.Join(_pluginInterface.GetPluginConfigDirectory(), "palace-pal.config.json");

        public IPalacePalConfiguration Load()
        {
            return JsonSerializer.Deserialize<ConfigurationV7>(File.ReadAllText(ConfigPath, Encoding.UTF8)) ??
                   new ConfigurationV7();
        }

        public void Save(IConfigurationInConfigDirectory config, bool queue = true)
        {
            File.WriteAllText(ConfigPath,
                JsonSerializer.Serialize(config, config.GetType(),
                    new JsonSerializerOptions
                    { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }),
                Encoding.UTF8);
            if (queue && config is ConfigurationV7 v7)
                Saved?.Invoke(this, v7);
        }

#pragma warning disable CS0612
#pragma warning disable CS0618
        private void Migrate()
        {
            if (_pluginInterface.ConfigFile.Exists)
            {
                PluginLog.Information("Migrating config file from v1-v6 format");

                ConfigurationV1 configurationV1 =
                    NJson.JsonConvert.DeserializeObject<ConfigurationV1>(
                        File.ReadAllText(_pluginInterface.ConfigFile.FullName)) ?? new ConfigurationV1();
                configurationV1.Migrate();
                configurationV1.Save();

                var v7 = MigrateToV7(configurationV1);
                Save(v7, queue: false);

                File.Move(_pluginInterface.ConfigFile.FullName, _pluginInterface.ConfigFile.FullName + ".old", true);
            }
        }

        private ConfigurationV7 MigrateToV7(ConfigurationV1 v1)
        {
            ConfigurationV7 v7 = new()
            {
                Version = 7,
                FirstUse = v1.FirstUse,
                Mode = v1.Mode,
                BetaKey = v1.BetaKey,

                DeepDungeons = new DeepDungeonConfiguration
                {
                    Traps = new MarkerConfiguration
                    {
                        Show = v1.ShowTraps,
                        Color = ImGui.ColorConvertFloat4ToU32(v1.TrapColor),
                        Fill = false
                    },
                    HoardCoffers = new MarkerConfiguration
                    {
                        Show = v1.ShowHoard,
                        Color = ImGui.ColorConvertFloat4ToU32(v1.HoardColor),
                        Fill = false
                    },
                    SilverCoffers = new MarkerConfiguration
                    {
                        Show = v1.ShowSilverCoffers,
                        Color = ImGui.ColorConvertFloat4ToU32(v1.SilverCofferColor),
                        Fill = v1.FillSilverCoffers
                    }
                }
            };

            foreach (var (server, oldAccount) in v1.Accounts)
            {
                string? accountId = oldAccount.Id;
                if (string.IsNullOrEmpty(accountId))
                    continue;

                string serverName = server.Replace(".μ.tv", ".liza.sh");
                IAccountConfiguration newAccount = v7.CreateAccount(serverName, accountId);
                newAccount.CachedRoles = oldAccount.CachedRoles.ToList();
            }

            // TODO Migrate ImportHistory

            return v7;
        }
#pragma warning restore CS0618
#pragma warning restore CS0612
    }
}
