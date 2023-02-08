using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Pal.Client.Rendering
{
    internal interface IRenderer
    {
        void SetLayer(ELayer layer, IReadOnlyList<IRenderElement> elements);

        void ResetLayer(ELayer layer);

        IRenderElement CreateElement(Marker.EType type, Vector3 pos, uint color, bool fill = false);
    }
}
