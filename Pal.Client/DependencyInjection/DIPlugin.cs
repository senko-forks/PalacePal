using System.Globalization;
using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Microsoft.Extensions.DependencyInjection;
using Pal.Client.Commands;
using Pal.Client.Configuration;
using Pal.Client.Net;
using Pal.Client.Properties;
using Pal.Client.Rendering;
using Pal.Client.Scheduled;
using Pal.Client.Windows;

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
            services.AddSingleton(clientState);
            services.AddSingleton(gameGui);
            services.AddSingleton(chatGui);
            services.AddSingleton(objectTable);
            services.AddSingleton(framework);
            services.AddSingleton(condition);
            services.AddSingleton(commandManager);
            services.AddSingleton(dataManager);
            services.AddSingleton(new WindowSystem(typeof(DIPlugin).AssemblyQualifiedName));

            // plugin-specific
            services.AddSingleton<Plugin>();
            services.AddSingleton<DebugState>();
            services.AddSingleton<Hooks>();
            services.AddSingleton<RemoteApi>();
            services.AddSingleton<ConfigurationManager>();
            services.AddSingleton<IPalacePalConfiguration>(sp => sp.GetRequiredService<ConfigurationManager>().Load());
            services.AddTransient<RepoVerification>();
            services.AddSingleton<PalCommand>();

            // territory handling
            services.AddSingleton<TerritoryState>();
            services.AddSingleton<FrameworkService>();
            services.AddSingleton<ChatService>();
            services.AddSingleton<FloorService>();
            services.AddSingleton<QueueHandler>();

            // windows & related services
            services.AddSingleton<AgreementWindow>();
            services.AddSingleton<ConfigWindow>();
            services.AddTransient<StatisticsService>();
            services.AddSingleton<StatisticsWindow>();

            // these should maybe be scoped
            services.AddSingleton<SimpleRenderer>();
            services.AddSingleton<SplatoonRenderer>();
            services.AddSingleton<RenderAdapter>();

            // set up the current UI language before creating anything
            Localization.Culture = new CultureInfo(pluginInterface.UiLanguage);

            // build
            _serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true,
            });

            // initialize plugin
#if RELEASE
            // You're welcome to remove this code in your fork, but please make sure that:
            // - none of the links accessible within FFXIV open the original repo (e.g. in the plugin installer), and
            // - you host your own server instance
            //
            // This is mainly to avoid this plugin being included in 'mega-repos' that, for whatever reason, decide
            // that collecting all plugins is a good idea (and break half in the process).
            _serviceProvider.GetService<RepoVerification>();
#endif

            _serviceProvider.GetRequiredService<Hooks>();
            _serviceProvider.GetRequiredService<AgreementWindow>();
            _serviceProvider.GetRequiredService<ConfigWindow>();
            _serviceProvider.GetRequiredService<StatisticsWindow>();
            _serviceProvider.GetRequiredService<PalCommand>();
            _serviceProvider.GetRequiredService<FrameworkService>();
            _serviceProvider.GetRequiredService<ChatService>();

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
