using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Pal.Client.Rendering;
using Pal.Client.Windows;
using System;
using System.Globalization;
using System.Linq;
using Dalamud.Logging;
using Pal.Client.Properties;
using ECommons;
using Microsoft.Extensions.DependencyInjection;
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
        private readonly IServiceProvider _serviceProvider;
        private readonly DalamudPluginInterface _pluginInterface;
        private readonly IPalacePalConfiguration _configuration;
        private readonly RenderAdapter _renderAdapter;

        public Plugin(
            IServiceProvider serviceProvider,
            DalamudPluginInterface pluginInterface,
            IPalacePalConfiguration configuration,
            RenderAdapter renderAdapter)
        {
            PluginLog.Information("Initializing Palace Pal");

            _serviceProvider = serviceProvider;
            _pluginInterface = pluginInterface;
            _configuration = configuration;
            _renderAdapter = renderAdapter;

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
            Localization.Culture = new CultureInfo(languageCode);
            _serviceProvider.GetRequiredService<WindowSystem>().Windows.OfType<ILanguageChanged>()
                .Each(w => w.LanguageChanged());
        }

        private void Draw()
        {
            _renderAdapter.DrawLayers();
            _serviceProvider.GetRequiredService<WindowSystem>().Draw();
        }
    }
}
