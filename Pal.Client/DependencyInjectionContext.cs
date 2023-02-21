using System.Globalization;
using System.IO;
using System.Threading;
using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pal.Client.Commands;
using Pal.Client.Configuration;
using Pal.Client.Configuration.Legacy;
using Pal.Client.Database;
using Pal.Client.DependencyInjection;
using Pal.Client.DependencyInjection.Logging;
using Pal.Client.Floors;
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
        public static DalamudLoggerProvider LoggerProvider { get; } = new();

        /// <summary>
        /// Initialized as temporary logger, will be overriden once context is ready with a logger that supports scopes.
        /// </summary>
        private readonly ILogger _logger = LoggerProvider.CreateLogger<DependencyInjectionContext>();

        private readonly string _sqliteConnectionString;
        private readonly CancellationTokenSource _initCts = new();
        private ServiceProvider? _serviceProvider;
        private Plugin? _plugin;

        public string Name => Localization.Palace_Pal;

        public DependencyInjectionContext(
            DalamudPluginInterface pluginInterface,
            ClientState clientState,
            GameGui gameGui,
            ChatGui chatGui,
            ObjectTable objectTable,
            Framework framework,
            Condition condition,
            CommandManager commandManager,
            DataManager dataManager)
        {
            _logger.LogInformation("Building service container for {Assembly}",
                typeof(DependencyInjectionContext).Assembly.FullName);

            // set up legacy services
#pragma warning disable CS0612
            JsonFloorState.SetContextProperties(pluginInterface.GetPluginConfigDirectory());
#pragma warning restore CS0612

            // set up logging
            IServiceCollection services = new ServiceCollection();
            services.AddLogging(builder =>
                builder.AddFilter("Pal", LogLevel.Trace)
                    .AddFilter("Microsoft.EntityFrameworkCore.Database", LogLevel.Warning)
                    .AddFilter("Grpc", LogLevel.Debug)
                    .ClearProviders()
                    .AddProvider(LoggerProvider));

            // dalamud
            services.AddSingleton<IDalamudPlugin>(this);
            services.AddSingleton(pluginInterface);
            services.AddSingleton(clientState);
            services.AddSingleton(gameGui);
            services.AddSingleton(chatGui);
            services.AddSingleton<Chat>();
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
            services.AddTransient<JsonMigration>();

            // plugin-specific
            services.AddScoped<DependencyInjectionLoader>();
            services.AddScoped<DebugState>();
            services.AddScoped<Hooks>();
            services.AddScoped<RemoteApi>();
            services.AddScoped<ConfigurationManager>();
            services.AddScoped<IPalacePalConfiguration>(sp => sp.GetRequiredService<ConfigurationManager>().Load());
            services.AddTransient<RepoVerification>();

            // commands
            services.AddScoped<PalConfigCommand>();
            services.AddScoped<PalNearCommand>();
            services.AddScoped<PalStatsCommand>();
            services.AddScoped<PalTestConnectionCommand>();

            // territory & marker related services
            services.AddScoped<TerritoryState>();
            services.AddScoped<FrameworkService>();
            services.AddScoped<ChatService>();
            services.AddScoped<FloorService>();
            services.AddScoped<ImportService>();

            // windows & related services
            services.AddScoped<AgreementWindow>();
            services.AddScoped<ConfigWindow>();
            services.AddScoped<StatisticsService>();
            services.AddScoped<StatisticsWindow>();

            // rendering
            services.AddScoped<SimpleRenderer>();
            services.AddScoped<SplatoonRenderer>();
            services.AddScoped<RenderAdapter>();

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
            _logger = _serviceProvider.GetRequiredService<ILogger<DependencyInjectionContext>>();
            _logger.LogInformation("Service container built, creating plugin");
            _plugin = new Plugin(pluginInterface, _serviceProvider, _initCts.Token);
        }

        public void Dispose()
        {
            _initCts.Cancel();

            // ensure we're not calling dispose recursively on ourselves
            if (_serviceProvider != null)
            {
                _logger.LogInformation("Disposing DI Context");

                ServiceProvider serviceProvider = _serviceProvider;
                _serviceProvider = null;

                _plugin?.Dispose();
                _plugin = null;
                serviceProvider.Dispose();

                // ensure we're not keeping the file open longer than the plugin is loaded
                using (SqliteConnection sqliteConnection = new(_sqliteConnectionString))
                    SqliteConnection.ClearPool(sqliteConnection);
            }
            else
            {
                _logger.LogDebug("DI context is already disposed");
            }
        }
    }
}
