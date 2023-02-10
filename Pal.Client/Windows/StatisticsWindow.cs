using Dalamud.Interface.Windowing;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using Pal.Common;
using Palace;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Pal.Client.Properties;

namespace Pal.Client.Windows
{
    internal class StatisticsWindow : Window
    {
        private SortedDictionary<ETerritoryType, TerritoryStatistics> _territoryStatistics = new();

        public StatisticsWindow() : base($"{Localization.Palace_Pal} - {Localization.Statistics}###PalacePalStats")
        {
            Size = new Vector2(500, 500);
            SizeCondition = ImGuiCond.FirstUseEver;
            Flags = ImGuiWindowFlags.AlwaysAutoResize;

            foreach (ETerritoryType territory in typeof(ETerritoryType).GetEnumValues())
            {
                _territoryStatistics[territory] = new TerritoryStatistics(territory.ToString());
            }
        }

        public override void Draw()
        {
            if (ImGui.BeginTabBar("Tabs"))
            {
                DrawDungeonStats(Localization.PalaceOfTheDead, ETerritoryType.Palace_1_10, ETerritoryType.Palace_191_200);
                DrawDungeonStats(Localization.HeavenOnHigh, ETerritoryType.HeavenOnHigh_1_10, ETerritoryType.HeavenOnHigh_91_100);
            }
        }

        private void DrawDungeonStats(string name, ETerritoryType minTerritory, ETerritoryType maxTerritory)
        {
            if (ImGui.BeginTabItem(name))
            {
                if (ImGui.BeginTable($"TrapHoardStatistics{name}", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.Resizable))
                {
                    ImGui.TableSetupColumn(Localization.Statistics_TerritoryId);
                    ImGui.TableSetupColumn(Localization.Statistics_InstanceName);
                    ImGui.TableSetupColumn(Localization.Statistics_Traps);
                    ImGui.TableSetupColumn(Localization.Statistics_HoardCoffers);
                    ImGui.TableHeadersRow();

                    foreach (var (territoryType, stats) in _territoryStatistics.Where(x => x.Key >= minTerritory && x.Key <= maxTerritory))
                    {
                        ImGui.TableNextRow();
                        if (ImGui.TableNextColumn())
                            ImGui.Text($"{(uint)territoryType}");

                        if (ImGui.TableNextColumn())
                            ImGui.Text(stats.TerritoryName);

                        if (ImGui.TableNextColumn())
                            ImGui.Text(stats.TrapCount?.ToString() ?? "-");

                        if (ImGui.TableNextColumn())
                            ImGui.Text(stats.HoardCofferCount?.ToString() ?? "-");
                    }
                    ImGui.EndTable();
                }
                ImGui.EndTabItem();
            }
        }

        internal void SetFloorData(IEnumerable<FloorStatistics> floorStatistics)
        {
            foreach (var territoryStatistics in _territoryStatistics.Values)
            {
                territoryStatistics.TrapCount = null;
                territoryStatistics.HoardCofferCount = null;
            }

            foreach (var floor in floorStatistics)
            {
                if (_territoryStatistics.TryGetValue((ETerritoryType)floor.TerritoryType, out TerritoryStatistics? territoryStatistics))
                {
                    territoryStatistics.TrapCount = floor.TrapCount;
                    territoryStatistics.HoardCofferCount = floor.HoardCount;
                }
            }
        }

        private class TerritoryStatistics
        {
            public string TerritoryName { get; set; }
            public uint? TrapCount { get; set; }
            public uint? HoardCofferCount { get; set; }

            public TerritoryStatistics(string territoryName)
            {
                TerritoryName = territoryName;
            }
        }
    }
}
