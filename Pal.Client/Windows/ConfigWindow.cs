using Account;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using ECommons;
using Google.Protobuf;
using ImGuiNET;
using Pal.Client.Net;
using Pal.Client.Rendering;
using Pal.Client.Scheduled;
using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Pal.Client.Extensions;
using Pal.Client.Properties;

namespace Pal.Client.Windows
{
    internal class ConfigWindow : Window, ILanguageChanged
    {
        private const string WindowId = "###PalPalaceConfig";
        private int _mode;
        private int _renderer;
        private bool _showTraps;
        private Vector4 _trapColor;
        private bool _onlyVisibleTrapsAfterPomander;
        private bool _showHoard;
        private Vector4 _hoardColor;
        private bool _onlyVisibleHoardAfterPomander;
        private bool _showSilverCoffers;
        private Vector4 _silverCofferColor;
        private bool _fillSilverCoffers;

        private string? _connectionText;
        private bool _switchToCommunityTab;
        private string _openImportPath = string.Empty;
        private string _saveExportPath = string.Empty;
        private string? _openImportDialogStartPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        private string? _saveExportDialogStartPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        private readonly FileDialogManager _importDialog;
        private readonly FileDialogManager _exportDialog;

        private CancellationTokenSource? _testConnectionCts;

        public ConfigWindow() : base(WindowId)
        {
            LanguageChanged();

            Size = new Vector2(500, 400);
            SizeCondition = ImGuiCond.FirstUseEver;
            Position = new Vector2(300, 300);
            PositionCondition = ImGuiCond.FirstUseEver;

            _importDialog = new FileDialogManager { AddedWindowFlags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoDocking };
            _exportDialog = new FileDialogManager { AddedWindowFlags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoDocking };
        }

        public void LanguageChanged()
        {
            var version = typeof(Plugin).Assembly.GetName().Version!.ToString(2);
            WindowName = $"{Localization.Palace_Pal} v{version}{WindowId}";
        }

        public override void OnOpen()
        {
            var config = Service.Configuration;
            _mode = (int)config.Mode;
            _renderer = (int)config.Renderer;
            _showTraps = config.ShowTraps;
            _trapColor = config.TrapColor;
            _onlyVisibleTrapsAfterPomander = config.OnlyVisibleTrapsAfterPomander;
            _showHoard = config.ShowHoard;
            _hoardColor = config.HoardColor;
            _onlyVisibleHoardAfterPomander = config.OnlyVisibleHoardAfterPomander;
            _showSilverCoffers = config.ShowSilverCoffers;
            _silverCofferColor = config.SilverCofferColor;
            _fillSilverCoffers = config.FillSilverCoffers;
            _connectionText = null;
        }

        public override void OnClose()
        {
            _importDialog.Reset();
            _exportDialog.Reset();
            _testConnectionCts?.Cancel();
            _testConnectionCts = null;
        }

        public override void Draw()
        {
            bool save = false;
            bool saveAndClose = false;
            if (ImGui.BeginTabBar("PalTabs"))
            {
                DrawDeepDungeonItemsTab(ref save, ref saveAndClose);
                DrawCommunityTab(ref saveAndClose);
                DrawImportTab();
                DrawExportTab();
                DrawRenderTab(ref save, ref saveAndClose);
                DrawDebugTab();

                ImGui.EndTabBar();
            }

            _importDialog.Draw();

            if (save || saveAndClose)
            {
                var config = Service.Configuration;
                config.Mode = (Configuration.EMode)_mode;
                config.Renderer = (Configuration.ERenderer)_renderer;
                config.ShowTraps = _showTraps;
                config.TrapColor = _trapColor;
                config.OnlyVisibleTrapsAfterPomander = _onlyVisibleTrapsAfterPomander;
                config.ShowHoard = _showHoard;
                config.HoardColor = _hoardColor;
                config.OnlyVisibleHoardAfterPomander = _onlyVisibleHoardAfterPomander;
                config.ShowSilverCoffers = _showSilverCoffers;
                config.SilverCofferColor = _silverCofferColor;
                config.FillSilverCoffers = _fillSilverCoffers;
                config.Save();

                if (saveAndClose)
                    IsOpen = false;
            }
        }

