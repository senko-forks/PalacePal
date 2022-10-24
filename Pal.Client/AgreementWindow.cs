using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;
using ECommons;
using ImGuiNET;
using System.Numerics;

namespace Pal.Client
{
    internal class AgreementWindow : Window
    {
        private int _choice;

        public AgreementWindow() : base("Palace Pal###PalPalaceAgreement")
        {
            Flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse;
            Size = new Vector2(500, 500);
            SizeCondition = ImGuiCond.Always;
            PositionCondition = ImGuiCond.Always;
            Position = new Vector2(310, 310);
        }

        public override void OnOpen()
        {
            _choice = -1;
        }

        public override void Draw()
        {
            var config = Service.Configuration;

            ImGui.TextWrapped("Pal Palace will show you via Splatoon overlays where potential trap & hoard coffer locations are.");
            ImGui.TextWrapped("To do this, using a pomander to reveal trap or treasure chest locations will save the position of what you see.");

            ImGui.Spacing();

            ImGui.TextWrapped("Ideally, we want to discover every potential trap and chest location in the game, but doing this alone is very tedious. Floor 51-60 has over 100 trap locations and over 50 coffer locations, the last of which took over 50 runs to find - and we don't know if that map is complete. Higher floors naturally see fewer runs, making solo attempts to map the place much harder.");
            ImGui.TextWrapped("You can decide whether you want to share traps and chests you find with the community, which likewise also will let you see chests and coffers found by other players. This can be changed at any time. No data regarding your FFXIV character or account is ever sent to our server.");
           
            ImGui.RadioButton("Upload my discoveries, show traps & coffers other players have discovered", ref _choice, (int)Configuration.EMode.Online);
            ImGui.RadioButton("Never upload discoveries, show only traps and coffers I found myself", ref _choice, (int)Configuration.EMode.Offline);
      
            ImGui.Separator();

            ImGui.TextColored(ImGuiColors.DalamudRed, "While this is not an automation feature, you're still very likely to break the ToS.");
            ImGui.TextColored(ImGuiColors.DalamudRed, "Other players in your party can always see where you're standing/walking.");
            ImGui.TextColored(ImGuiColors.DalamudRed, "As such, please avoid mentioning it in-game and do not share videos/screenshots.");

            ImGui.Separator();

            if (_choice == -1)
                ImGui.TextDisabled("Please chose one of the options above.");
            ImGui.BeginDisabled(_choice == -1);
            if (ImGui.Button("I understand I'm using this plugin on my own risk."))
            {
                config.Mode = (Configuration.EMode)_choice;
                config.FirstUse = false;
                config.Save();

                IsOpen = false;
            }
            ImGui.EndDisabled();

            ImGui.Separator();

            if (ImGui.Button("View plugin & server source code"))
                GenericHelpers.ShellStart("https://github.com/LizaCarvbelli/PalPalace");
        }
    }
}
