using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Pal.Client.Rendering;
using Pal.Client.Scheduled;
using Pal.Client.Windows;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using Dalamud.Logging;
using Pal.Client.Extensions;
using Pal.Client.Properties;
using ECommons;
using Microsoft.Extensions.DependencyInjection;
using Pal.Client.Configuration;

namespace Pal.Client
{
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

            // initialize legacy services
            pluginInterface.Create<Service>();
            Service.Configuration = configuration;

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

        #region IDisposable Support
        public void Dispose()
        {
            _pluginInterface.UiBuilder.Draw -= Draw;
            _pluginInterface.UiBuilder.OpenConfigUi -= OpenConfigUi;
            _pluginInterface.LanguageChanged -= LanguageChanged;
        }
        #endregion

        private void LanguageChanged(string languageCode)
        {
            Localization.Culture = new CultureInfo(languageCode);
            _serviceProvider.GetRequiredService<WindowSystem>().Windows.OfType<ILanguageChanged>().Each(w => w.LanguageChanged());
        }

        private void Draw()
        {
            if (_renderAdapter.Implementation is SimpleRenderer sr)
                sr.DrawLayers();

            _serviceProvider.GetRequiredService<WindowSystem>().Draw();
        }
    }
}