        private void DrawDeepDungeonItemsTab(ref bool save, ref bool saveAndClose)
        {
            if (ImGui.BeginTabItem($"{Localization.ConfigTab_DeepDungeons}###TabDeepDungeons"))
            {
                ImGui.Checkbox(Localization.Config_Traps_Show, ref _showTraps);
                ImGui.Indent();
                ImGui.BeginDisabled(!_showTraps);
                ImGui.Spacing();
                ImGui.ColorEdit4(Localization.Config_Traps_Color, ref _trapColor, ImGuiColorEditFlags.NoInputs);
                ImGui.Checkbox(Localization.Config_Traps_HideImpossible, ref _onlyVisibleTrapsAfterPomander);
                ImGui.SameLine();
                ImGuiComponents.HelpMarker(Localization.Config_Traps_HideImpossible_ToolTip);
                ImGui.EndDisabled();
                ImGui.Unindent();

                ImGui.Separator();

                ImGui.Checkbox(Localization.Config_HoardCoffers_Show, ref _showHoard);
                ImGui.Indent();
                ImGui.BeginDisabled(!_showHoard);
                ImGui.Spacing();
                ImGui.ColorEdit4(Localization.Config_HoardCoffers_Color, ref _hoardColor, ImGuiColorEditFlags.NoInputs);
                ImGui.Checkbox(Localization.Config_HoardCoffers_HideImpossible, ref _onlyVisibleHoardAfterPomander);
                ImGui.SameLine();
                ImGuiComponents.HelpMarker(Localization.Config_HoardCoffers_HideImpossible_ToolTip);
                ImGui.EndDisabled();
                ImGui.Unindent();

                ImGui.Separator();

                ImGui.Checkbox(Localization.Config_SilverCoffer_Show, ref _showSilverCoffers);
                ImGuiComponents.HelpMarker(Localization.Config_SilverCoffers_ToolTip);
                ImGui.Indent();
                ImGui.BeginDisabled(!_showSilverCoffers);
                ImGui.Spacing();
                ImGui.ColorEdit4(Localization.Config_SilverCoffer_Color, ref _silverCofferColor, ImGuiColorEditFlags.NoInputs);
                ImGui.Checkbox(Localization.Config_SilverCoffer_Filled, ref _fillSilverCoffers);
                ImGui.EndDisabled();
                ImGui.Unindent();

                ImGui.Separator();

                save = ImGui.Button(Localization.Save);
                ImGui.SameLine();
                saveAndClose = ImGui.Button(Localization.SaveAndClose);

                ImGui.EndTabItem();
            }
        }

        private void DrawCommunityTab(ref bool saveAndClose)
        {
            if (PalImGui.BeginTabItemWithFlags($"{Localization.ConfigTab_Community}###TabCommunity", _switchToCommunityTab ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None))
            {
                _switchToCommunityTab = false;

                ImGui.TextWrapped(Localization.Explanation_3);
                ImGui.TextWrapped(Localization.Explanation_4);

                PalImGui.RadioButtonWrapped(Localization.Config_UploadMyDiscoveries_ShowOtherTraps, ref _mode, (int)Configuration.EMode.Online);
                PalImGui.RadioButtonWrapped(Localization.Config_NeverUploadDiscoveries_ShowMyTraps, ref _mode, (int)Configuration.EMode.Offline);
                saveAndClose = ImGui.Button(Localization.SaveAndClose);

                ImGui.Separator();

                ImGui.BeginDisabled(Service.Configuration.Mode != Configuration.EMode.Online);
                if (ImGui.Button(Localization.Config_TestConnection))
                    TestConnection();

                if (_connectionText != null)
                    ImGui.Text(_connectionText);

                ImGui.EndDisabled();
                ImGui.EndTabItem();
            }
        }

