using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Pal.Client.Rendering;
using Pal.Client.Windows;
using System;
using System.Globalization;
using System.Linq;
using Pal.Client.Properties;
using ECommons;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pal.Client.Configuration;

namespace Pal.Client
{
    /// <summary>
    /// With all DI logic elsewhere, this plugin shell really only takes care of a few things around events that
    /// need to be sent to different receivers depending on priority or configuration .
    /// </summary>
    /// <see cref="DependencyInjection.DependencyInjectionContext"/>
    internal sealed class Plugin : IDisposable
    {
        private readonly DalamudPluginInterface _pluginInterface;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<Plugin> _logger;
        private readonly IPalacePalConfiguration _configuration;
        private readonly RenderAdapter _renderAdapter;
        private readonly WindowSystem _windowSystem;

        public Plugin(
            DalamudPluginInterface pluginInterface,
            IServiceProvider serviceProvider)
        {
            _pluginInterface = pluginInterface;
            _serviceProvider = serviceProvider;
            _logger = _serviceProvider.GetRequiredService<ILogger<Plugin>>();
            _configuration = serviceProvider.GetRequiredService<IPalacePalConfiguration>();
            _renderAdapter = serviceProvider.GetRequiredService<RenderAdapter>();
            _windowSystem = serviceProvider.GetRequiredService<WindowSystem>();

            LanguageChanged(pluginInterface.UiLanguage);

            pluginInterface.UiBuilder.Draw += Draw;
            pluginInterface.UiBuilder.OpenConfigUi += OpenConfigUi;
            pluginInterface.LanguageChanged += LanguageChanged;
        }

        private void OpenConfigUi()
        {
            Window configWindow;
            if (_configuration.FirstUse)
                configWindow = _serviceProvider.GetRequiredService<AgreementWindow>();
            else
                configWindow = _serviceProvider.GetRequiredService<ConfigWindow>();

            configWindow.IsOpen = true;
        }

        public void Dispose()
        {
            _pluginInterface.UiBuilder.Draw -= Draw;
            _pluginInterface.UiBuilder.OpenConfigUi -= OpenConfigUi;
            _pluginInterface.LanguageChanged -= LanguageChanged;
        }

        private void LanguageChanged(string languageCode)
        {
            _logger.LogInformation("Language set to '{Language}'", languageCode);

            Localization.Culture = new CultureInfo(languageCode);
            _windowSystem.Windows.OfType<ILanguageChanged>()
                .Each(w => w.LanguageChanged());
        }

        private void Draw()
        {
            _renderAdapter.DrawLayers();
            _windowSystem.Draw();
        }
    }
}
