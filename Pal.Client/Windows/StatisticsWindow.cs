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

namespace Pal.Client.Windows
{
    internal class StatisticsWindow : Window
    {
        private SortedDictionary<ETerritoryType, TerritoryStatistics> _territoryStatistics = new();

        public StatisticsWindow() : base("Palace Pal - Statistics###PalacePalStats")
        {
            Size = new Vector2(500, 500);
            SizeCondition = ImGuiCond.FirstUseEver;

            foreach (ETerritoryType territory in typeof(ETerritoryType).GetEnumValues())
            {
                _territoryStatistics[territory] = new TerritoryStatistics(territory.ToString());
            }
        }

        public override void Draw()
        {
            DrawDungeonStats("Palace of the Dead", ETerritoryType.Palace_1_10, ETerritoryType.Palace_191_200);
            DrawDungeonStats("Heaven on High", ETerritoryType.HeavenOnHigh_1_10, ETerritoryType.HeavenOnHigh_91_100);
        }

        private void DrawDungeonStats(string name, ETerritoryType minTerritory, ETerritoryType maxTerritory)
        {

            if (ImGui.CollapsingHeader(name, ImGuiTreeNodeFlags.DefaultOpen))
            {
                if (ImGui.BeginTable($"TrapHoardStatistics{name}", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.Resizable))
                {
                    ImGui.TableSetupColumn("Id");
                    ImGui.TableSetupColumn("Instance");
                    ImGui.TableSetupColumn("Traps");
                    ImGui.TableSetupColumn("Hoard");
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
