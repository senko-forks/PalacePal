using Dalamud.Game.Gui;
using Dalamud.Interface;
using Dalamud.Plugin;
using ECommons.ExcelServices.TerritoryEnumeration;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Xml.Linq;

namespace Pal.Client.Rendering
{
    /// <summary>
    /// Simple renderer that only draws basic stuff. 
    /// 
    /// This is based on what SliceIsRight uses, and what PalacePal used before it was
    /// remade into PalacePal (which is the third or fourth iteration on the same idea
    /// I made, just with a clear vision).
    /// </summary>
    internal class SimpleRenderer : IRenderer, IDisposable
    {
        private ConcurrentDictionary<ELayer, SimpleLayer> layers = new();

        public void SetLayer(ELayer layer, IReadOnlyList<IRenderElement> elements)
        {
            layers[layer] = new SimpleLayer
            {
                TerritoryType = Service.ClientState.TerritoryType,
                Elements = elements.Cast<SimpleElement>().ToList()
            };
        }

        public void ResetLayer(ELayer layer)
        {
            if (layers.Remove(layer, out var l))
                l.Dispose();
        }

        public IRenderElement CreateElement(Marker.EType type, Vector3 pos, uint color, bool fill = false)
        {
            var config = MarkerConfig.ForType(type);
            return new SimpleElement
            {
                Type = type,
                Position = pos + new Vector3(0, config.OffsetY, 0),
                Color = color,
                Radius = config.Radius,
                Fill = fill,
            };
        }

        public void DrawLayers()
        {
            if (layers.Count == 0)
                return;

            ImGuiHelpers.ForceNextWindowMainViewport();
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
            ImGuiHelpers.SetNextWindowPosRelativeMainViewport(Vector2.Zero, ImGuiCond.None, Vector2.Zero);
            ImGui.SetNextWindowSize(ImGuiHelpers.MainViewport.Size);
            if (ImGui.Begin("###PalacePalSimpleRender", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.AlwaysUseWindowPadding))
            {
                ushort territoryType = Service.ClientState.TerritoryType;

                foreach (var layer in layers.Values.Where(l => l.TerritoryType == territoryType))
                    layer.Draw();

                foreach (var key in layers.Where(l => l.Value.TerritoryType != territoryType).Select(l => l.Key).ToList())
                    ResetLayer(key);

                ImGui.End();
            }
            ImGui.PopStyleVar();
        }

        public void Dispose()
        {
            foreach (var l in layers.Values)
                l.Dispose();
        }

        public class SimpleLayer : IDisposable
        {
            public required ushort TerritoryType { get; init; }
            public required IReadOnlyList<SimpleElement> Elements { get; set; }

            public void Draw()
            {
                foreach (var element in Elements)
                    element.Draw();
            }

            public void Dispose()
            {
                foreach (var e in Elements)
                    e.IsValid = false;
            }
        }

        public class SimpleElement : IRenderElement
        {
            private const int segmentCount = 20;

            public bool IsValid { get; set; } = true;
            public required Marker.EType Type { get; set; }
            public required Vector3 Position { get; set; }
            public required uint Color { get; set; }
            public required float Radius { get; set; }
            public required bool Fill { get; set; }

            public void Draw()
            {
                if (Color == Plugin.COLOR_INVISIBLE)
                    return;

                switch (Type)
                {
                    case Marker.EType.Hoard:
                        // ignore distance if this is a found hoard coffer
                        if (Service.Plugin.PomanderOfIntuition == Plugin.PomanderState.Active && Service.Configuration.OnlyVisibleHoardAfterPomander)
                            break;

                        goto case Marker.EType.Trap;

                    case Marker.EType.Trap:
                        var playerPos = Service.ClientState.LocalPlayer?.Position;
                        if (playerPos == null)
                            return;

                        if ((playerPos.Value - Position).Length() > 65)
                            return;
                        break;
                }

                bool onScreen = false;
                for (int index = 0; index < 2 * segmentCount; ++index)
                {
                    onScreen |= Service.GameGui.WorldToScreen(new Vector3(
                        Position.X + Radius * (float)Math.Sin(Math.PI / segmentCount * index),
                        Position.Y,
                        Position.Z + Radius * (float)Math.Cos(Math.PI / segmentCount * index)),
                        out Vector2 vector2);

                    ImGui.GetWindowDrawList().PathLineTo(vector2);
                }

                if (onScreen)
                {
                    if (Fill)
                        ImGui.GetWindowDrawList().PathFillConvex(Color);
                    else
                        ImGui.GetWindowDrawList().PathStroke(Color, ImDrawFlags.Closed, 2);
                }
                else
                    ImGui.GetWindowDrawList().PathClear();
            }
        }
    }
}
