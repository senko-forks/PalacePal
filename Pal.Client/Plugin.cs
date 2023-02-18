using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Pal.Client.Rendering;
using Pal.Client.Windows;
using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.ClientState;
using Dalamud.Game.Command;
using Pal.Client.Properties;
using ECommons;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pal.Client.Commands;
using Pal.Client.Configuration;
using Pal.Client.DependencyInjection;

namespace Pal.Client
{
    /// <summary>
    /// With all DI logic elsewhere, this plugin shell really only takes care of a few things around events that
    /// need to be sent to different receivers depending on priority or configuration .
    /// </summary>
    /// <see cref="DependencyInjectionContext"/>
    internal sealed class Plugin : IDisposable
    {
        private readonly DalamudPluginInterface _pluginInterface;
        private readonly ILogger<Plugin> _logger;
        private readonly CommandManager _commandManager;
        private readonly Chat _chat;
        private readonly WindowSystem _windowSystem;
        private readonly ClientState _clientState;

        private readonly IServiceScope _rootScope;
        private readonly DependencyInjectionLoader _loader;

        private Action? _loginAction = null;

        public Plugin(
            DalamudPluginInterface pluginInterface,
            IServiceProvider serviceProvider,
            CancellationToken cancellationToken)
        {
            _pluginInterface = pluginInterface;
            _logger = serviceProvider.GetRequiredService<ILogger<Plugin>>();
            _commandManager = serviceProvider.GetRequiredService<CommandManager>();
            _chat = serviceProvider.GetRequiredService<Chat>();
            _windowSystem = serviceProvider.GetRequiredService<WindowSystem>();
            _clientState = serviceProvider.GetRequiredService<ClientState>();

            _rootScope = serviceProvider.CreateScope();
            _loader = _rootScope.ServiceProvider.GetRequiredService<DependencyInjectionLoader>();
            _loader.InitCompleted += InitCompleted;
            var _ = Task.Run(async () => await _loader.InitializeAsync(cancellationToken));

            pluginInterface.UiBuilder.Draw += Draw;
            pluginInterface.UiBuilder.OpenConfigUi += OpenConfigUi;
            pluginInterface.LanguageChanged += LanguageChanged;
            _clientState.Login += Login;

            _commandManager.AddHandler("/pal", new CommandInfo(OnCommand)
            {
                HelpMessage = Localization.Command_pal_HelpText
            });
        }

        private void InitCompleted(Action? loginAction)
        {
            LanguageChanged(_pluginInterface.UiLanguage);

            if (_clientState.IsLoggedIn)
            {
                loginAction?.Invoke();
                _loginAction = null;
            }
            else
                _loginAction = loginAction;
        }

        private void Login(object? sender, EventArgs eventArgs)
        {
            _loginAction?.Invoke();
            _loginAction = null;
        }

        private void OnCommand(string command, string arguments)
        {
            arguments = arguments.Trim();

            IPalacePalConfiguration configuration =
                _rootScope.ServiceProvider.GetRequiredService<IPalacePalConfiguration>();
            if (configuration.FirstUse && arguments != "" && arguments != "config")
            {
                _chat.Error(Localization.Error_FirstTimeSetupRequired);
                return;
            }

            try
            {
                var sp = _rootScope.ServiceProvider;

                switch (arguments)
                {
                    case "":
                    case "config":
                        sp.GetRequiredService<PalConfigCommand>().Execute();
                        break;

                    case "stats":
                        sp.GetRequiredService<PalStatsCommand>().Execute();
                        break;

                    case "tc":
                    case "test-connection":
                        sp.GetRequiredService<PalTestConnectionCommand>().Execute();
                        break;

                    case "near":
                    case "tnear":
                    case "hnear":
                        sp.GetRequiredService<PalNearCommand>().Execute(arguments);
                        break;

                    default:
                        _chat.Error(string.Format(Localization.Command_pal_UnknownSubcommand, arguments,
                            command));
                        break;
                }
            }
            catch (Exception e)
            {
                _chat.Error(e.ToString());
            }
        }

        private void OpenConfigUi()
            => _rootScope.ServiceProvider.GetRequiredService<PalConfigCommand>().Execute();

        private void LanguageChanged(string languageCode)
        {
            _logger.LogInformation("Language set to '{Language}'", languageCode);

            Localization.Culture = new CultureInfo(languageCode);
            _windowSystem.Windows.OfType<ILanguageChanged>()
                .Each(w => w.LanguageChanged());
        }

        private void Draw()
        {
            if (_loader.LoadState == DependencyInjectionLoader.ELoadState.Loaded)
            {
                _rootScope.ServiceProvider.GetRequiredService<RenderAdapter>().DrawLayers();
                _windowSystem.Draw();
            }
        }

        public void Dispose()
        {
            _commandManager.RemoveHandler("/pal");

            _pluginInterface.UiBuilder.Draw -= Draw;
            _pluginInterface.UiBuilder.OpenConfigUi -= OpenConfigUi;
            _pluginInterface.LanguageChanged -= LanguageChanged;
            _clientState.Login -= Login;

            _loader.InitCompleted -= InitCompleted;
            _rootScope.Dispose();
        }
    }
}
