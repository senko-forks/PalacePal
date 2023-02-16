using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using Dalamud.Plugin;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pal.Client.Commands;
using Pal.Client.Configuration;
using Pal.Client.Database;
using Pal.Client.DependencyInjection;
using Pal.Client.Net;
using Pal.Client.Properties;
using Pal.Client.Rendering;
using Pal.Client.Scheduled;
using Pal.Client.Windows;

namespace Pal.Client
{
    /// <summary>
    /// DI-aware Plugin.
    /// </summary>
    // ReSharper disable once UnusedType.Global
    internal sealed class DependencyInjectionContext : IDalamudPlugin
    {
        private readonly string _sqliteConnectionString;
        private readonly CancellationTokenSource _initCts = new();
        private ServiceProvider? _serviceProvider;

        public string Name => Localization.Palace_Pal;

        public DependencyInjectionContext(DalamudPluginInterface pluginInterface,
            ClientState clientState,
            GameGui gameGui,
            ChatGui chatGui,
            ObjectTable objectTable,
            Framework framework,
            Condition condition,
            CommandManager commandManager,
            DataManager dataManager)
        {
            PluginLog.Information("Building service container");

            CancellationToken token = _initCts.Token;
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
            services.AddSingleton(new WindowSystem(typeof(DependencyInjectionContext).AssemblyQualifiedName));

            // EF core
            _sqliteConnectionString =
                $"Data Source={Path.Join(pluginInterface.GetPluginConfigDirectory(), "palace-pal.data.sqlite3")}";
            services.AddDbContext<PalClientContext>(o => o.UseSqlite(_sqliteConnectionString));

            // plugin-specific
            services.AddSingleton<Plugin>();
            services.AddSingleton<DebugState>();
            services.AddSingleton<Hooks>();
            services.AddSingleton<RemoteApi>();
            services.AddSingleton<ConfigurationManager>();
            services.AddSingleton<IPalacePalConfiguration>(sp => sp.GetRequiredService<ConfigurationManager>().Load());
            services.AddTransient<RepoVerification>();
            services.AddSingleton<PalCommand>();

            // territory & marker related services
            services.AddSingleton<TerritoryState>();
            services.AddSingleton<FrameworkService>();
            services.AddSingleton<ChatService>();
            services.AddSingleton<FloorService>();
            services.AddSingleton<ImportService>();

            // windows & related services
            services.AddSingleton<AgreementWindow>();
            services.AddSingleton<ConfigWindow>();
            services.AddTransient<StatisticsService>();
            services.AddSingleton<StatisticsWindow>();

            // these should maybe be scoped
            services.AddSingleton<SimpleRenderer>();
            services.AddSingleton<SplatoonRenderer>();
            services.AddSingleton<RenderAdapter>();

            // queue handling
            services.AddTransient<IQueueOnFrameworkThread.Handler<QueuedImport>, QueuedImport.Handler>();
            services.AddTransient<IQueueOnFrameworkThread.Handler<QueuedUndoImport>, QueuedUndoImport.Handler>();
            services.AddTransient<IQueueOnFrameworkThread.Handler<QueuedConfigUpdate>, QueuedConfigUpdate.Handler>();
            services.AddTransient<IQueueOnFrameworkThread.Handler<QueuedSyncResponse>, QueuedSyncResponse.Handler>();

            // set up the current UI language before creating anything
            Localization.Culture = new CultureInfo(pluginInterface.UiLanguage);

            // build
            _serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true,
            });


#if RELEASE
            // You're welcome to remove this code in your fork, but please make sure that:
            // - none of the links accessible within FFXIV open the original repo (e.g. in the plugin installer), and
            // - you host your own server instance
            //
            // This is mainly to avoid this plugin being included in 'mega-repos' that, for whatever reason, decide
            // that collecting all plugins is a good idea (and break half in the process).
            _serviceProvider.GetService<RepoVerification>();
#endif

            // This is not ideal as far as loading the plugin goes, because there's no way to check for errors and
            // tell Dalamud that no, the plugin isn't ready -- so the plugin will count as properly initialized,
            // even if it's not.
            //
            // There's 2-3 seconds of slowdown primarily caused by the sqlite init, but that needs to happen for
            // config stuff.
            PluginLog.Information("Service container built, triggering async init");
            Task.Run(async () =>
            {
                try
                {
                    PluginLog.Information("Starting async init");

                    // initialize database
                    await using (var scope = _serviceProvider.CreateAsyncScope())
                    {
                        PluginLog.Log("Loading database & running migrations");
                        await using var dbContext = scope.ServiceProvider.GetRequiredService<PalClientContext>();
                        await dbContext.Database.MigrateAsync();

                        PluginLog.Log("Completed database migrations");
                    }

                    token.ThrowIfCancellationRequested();

                    // set up legacy services
                    LocalState.PluginConfigDirectory = pluginInterface.GetPluginConfigDirectory();
                    LocalState.Mode = _serviceProvider.GetRequiredService<IPalacePalConfiguration>().Mode;

                    // windows that have logic to open on startup
                    _serviceProvider.GetRequiredService<AgreementWindow>();

                    // initialize components that are mostly self-contained/self-registered
                    _serviceProvider.GetRequiredService<Hooks>();
                    _serviceProvider.GetRequiredService<PalCommand>();
                    _serviceProvider.GetRequiredService<FrameworkService>();
                    _serviceProvider.GetRequiredService<ChatService>();

                    token.ThrowIfCancellationRequested();
                    _serviceProvider.GetRequiredService<Plugin>();

                    PluginLog.Information("Async init complete");
                }
                catch (Exception e)
                {
                    PluginLog.Error(e, "Async load failed");
                    chatGui.PrintError($"Async loading failed: {e}");
                }
            });
        }

        public void Dispose()
        {
            _initCts.Cancel();

            // ensure we're not calling dispose recursively on ourselves
            if (_serviceProvider != null)
            {
                ServiceProvider serviceProvider = _serviceProvider;
                _serviceProvider = null;

                serviceProvider.Dispose();

                // ensure we're not keeping the file open longer than the plugin is loaded
                using (SqliteConnection sqliteConnection = new(_sqliteConnectionString))
                    SqliteConnection.ClearPool(sqliteConnection);
            }
        }
    }
}