        private void DrawImportTab()
        {
            if (ImGui.BeginTabItem($"{Localization.ConfigTab_Import}###TabImport"))
            {
                ImGui.TextWrapped(Localization.Config_ImportExplanation1);
                ImGui.TextWrapped(Localization.Config_ImportExplanation2);
                ImGui.TextWrapped(Localization.Config_ImportExplanation3);
                ImGui.Separator();
                ImGui.TextWrapped(string.Format(Localization.Config_ImportDownloadLocation, "https://github.com/carvelli/PalacePal/releases/"));
                if (ImGui.Button(Localization.Config_Import_VisitGitHub))
                    GenericHelpers.ShellStart("https://github.com/carvelli/PalacePal/releases/latest");
                ImGui.Separator();
                ImGui.Text(Localization.Config_SelectImportFile);
                ImGui.SameLine();
                ImGui.InputTextWithHint("", Localization.Config_SelectImportFile_Hint, ref _openImportPath, 260);
                ImGui.SameLine();
                if (ImGuiComponents.IconButton(FontAwesomeIcon.Search))
                {
                    _importDialog.OpenFileDialog(Localization.Palace_Pal, $"{Localization.Palace_Pal} (*.pal) {{.pal}}", (success, paths) =>
                    {
                        if (success && paths.Count == 1)
                        {
                            _openImportPath = paths.First();
                        }
                    }, selectionCountMax: 1, startPath: _openImportDialogStartPath, isModal: false);
                    _openImportDialogStartPath = null; // only use this once, FileDialogManager will save path between calls
                }

                ImGui.BeginDisabled(string.IsNullOrEmpty(_openImportPath) || !File.Exists(_openImportPath));
                if (ImGui.Button(Localization.Config_StartImport))
                    DoImport(_openImportPath);
                ImGui.EndDisabled();

                var importHistory = Service.Configuration.ImportHistory.OrderByDescending(x => x.ImportedAt).ThenBy(x => x.Id).FirstOrDefault();
                if (importHistory != null)
                {
                    ImGui.Separator();
                    ImGui.TextWrapped(string.Format(Localization.Config_UndoImportExplanation1, importHistory.ImportedAt, importHistory.RemoteUrl, importHistory.ExportedAt));
                    ImGui.TextWrapped(Localization.Config_UndoImportExplanation2);
                    if (ImGui.Button(Localization.Config_UndoImport))
                        UndoImport(importHistory.Id);
                }

                ImGui.EndTabItem();
            }
        }

        private void DrawExportTab()
        {
            if (Service.RemoteApi.HasRoleOnCurrentServer("export:run") && ImGui.BeginTabItem($"{Localization.ConfigTab_Export}###TabExport"))
            {
                string todaysFileName = $"export-{DateTime.Today:yyyy-MM-dd}.pal";
                if (string.IsNullOrEmpty(_saveExportPath) && !string.IsNullOrEmpty(_saveExportDialogStartPath))
                    _saveExportPath = Path.Join(_saveExportDialogStartPath, todaysFileName);

                ImGui.TextWrapped(string.Format(Localization.Config_ExportSource, RemoteApi.RemoteUrl));
                ImGui.Text(Localization.Config_Export_SaveAs);
                ImGui.SameLine();
                ImGui.InputTextWithHint("", Localization.Config_SelectImportFile_Hint, ref _saveExportPath, 260);
                ImGui.SameLine();
                if (ImGuiComponents.IconButton(FontAwesomeIcon.Search))
                {
                    _importDialog.SaveFileDialog(Localization.Palace_Pal, $"{Localization.Palace_Pal} (*.pal) {{.pal}}", todaysFileName, "pal", (success, path) =>
                    {
                        if (success && !string.IsNullOrEmpty(path))
                        {
                            _saveExportPath = path;
                        }
                    }, startPath: _saveExportDialogStartPath, isModal: false);
                    _saveExportDialogStartPath = null; // only use this once, FileDialogManager will save path between calls
                }

                ImGui.BeginDisabled(string.IsNullOrEmpty(_saveExportPath) || File.Exists(_saveExportPath));
                if (ImGui.Button(Localization.Config_StartExport))
                    DoExport(_saveExportPath);
                ImGui.EndDisabled();

                ImGui.EndTabItem();
            }
        }

        private void DrawRenderTab(ref bool save, ref bool saveAndClose)
        {
            if (ImGui.BeginTabItem($"{Localization.ConfigTab_Renderer}###TabRenderer"))
            {
                ImGui.Text(Localization.Config_SelectRenderBackend);
                ImGui.RadioButton($"{Localization.Config_Renderer_Splatoon} ({Localization.Config_Renderer_Splatoon_Hint})", ref _renderer, (int)Configuration.ERenderer.Splatoon);
                ImGui.RadioButton($"{Localization.Config_Renderer_Simple} ({Localization.Config_Renderer_Simple_Hint})", ref _renderer, (int)Configuration.ERenderer.Simple);

                ImGui.Separator();

                save = ImGui.Button(Localization.Save);
                ImGui.SameLine();
                saveAndClose = ImGui.Button(Localization.SaveAndClose);

                ImGui.Separator();
                ImGui.Text(Localization.Config_Splatoon_Test);
                ImGui.BeginDisabled(!(Service.Plugin.Renderer is IDrawDebugItems));
                if (ImGui.Button(Localization.Config_Splatoon_DrawCircles))
                    (Service.Plugin.Renderer as IDrawDebugItems)?.DrawDebugItems(_trapColor, _hoardColor);
                ImGui.EndDisabled();

                ImGui.EndTabItem();
            }
        }

