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
                _territoryStatistics[territory] = new TerritoryStatistics { TerritoryName = territory.ToString() };
            }
        }

        public override void Draw()
        {
            if (ImGui.CollapsingHeader("Discovered Traps & Coffers per Instance", ImGuiTreeNodeFlags.DefaultOpen))
            {
                if (ImGui.BeginTable("TrapHoardStatistics", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.Resizable))
                {
                    ImGui.TableSetupColumn("Id");
                    ImGui.TableSetupColumn("Instance");
                    ImGui.TableSetupColumn("Traps");
                    ImGui.TableSetupColumn("Hoard");
                    ImGui.TableHeadersRow();

                    foreach (var (territoryType, stats) in _territoryStatistics)
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
                if (_territoryStatistics.TryGetValue((ETerritoryType)floor.TerritoryType, out TerritoryStatistics territoryStatistics))
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
        }
    }
}
