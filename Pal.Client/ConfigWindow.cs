using Dalamud.Interface.Windowing;
using ECommons.Automation;
using ECommons.DalamudServices;
using ECommons.SplatoonAPI;
using ImGuiNET;
using ImGuizmoNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Pal.Client
{
    internal class ConfigWindow : Window
    {
        private int _mode;
        private bool _showTraps;
        private Vector4 _trapColor;
        private bool _showHoard;
        private Vector4 _hoardColor;
        private string _connectionText;

        public ConfigWindow() : base("Pal Palace - Configuration###PalPalaceConfig")
        {
            Size = new Vector2(500, 400);
            SizeCondition = ImGuiCond.FirstUseEver;
            Position = new Vector2(300, 300);
            PositionCondition = ImGuiCond.FirstUseEver;
        }

        public override void OnOpen()
        {
            var config = Service.Configuration;
            _mode = (int)config.Mode;
            _showTraps = config.ShowTraps;
            _trapColor = config.TrapColor;
            _showHoard = config.ShowHoard;
            _hoardColor = config.HoardColor;
            _connectionText = null;
        }

        public override void Draw()
        {
            bool save = false;
            bool saveAndClose = false;
            if (ImGui.BeginTabBar("PalTabs"))
            {
                if (ImGui.BeginTabItem("PotD/HoH"))
                {
                    ImGui.Checkbox("Show traps", ref _showTraps);
                    ImGui.Indent();
                    ImGui.BeginDisabled(!_showTraps);
                    ImGui.Spacing();
                    ImGui.ColorEdit4("Trap color", ref _trapColor, ImGuiColorEditFlags.NoInputs);
                    ImGui.EndDisabled();
                    ImGui.Unindent();

                    ImGui.Separator();

                    ImGui.Checkbox("Show hoard coffers", ref _showHoard);
                    ImGui.Indent();
                    ImGui.BeginDisabled(!_showHoard);
                    ImGui.Spacing();
                    ImGui.ColorEdit4("Hoard Coffer color", ref _hoardColor, ImGuiColorEditFlags.NoInputs);
                    ImGui.EndDisabled();
                    ImGui.Unindent();

                    ImGui.Separator();

                    save = ImGui.Button("Save");
                    ImGui.SameLine();
                    saveAndClose = ImGui.Button("Save & Close");

                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Community"))
                {
                    ImGui.TextWrapped("Ideally, we want to discover every potential trap and chest location in the game, but doing this alone is very tedious. Floor 51-60 has over 100 trap locations and over 50 coffer locations, the last of which took over 50 runs to find - and we don't know if that map is complete. Higher floors naturally see fewer runs, making solo attempts to map the place much harder.");
                    ImGui.TextWrapped("You can decide whether you want to share traps and chests you find with the community, which likewise also will let you see chests and coffers found by other players. This can be changed at any time. No data regarding your FFXIV character or account is ever sent to our server.");

                    ImGui.RadioButton("Upload my discoveries, show traps & coffers other players have discovered", ref _mode, (int)Configuration.EMode.Online);
                    ImGui.RadioButton("Never upload discoveries, show only traps and coffers I found myself", ref _mode, (int)Configuration.EMode.Offline);
                    saveAndClose = ImGui.Button("Save & Close");

                    ImGui.Separator();

                    ImGui.BeginDisabled(Service.Configuration.Mode != Configuration.EMode.Online);
                    if (ImGui.Button("Test Connection"))
                    {
                        Task.Run(async () =>
                        {
                            _connectionText = "Testing...";
                            try
                            {
                                _connectionText = await Service.RemoteApi.VerifyConnection();
                            }
                            catch (Exception e)
                            {
                                _connectionText = e.ToString();
                            }
                        });
                    }

                    if (_connectionText != null)
                        ImGui.Text(_connectionText);

                    ImGui.EndDisabled();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Debug"))
                {
                    var plugin = Service.Plugin;
                    if (plugin.IsInPotdOrHoh())
                    {
                        ImGui.Text($"You are in a deep dungeon, territory type {plugin.LastTerritory}.");
                        ImGui.Text($"Sync State = {plugin.TerritorySyncState}");
                        ImGui.Text($"{plugin.DebugMessage}");

                        ImGui.Indent();
                        if (plugin.FloorMarkers.TryGetValue(plugin.LastTerritory, out var currentFloorMarkers))
                        {
                            if (_showTraps)
                                ImGui.Text($"{currentFloorMarkers.Count(x => x != null && x.Type == Palace.ObjectType.Trap)} known traps");
                            if (_showHoard)
                                ImGui.Text($"{currentFloorMarkers.Count(x => x != null && x.Type == Palace.ObjectType.Hoard)} known hoard coffers");

                            foreach (var m in currentFloorMarkers)
                            {
                                var dup = currentFloorMarkers.FirstOrDefault(x => !ReferenceEquals(x, m) && x.GetHashCode() == m.GetHashCode());
                                if (dup != null)
                                    ImGui.Text($"{m.Type} {m.Position} // {dup.Type} {dup.Position}");

                            }
                        }
                        else
                            ImGui.Text("Could not query current trap/coffer count.");
                        ImGui.Unindent();
                    }
                    else
                        ImGui.Text("You are NOT in a deep dungeon.");

                    ImGui.Separator();

                    if (ImGui.Button("Draw trap & coffer circles around self"))
                    {
                        try
                        {
                            var pos = Service.ClientState.LocalPlayer.Position;
                            var elements = new List<Element>
                            {
                                Plugin.CreateSplatoonElement(Palace.ObjectType.Trap, pos, _trapColor),
                                Plugin.CreateSplatoonElement(Palace.ObjectType.Hoard, pos, _hoardColor),
                            };

                            if (!Splatoon.AddDynamicElements("PalacePal.Test", elements.ToArray(), new long[] { Environment.TickCount64 + 10000 }))
                            {
                                Service.Chat.PrintError("Could not draw markers :(");
                            }
                        }
                        catch (Exception)
                        {
                            Service.Chat.PrintError("Could not draw markers, is Splatoon installed and enabled?");
                        }
                    }

                    ImGui.EndTabItem();
                }

                ImGui.EndTabBar();
            }

            if (save || saveAndClose)
            {
                var config = Service.Configuration;
                config.Mode = (Configuration.EMode)_mode;
                config.ShowTraps = _showTraps;
                config.TrapColor = _trapColor;
                config.ShowHoard = _showHoard;
                config.HoardColor = _hoardColor;
                config.Save();

                if (saveAndClose)
                    IsOpen = false;
            }
        }
    }
}
