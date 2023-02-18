using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pal.Client.Commands;
using Pal.Client.Configuration;
using Pal.Client.Configuration.Legacy;
using Pal.Client.Database;
using Pal.Client.DependencyInjection;
using Pal.Client.Properties;
using Pal.Client.Windows;

namespace Pal.Client
{
    /// <summary>
    /// Takes care of async plugin init - this is mostly everything that requires either the config or the database to
    /// be available.
    /// </summary>
    internal sealed class DependencyInjectionLoader
    {
        private readonly ILogger<DependencyInjectionLoader> _logger;
        private readonly IServiceProvider _serviceProvider;

        public DependencyInjectionLoader(ILogger<DependencyInjectionLoader> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        public ELoadState LoadState { get; private set; } = ELoadState.Initializing;

        public event Action<Action?>? InitCompleted;

        public async Task InitializeAsync(CancellationToken cancellationToken)
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

                    // takes 2-3 seconds with initializing connections, loading driver etc.
                    await dbContext.Database.MigrateAsync(cancellationToken);
                    _logger.LogInformation("Completed database migrations");
                }

                cancellationToken.ThrowIfCancellationRequested();

                // v1 migration: config migration for import history, json migration for markers
                _serviceProvider.GetRequiredService<ConfigurationManager>().Migrate();
                await _serviceProvider.GetRequiredService<JsonMigration>().MigrateAsync(cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                // windows that have logic to open on startup
                _serviceProvider.GetRequiredService<AgreementWindow>();

                // initialize components that are mostly self-contained/self-registered
                _serviceProvider.GetRequiredService<Hooks>();
                _serviceProvider.GetRequiredService<FrameworkService>();
                _serviceProvider.GetRequiredService<ChatService>();

                // eager load any commands to find errors now, not when running them
                _serviceProvider.GetRequiredService<PalConfigCommand>();
                _serviceProvider.GetRequiredService<PalNearCommand>();
                _serviceProvider.GetRequiredService<PalStatsCommand>();
                _serviceProvider.GetRequiredService<PalTestConnectionCommand>();

                cancellationToken.ThrowIfCancellationRequested();

                LoadState = ELoadState.Loaded;
                InitCompleted?.Invoke(null);
                _logger.LogInformation("Async init complete");
            }
            catch (ObjectDisposedException)
            {
                InitCompleted?.Invoke(null);
                LoadState = ELoadState.Error;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Async load failed");
                InitCompleted?.Invoke(() => chat?.Error(string.Format(Localization.Error_LoadFailed, $"{e.GetType()} - {e.Message}")));

                LoadState = ELoadState.Error;
            }
        }

        public enum ELoadState
        {
            Initializing,
            Loaded,
            Error
        }
    }
}
