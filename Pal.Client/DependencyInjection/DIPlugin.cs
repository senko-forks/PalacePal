using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Plugin;
using Microsoft.Extensions.DependencyInjection;
using Pal.Client.Properties;

namespace Pal.Client.DependencyInjection
{
    /// <summary>
    /// DI-aware Plugin.
    /// </summary>
    internal sealed class DIPlugin : IDalamudPlugin
    {
        private ServiceProvider? _serviceProvider;

        public string Name => Localization.Palace_Pal;

        public DIPlugin(DalamudPluginInterface pluginInterface,
            ClientState clientState,
            GameGui gameGui,
            ChatGui chatGui,
            ObjectTable objectTable,
            Framework framework,
            Condition condition,
            CommandManager commandManager,
            DataManager dataManager)
        {
            IServiceCollection services = new ServiceCollection();

            // dalamud
            services.AddSingleton<IDalamudPlugin>(this);
            services.AddSingleton(pluginInterface);
            services.AddSingleton(gameGui);
            services.AddSingleton(chatGui);
            services.AddSingleton(objectTable);
            services.AddSingleton(framework);
            services.AddSingleton(condition);
            services.AddSingleton(commandManager);
            services.AddSingleton(dataManager);

            // palace pal
            services.AddSingleton<Plugin>();

            // build
            _serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true,
            });

            // initialize plugin
            _serviceProvider.GetRequiredService<Plugin>();
        }

        public void Dispose()
        {
            // ensure we're not calling dispose recursively on ourselves
            if (_serviceProvider != null)
            {
                ServiceProvider serviceProvider = _serviceProvider;
                _serviceProvider = null;

                serviceProvider.Dispose();
            }

        }
    }
}
