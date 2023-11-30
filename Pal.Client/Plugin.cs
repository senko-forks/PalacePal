﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.DalamudServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pal.Client.Commands;
using Pal.Client.Configuration;
using Pal.Client.DependencyInjection;
using Pal.Client.Properties;
using Pal.Client.Rendering;

namespace Pal.Client
{
    /// <summary>
    /// With all DI logic elsewhere, this plugin shell really only takes care of a few things around events that
    /// need to be sent to different receivers depending on priority or configuration .
    /// </summary>
    /// <see cref="DependencyInjectionContext"/>
    internal sealed class Plugin : IDalamudPlugin
    {
        private readonly CancellationTokenSource _initCts = new();

        private readonly DalamudPluginInterface _pluginInterface;
        private readonly ICommandManager _commandManager;
        private readonly IClientState _clientState;
        private readonly IChatGui _chatGui;
        private readonly IFramework _framework;

        private readonly TaskCompletionSource<IServiceScope> _rootScopeCompletionSource = new();
        private ELoadState _loadState = ELoadState.Initializing;

        private DependencyInjectionContext? _dependencyInjectionContext;
        private ILogger _logger = DependencyInjectionContext.LoggerProvider.CreateLogger<Plugin>();
        private WindowSystem? _windowSystem;
        private IServiceScope? _rootScope;
        private Action? _loginAction;

        public Plugin(
            DalamudPluginInterface pluginInterface,
            ICommandManager commandManager,
            IClientState clientState,
            IChatGui chatGui,
            IFramework framework)
        {
            _pluginInterface = pluginInterface;
            _commandManager = commandManager;
            _clientState = clientState;
            _chatGui = chatGui;
            _framework = framework;

            // set up the current UI language before creating anything
            Localization.Culture = new CultureInfo(_pluginInterface.UiLanguage);

            _commandManager.AddHandler("/pal", new CommandInfo(OnCommand)
            {
                HelpMessage = Localization.Command_pal_HelpText
            });

            // Using TickScheduler requires ECommons to at least be partially initialized
            // ECommonsMain.Dispose leaves this untouched.
            Svc.Init(pluginInterface);

            Task.Run(async () => await CreateDependencyContext());
        }

        public string Name => Localization.Palace_Pal;

        private async Task CreateDependencyContext()
        {
            try
            {
                _dependencyInjectionContext = _pluginInterface.Create<DependencyInjectionContext>(this)
                                              ?? throw new Exception("Could not create DI root context class");
                var serviceProvider = _dependencyInjectionContext.BuildServiceContainer();
                _initCts.Token.ThrowIfCancellationRequested();

                _logger = serviceProvider.GetRequiredService<ILogger<Plugin>>();
                _windowSystem = serviceProvider.GetRequiredService<WindowSystem>();
                _rootScope = serviceProvider.CreateScope();

                var loader = _rootScope.ServiceProvider.GetRequiredService<DependencyContextInitializer>();
                await loader.InitializeAsync(_initCts.Token);

                await _framework.RunOnFrameworkThread(() =>
                {
                    _pluginInterface.UiBuilder.Draw += Draw;
                    _pluginInterface.UiBuilder.OpenConfigUi += OpenConfigUi;
                    _pluginInterface.LanguageChanged += LanguageChanged;
                    _clientState.Login += Login;
                });
                _rootScopeCompletionSource.SetResult(_rootScope);
                _loadState = ELoadState.Loaded;
            }
            catch (Exception e) when (e is ObjectDisposedException
                                          or OperationCanceledException
                                          or RepoVerification.RepoVerificationFailedException
                                      || (e is FileLoadException && _pluginInterface.IsDev))
            {
                _rootScopeCompletionSource.SetException(e);
                _loadState = ELoadState.Error;
            }
            catch (Exception e)
            {
                _rootScopeCompletionSource.SetException(e);
                _logger.LogError(e, "Async load failed");
                ShowErrorOnLogin(() =>
                    new Chat(_chatGui).Error(string.Format(Localization.Error_LoadFailed,
                        $"{e.GetType()} - {e.Message}")));

                _loadState = ELoadState.Error;
            }
        }

        private void ShowErrorOnLogin(Action? loginAction)
        {
            if (_clientState.IsLoggedIn)
            {
                loginAction?.Invoke();
                _loginAction = null;
            }
            else
                _loginAction = loginAction;
        }

        private void Login()
        {
            _loginAction?.Invoke();
            _loginAction = null;
        }

        private void OnCommand(string command, string arguments)
        {
            arguments = arguments.Trim();

            Task.Run(async () =>
            {
                IServiceScope rootScope;
                Chat chat;

                try
                {
                    rootScope = await _rootScopeCompletionSource.Task;
                    chat = rootScope.ServiceProvider.GetRequiredService<Chat>();
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Could not wait for command root scope");
                    return;
                }

                try
                {
                    IPalacePalConfiguration configuration =
                        rootScope.ServiceProvider.GetRequiredService<IPalacePalConfiguration>();
                    if (configuration.FirstUse && arguments != "" && arguments != "config")
                    {
                        chat.Error(Localization.Error_FirstTimeSetupRequired);
                        return;
                    }

                    Action<string> commandHandler = rootScope.ServiceProvider
                        .GetRequiredService<IEnumerable<ISubCommand>>()
                        .SelectMany(cmd => cmd.GetHandlers())
                        .Where(cmd => cmd.Key == arguments.ToLowerInvariant())
                        .Select(cmd => cmd.Value)
                        .SingleOrDefault(missingCommand =>
                        {
                            chat.Error(string.Format(Localization.Command_pal_UnknownSubcommand, missingCommand,
                                command));
                        });
                    commandHandler.Invoke(arguments);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Could not execute command '{Command}' with arguments '{Arguments}'", command,
                        arguments);
                    chat.Error(string.Format(Localization.Error_CommandFailed,
                        $"{e.GetType()} - {e.Message}"));
                }
            });
        }

        private void OpenConfigUi()
            => _rootScope!.ServiceProvider.GetRequiredService<PalConfigCommand>().Execute();

        private void LanguageChanged(string languageCode)
        {
            _logger.LogInformation("Language set to '{Language}'", languageCode);

            Localization.Culture = new CultureInfo(languageCode);
            _windowSystem!.Windows.OfType<ILanguageChanged>()
                .Each(w => w.LanguageChanged());
        }

        private void Draw()
        {
            _rootScope!.ServiceProvider.GetRequiredService<RenderAdapter>().DrawLayers();
            _windowSystem!.Draw();
        }

        public void Dispose()
        {
            _commandManager.RemoveHandler("/pal");

            if (_loadState == ELoadState.Loaded)
            {
                _pluginInterface.UiBuilder.Draw -= Draw;
                _pluginInterface.UiBuilder.OpenConfigUi -= OpenConfigUi;
                _pluginInterface.LanguageChanged -= LanguageChanged;
                _clientState.Login -= Login;
            }

            _initCts.Cancel();
            _rootScope?.Dispose();
            _dependencyInjectionContext?.Dispose();
        }

        private enum ELoadState
        {
            Initializing,
            Loaded,
            Error
        }
    }
}
