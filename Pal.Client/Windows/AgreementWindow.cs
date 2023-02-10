using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;
using ECommons;
using ImGuiNET;
using System.Numerics;
using Pal.Client.Properties;

namespace Pal.Client.Windows
{
    internal class AgreementWindow : Window
    {
        private int _choice;

        public AgreementWindow() : base($"{Localization.Palace_Pal}###PalPalaceAgreement")
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

            ImGui.TextWrapped(Localization.Explanation_1);
            ImGui.TextWrapped(Localization.Explanation_2);

            ImGui.Spacing();

            ImGui.TextWrapped(Localization.Explanation_3);
            ImGui.TextWrapped(Localization.Explanation_4);

            ImGui.RadioButton(Localization.Config_UploadMyDiscoveries_ShowOtherTraps, ref _choice, (int)Configuration.EMode.Online);
            ImGui.RadioButton(Localization.Config_NeverUploadDiscoveries_ShowMyTraps, ref _choice, (int)Configuration.EMode.Offline);

            ImGui.Separator();

            ImGui.TextColored(ImGuiColors.DalamudRed, Localization.Agreement_Warning1);
            ImGui.TextColored(ImGuiColors.DalamudRed, Localization.Agreement_Warning2);
            ImGui.TextColored(ImGuiColors.DalamudRed, Localization.Agreement_Warning3);

            ImGui.Separator();

            if (_choice == -1)
                ImGui.TextDisabled(Localization.Agreement_PickOneOption);
            ImGui.BeginDisabled(_choice == -1);
            if (ImGui.Button(Localization.Agreement_UsingThisOnMyOwnRisk))
            {
                config.Mode = (Configuration.EMode)_choice;
                config.FirstUse = false;
                config.Save();

                IsOpen = false;
            }
            ImGui.EndDisabled();

            ImGui.Separator();

            if (ImGui.Button(Localization.Agreement_ViewPluginAndServerSourceCode))
                GenericHelpers.ShellStart("https://github.com/LizaCarvbelli/PalPalace");
        }
    }
}
