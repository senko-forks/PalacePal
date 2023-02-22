﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
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

                await RemoveOldBackups();
                await CreateBackups();
                cancellationToken.ThrowIfCancellationRequested();

                await RunMigrations(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                await RunCleanup(_logger);
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
                InitCompleted?.Invoke(() =>
                    chat?.Error(string.Format(Localization.Error_LoadFailed, $"{e.GetType()} - {e.Message}")));

                LoadState = ELoadState.Error;
            }
        }

        private async Task RemoveOldBackups()
        {
            await using var scope = _serviceProvider.CreateAsyncScope();
            var pluginInterface = scope.ServiceProvider.GetRequiredService<DalamudPluginInterface>();
            var configuration = scope.ServiceProvider.GetRequiredService<IPalacePalConfiguration>();

            var paths = Directory.GetFiles(pluginInterface.GetPluginConfigDirectory(), "backup-*.data.sqlite3",
                new EnumerationOptions
                {
                    IgnoreInaccessible = true,
                    RecurseSubdirectories = false,
                    MatchCasing = MatchCasing.CaseSensitive,
                    AttributesToSkip = FileAttributes.ReadOnly | FileAttributes.Hidden | FileAttributes.System,
                    ReturnSpecialDirectories = false,
                });
            if (paths.Length == 0)
                return;

            Regex backupRegex = new Regex(@"backup-([\d\-]{10})\.data\.sqlite3", RegexOptions.Compiled);
            List<(DateTime Date, string Path)> backupFiles = new();
            foreach (string path in paths)
            {
                var match = backupRegex.Match(Path.GetFileName(path));
                if (!match.Success)
                    continue;

                if (DateTime.TryParseExact(match.Groups[1].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal, out DateTime backupDate))
                {
                    backupFiles.Add((backupDate, path));
                }
            }

            var toDelete = backupFiles.OrderByDescending(x => x.Date)
                .Skip(configuration.Backups.MinimumBackupsToKeep)
                .Where(x => (DateTime.Today.ToUniversalTime() - x.Date).Days > configuration.Backups.DaysToDeleteAfter)
                .Select(x => x.Path);
            foreach (var path in toDelete)
            {
                try
                {
                    File.Delete(path);
                    _logger.LogInformation("Deleted old backup file '{Path}'", path);
                }
                catch (Exception e)
                {
                    _logger.LogWarning(e, "Could not delete backup file '{Path}'", path);
                }
            }
        }

        private async Task CreateBackups()
        {
            await using var scope = _serviceProvider.CreateAsyncScope();

            var pluginInterface = scope.ServiceProvider.GetRequiredService<DalamudPluginInterface>();
            string backupPath = Path.Join(pluginInterface.GetPluginConfigDirectory(),
                $"backup-{DateTime.Today.ToUniversalTime():yyyy-MM-dd}.data.sqlite3");
            if (!File.Exists(backupPath))
            {
                _logger.LogInformation("Creating database backup '{Path}'", backupPath);

                await using var db = scope.ServiceProvider.GetRequiredService<PalClientContext>();
                await using SqliteConnection source = new(db.Database.GetConnectionString());
                await source.OpenAsync();
                await using SqliteConnection backup = new($"Data Source={backupPath}");
                source.BackupDatabase(backup);
                SqliteConnection.ClearPool(backup);
            }
            else
                _logger.LogInformation("Database backup in '{Path}' already exists", backupPath);
        }

        private async Task RunMigrations(CancellationToken cancellationToken)
        {
            await using var scope = _serviceProvider.CreateAsyncScope();

            _logger.LogInformation("Loading database & running migrations");
            await using var dbContext = scope.ServiceProvider.GetRequiredService<PalClientContext>();

            // takes 2-3 seconds with initializing connections, loading driver etc.
            await dbContext.Database.MigrateAsync(cancellationToken);
            _logger.LogInformation("Completed database migrations");
        }

        private async Task RunCleanup(ILogger<DependencyInjectionLoader> logger)
        {
            await using var scope = _serviceProvider.CreateAsyncScope();
            await using var dbContext = scope.ServiceProvider.GetRequiredService<PalClientContext>();
            var cleanup = scope.ServiceProvider.GetRequiredService<Cleanup>();

            cleanup.Purge(dbContext);

            await dbContext.SaveChangesAsync();
        }


        public enum ELoadState
        {
            Initializing,
            Loaded,
            Error
        }
    }
}
