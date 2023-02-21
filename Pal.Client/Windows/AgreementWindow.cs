﻿using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;
using ECommons;
using ImGuiNET;
using System.Numerics;
using Pal.Client.Extensions;
using Pal.Client.Properties;

namespace Pal.Client.Windows
{
    internal class AgreementWindow : Window, ILanguageChanged
    {
        private const string WindowId = "###PalPalaceAgreement";
        private int _choice;

        public AgreementWindow() : base(WindowId)
        {
            LanguageChanged();

            Flags = ImGuiWindowFlags.NoCollapse;
            Size = new Vector2(500, 500);
            SizeCondition = ImGuiCond.FirstUseEver;
            PositionCondition = ImGuiCond.FirstUseEver;
            Position = new Vector2(310, 310);

            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(500, 500),
                MaximumSize = new Vector2(2000, 2000),
            };
        }

        public void LanguageChanged()
            => WindowName = $"{Localization.Palace_Pal}{WindowId}";

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

            PalImGui.RadioButtonWrapped(Localization.Config_UploadMyDiscoveries_ShowOtherTraps, ref _choice, (int)Configuration.EMode.Online);
            PalImGui.RadioButtonWrapped(Localization.Config_NeverUploadDiscoveries_ShowMyTraps, ref _choice, (int)Configuration.EMode.Offline);

            ImGui.Separator();

            ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudRed);
            ImGui.TextWrapped(Localization.Agreement_Warning1);
            ImGui.TextWrapped(Localization.Agreement_Warning2);
            ImGui.TextWrapped(Localization.Agreement_Warning3);
            ImGui.PopStyleColor();

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
                GenericHelpers.ShellStart("https://github.com/carvelli/PalPalace");
        }
    }
}
