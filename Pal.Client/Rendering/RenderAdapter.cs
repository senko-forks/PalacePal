using System;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pal.Client.Configuration;

namespace Pal.Client.Rendering
{
    internal sealed class RenderAdapter : IRenderer, IDisposable
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<RenderAdapter> _logger;
        private readonly IPalacePalConfiguration _configuration;

        private IServiceScope? _renderScope;

        public RenderAdapter(IServiceScopeFactory serviceScopeFactory, ILogger<RenderAdapter> logger,
            IPalacePalConfiguration configuration)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
            _configuration = configuration;

            Implementation = Recreate(null);
        }

        private IRenderer Recreate(ERenderer? currentRenderer)
        {
            ERenderer targetRenderer = _configuration.Renderer.SelectedRenderer;
            if (targetRenderer == currentRenderer)
                return Implementation;

            _renderScope?.Dispose();

            _logger.LogInformation("Selected new renderer: {Renderer}", _configuration.Renderer.SelectedRenderer);
            _renderScope = _serviceScopeFactory.CreateScope();
            if (targetRenderer == ERenderer.Splatoon)
                return _renderScope.ServiceProvider.GetRequiredService<SplatoonRenderer>();
            else
                return _renderScope.ServiceProvider.GetRequiredService<SimpleRenderer>();
        }

        public void ConfigUpdated()
        {
            Implementation = Recreate(Implementation.GetConfigValue());
        }

        public void Dispose()
            => _renderScope?.Dispose();

        public IRenderer Implementation { get; private set; }

        public void SetLayer(ELayer layer, IReadOnlyList<IRenderElement> elements)
            => Implementation.SetLayer(layer, elements);

        public void ResetLayer(ELayer layer)
            => Implementation.ResetLayer(layer);

        public IRenderElement CreateElement(Marker.EType type, Vector3 pos, uint color, bool fill = false)
            => Implementation.CreateElement(type, pos, color, fill);

        public void DrawLayers()
        {
            if (Implementation is SimpleRenderer sr)
                sr.DrawLayers();
        }

        public ERenderer GetConfigValue()
            => throw new NotImplementedException();
    }
}
