using Dalamud.Logging;
using Dalamud.Plugin;
using ECommons;
using ECommons.Reflection;
using ECommons.Schedulers;
using ECommons.SplatoonAPI;
using ImGuiNET;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Pal.Client.Rendering
{
    internal class SplatoonRenderer : IRenderer, IDrawDebugItems, IDisposable
    {
        private const long OnTerritoryChange = -2;
        private bool IsDisposed { get; set; }

        public SplatoonRenderer(DalamudPluginInterface pluginInterface, IDalamudPlugin plugin)
        {
            ECommonsMain.Init(pluginInterface, plugin, ECommons.Module.SplatoonAPI);
        }

        public void SetLayer(ELayer layer, IReadOnlyList<IRenderElement> elements)
        {
            // we need to delay this, as the current framework update could be before splatoon's, in which case it would immediately delete the layout
            _ = new TickScheduler(delegate
            {
                try
                {
                    Splatoon.AddDynamicElements(ToLayerName(layer), elements.Cast<SplatoonElement>().Select(x => x.Delegate).ToArray(), new[] { Environment.TickCount64 + 60 * 60 * 1000, OnTerritoryChange });
                }
                catch (Exception e)
                {
                    PluginLog.Error(e, $"Could not create splatoon layer {layer} with {elements.Count} elements");
                    Service.Plugin.DebugMessage = $"{DateTime.Now}\n{e}";
                }
            });
        }

        public void ResetLayer(ELayer layer)
        {
            try
            {
                Splatoon.RemoveDynamicElements(ToLayerName(layer));
            }
            catch (Exception e)
            {
                PluginLog.Error(e, $"Could not reset splatoon layer {layer}");
            }
        }

        private string ToLayerName(ELayer layer)
            => $"PalacePal.{layer}";

        public IRenderElement CreateElement(Marker.EType type, Vector3 pos, uint color, bool fill = false)
        {
            MarkerConfig config = MarkerConfig.ForType(type);
            Element element = new Element(ElementType.CircleAtFixedCoordinates)
            {
                refX = pos.X,
                refY = pos.Z, // z and y are swapped
                refZ = pos.Y,
                offX = 0,
                offY = 0,
                offZ = config.OffsetY,
                Filled = fill,
                radius = config.Radius,
                FillStep = 1,
                color = color,
                thicc = 2,
            };
            return new SplatoonElement(this, element);
        }

        public void DrawDebugItems(Vector4 trapColor, Vector4 hoardColor)
        {
            try
            {
                Vector3? pos = Service.ClientState.LocalPlayer?.Position;
                if (pos != null)
                {
                    var elements = new List<IRenderElement>
                    {
                        CreateElement(Marker.EType.Trap, pos.Value, ImGui.ColorConvertFloat4ToU32(trapColor)),
                        CreateElement(Marker.EType.Hoard, pos.Value, ImGui.ColorConvertFloat4ToU32(hoardColor)),
                    };

                    if (!Splatoon.AddDynamicElements("PalacePal.Test", elements.Cast<SplatoonElement>().Select(x => x.Delegate).ToArray(), new[] { Environment.TickCount64 + 10000 }))
                    {
                        Service.Chat.PrintError("Could not draw markers :(");
                    }
                }
            }
            catch (Exception)
            {
                try
                {
                    var pluginManager = DalamudReflector.GetPluginManager();
                    IList installedPlugins = pluginManager.GetType().GetProperty("InstalledPlugins")?.GetValue(pluginManager) as IList ?? new List<object>();

                    foreach (var t in installedPlugins)
                    {
                        AssemblyName? assemblyName = (AssemblyName?)t.GetType().GetProperty("AssemblyName")?.GetValue(t);
                        string? pluginName = (string?)t.GetType().GetProperty("Name")?.GetValue(t);
                        if (assemblyName?.Name == "Splatoon" && pluginName != "Splatoon")
                        {
                            Service.Chat.PrintError($"[Palace Pal] Splatoon is installed under the plugin name '{pluginName}', which is incompatible with the Splatoon API.");
                            Service.Chat.Print("[Palace Pal] You need to install Splatoon from the official repository at https://github.com/NightmareXIV/MyDalamudPlugins.");
                            return;
                        }
                    }
                }
                catch (Exception) { }

                Service.Chat.PrintError("Could not draw markers, is Splatoon installed and enabled?");
            }
        }

        public void Dispose()
        {
            IsDisposed = true;

            ResetLayer(ELayer.TrapHoard);
            ResetLayer(ELayer.RegularCoffers);

            ECommonsMain.Dispose();
        }

        public class SplatoonElement : IRenderElement
        {
            private readonly SplatoonRenderer _renderer;

            public SplatoonElement(SplatoonRenderer renderer, Element element)
            {
                _renderer = renderer;
                Delegate = element;
            }

            public Element Delegate { get; }

            public bool IsValid => !_renderer.IsDisposed && Delegate.IsValid();
            public uint Color
            {
                get => Delegate.color;
                set => Delegate.color = value;
            }
        }
    }
}
