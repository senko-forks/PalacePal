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
using Microsoft.Extensions.Logging;
using Pal.Client.Commands;
using Pal.Client.Configuration;
using Pal.Client.Configuration.Legacy;
using Pal.Client.Database;
using Pal.Client.DependencyInjection;
using Pal.Client.DependencyInjection.Logging;
using Pal.Client.Extensions;
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
            _logger.LogInformation("Building service container");

            // set up legacy services
#pragma warning disable CS0612
            JsonFloorState.SetContextProperties(pluginInterface.GetPluginConfigDirectory());
#pragma warning restore CS0612

            // set up logging
            CancellationToken token = _initCts.Token;
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
            _logger = _serviceProvider.GetRequiredService<ILogger<DependencyInjectionContext>>();
            _logger.LogInformation("Service container built, triggering async init");
            Task.Run(async () =>
            {
                using IDisposable? logScope = _logger.BeginScope("AsyncInit");

                Chat? chat = null;
                try
                {
                    _logger.LogInformation("Starting async init");
                    chat = _serviceProvider.GetService<Chat>();

                    // initialize database
                    await using (var scope = _serviceProvider.CreateAsyncScope())
                    {
                        _logger.LogInformation("Loading database & running migrations");
                        await using var dbContext = scope.ServiceProvider.GetRequiredService<PalClientContext>();
                        await dbContext.Database.MigrateAsync();

                        _logger.LogInformation("Completed database migrations");
                    }

                    token.ThrowIfCancellationRequested();

                    // windows that have logic to open on startup
                    _serviceProvider.GetRequiredService<AgreementWindow>();

                    // initialize components that are mostly self-contained/self-registered
                    _serviceProvider.GetRequiredService<Hooks>();
                    _serviceProvider.GetRequiredService<PalCommand>();
                    _serviceProvider.GetRequiredService<FrameworkService>();
                    _serviceProvider.GetRequiredService<ChatService>();

                    token.ThrowIfCancellationRequested();
                    _serviceProvider.GetRequiredService<Plugin>();

                    _logger.LogInformation("Async init complete");
                }
                catch (ObjectDisposedException)
                {
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Async load failed");
                    chat?.Error($"Async loading failed: {e.GetType()}: {e.Message}");
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
