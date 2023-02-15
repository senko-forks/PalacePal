using System.Collections.Generic;
using System.Numerics;
using Pal.Client.Configuration;

namespace Pal.Client.Rendering
{
    internal sealed class RenderAdapter : IRenderer
    {
        private readonly SimpleRenderer _simpleRenderer;
        private readonly SplatoonRenderer _splatoonRenderer;
        private readonly IPalacePalConfiguration _configuration;

        public RenderAdapter(SimpleRenderer simpleRenderer, SplatoonRenderer splatoonRenderer, IPalacePalConfiguration configuration)
        {
            _simpleRenderer = simpleRenderer;
            _splatoonRenderer = splatoonRenderer;
            _configuration = configuration;
        }

        public IRenderer Implementation => _configuration.Renderer.SelectedRenderer == ERenderer.Splatoon
            ? _splatoonRenderer
            : _simpleRenderer;

        public void SetLayer(ELayer layer, IReadOnlyList<IRenderElement> elements)
            => Implementation.SetLayer(layer, elements);

        public void ResetLayer(ELayer layer)
            => Implementation.ResetLayer(layer);

        public IRenderElement CreateElement(Marker.EType type, Vector3 pos, uint color, bool fill = false)
            => Implementation.CreateElement(type, pos, color, fill);
    }
}