        private void DrawDebugTab()
        {
            if (ImGui.BeginTabItem($"{Localization.ConfigTab_Debug}###TabDebug"))
            {
                var plugin = Service.Plugin;
                if (plugin.IsInDeepDungeon())
                {
                    ImGui.Text($"You are in a deep dungeon, territory type {plugin.LastTerritory}.");
                    ImGui.Text($"Sync State = {plugin.TerritorySyncState}");
                    ImGui.Text($"{plugin.DebugMessage}");

                    ImGui.Indent();
                    if (plugin.FloorMarkers.TryGetValue(plugin.LastTerritory, out var currentFloor))
                    {
                        if (_showTraps)
                        {
                            int traps = currentFloor.Markers.Count(x => x.Type == Marker.EType.Trap);
                            ImGui.Text($"{traps} known trap{(traps == 1 ? "" : "s")}");
                        }
                        if (_showHoard)
                        {
                            int hoardCoffers = currentFloor.Markers.Count(x => x.Type == Marker.EType.Hoard);
                            ImGui.Text($"{hoardCoffers} known hoard coffer{(hoardCoffers == 1 ? "" : "s")}");
                        }
                        if (_showSilverCoffers)
                        {
                            int silverCoffers = plugin.EphemeralMarkers.Count(x => x.Type == Marker.EType.SilverCoffer);
                            ImGui.Text($"{silverCoffers} silver coffer{(silverCoffers == 1 ? "" : "s")} visible on current floor");
                        }

                        ImGui.Text($"Pomander of Sight: {plugin.PomanderOfSight}");
                        ImGui.Text($"Pomander of Intuition: {plugin.PomanderOfIntuition}");
                    }
                    else
                        ImGui.Text("Could not query current trap/coffer count.");
                    ImGui.Unindent();
                    ImGui.TextWrapped("Traps and coffers may not be discovered even after using a pomander if they're far away (around 1,5-2 rooms).");
                }
                else
                    ImGui.Text(Localization.Config_Debug_NotInADeepDungeon);

                ImGui.EndTabItem();
            }
        }

        internal void TestConnection()
        {
            Task.Run(async () =>
            {
                _connectionText = Localization.Config_TestConnection_Connecting;
                _switchToCommunityTab = true;

                _testConnectionCts?.Cancel();

                CancellationTokenSource cts = new();
                cts.CancelAfter(TimeSpan.FromSeconds(60));
                _testConnectionCts = cts;

                try
                {
                    _connectionText = await Service.RemoteApi.VerifyConnection(cts.Token);
                }
                catch (Exception e)
                {
                    if (cts == _testConnectionCts)
                    {
                        PluginLog.Error(e, "Could not establish remote connection");
                        _connectionText = e.ToString();
                    }
                    else
                        PluginLog.Warning(e, "Could not establish a remote connection, but user also clicked 'test connection' again so not updating UI");
                }
            });
        }

        private void DoImport(string sourcePath)
        {
            Service.Plugin.EarlyEventQueue.Enqueue(new QueuedImport(sourcePath));
        }

        private void UndoImport(Guid importId)
        {
            Service.Plugin.EarlyEventQueue.Enqueue(new QueuedUndoImport(importId));
        }

        private void DoExport(string destinationPath)
        {
            Task.Run(async () =>
            {
                try
                {
                    (bool success, ExportRoot export) = await Service.RemoteApi.DoExport();
                    if (success)
                    {
                        await using var output = File.Create(destinationPath);
                        export.WriteTo(output);

                        Service.Chat.Print($"Export saved as {destinationPath}.");
                    }
                    else
                    {
                        Service.Chat.PrintError("Export failed due to server error.");
                    }
                }
                catch (Exception e)
                {
                    PluginLog.Error(e, "Export failed");
                    Service.Chat.PrintError($"Export failed: {e}");
                }
            });
        }
    }
}
