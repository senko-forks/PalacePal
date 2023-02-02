using Account;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using ECommons;
using ECommons.Reflection;
using ECommons.SplatoonAPI;
using Google.Protobuf;
using ImGuiNET;
using Pal.Client.Net;
using Pal.Client.Scheduled;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Pal.Client.Windows
{
    internal class ConfigWindow : Window
    {
        private int _mode;
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
        private FileDialogManager _importDialog;
        private FileDialogManager _exportDialog;

        public ConfigWindow() : base("Palace Pal###PalPalaceConfig")
        {
            var version = typeof(Plugin).Assembly.GetName().Version!.ToString(2);
            WindowName = $"Palace Pal v{version}###PalPalaceConfig";
            Size = new Vector2(500, 400);
            SizeCondition = ImGuiCond.FirstUseEver;
            Position = new Vector2(300, 300);
            PositionCondition = ImGuiCond.FirstUseEver;

            _importDialog = new FileDialogManager { AddedWindowFlags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoDocking };
            _exportDialog = new FileDialogManager { AddedWindowFlags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoDocking };
        }

        public override void OnOpen()
        {
            var config = Service.Configuration;
            _mode = (int)config.Mode;
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
        }

        public override void Draw()
        {
            bool save = false;
            bool saveAndClose = false;
            if (ImGui.BeginTabBar("PalTabs"))
            {
                DrawTrapCofferTab(ref save, ref saveAndClose);
                DrawCommunityTab(ref saveAndClose);
                DrawImportTab();
                DrawExportTab();
                DrawDebugTab();

                ImGui.EndTabBar();
            }

            _importDialog.Draw();

            if (save || saveAndClose)
            {
                var config = Service.Configuration;
                config.Mode = (Configuration.EMode)_mode;
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

        private void DrawTrapCofferTab(ref bool save, ref bool saveAndClose)
        {
            if (ImGui.BeginTabItem("PotD/HoH"))
            {
                ImGui.Checkbox("Show traps", ref _showTraps);
                ImGui.Indent();
                ImGui.BeginDisabled(!_showTraps);
                ImGui.Spacing();
                ImGui.ColorEdit4("Trap color", ref _trapColor, ImGuiColorEditFlags.NoInputs);
                ImGui.Checkbox("Hide traps not on current floor", ref _onlyVisibleTrapsAfterPomander);
                ImGui.SameLine();
                ImGuiComponents.HelpMarker("When using a Pomander of sight, only the actual trap locations are visible, all other traps are hidden.");
                ImGui.EndDisabled();
                ImGui.Unindent();

                ImGui.Separator();

                ImGui.Checkbox("Show hoard coffers", ref _showHoard);
                ImGui.Indent();
                ImGui.BeginDisabled(!_showHoard);
                ImGui.Spacing();
                ImGui.ColorEdit4("Hoard Coffer color", ref _hoardColor, ImGuiColorEditFlags.NoInputs);
                ImGui.Checkbox("Hide hoard coffers not on current floor", ref _onlyVisibleHoardAfterPomander);
                ImGui.SameLine();
                ImGuiComponents.HelpMarker("When using a Pomander of intuition, only the actual hoard coffer location is visible, all other (potential) hoard coffers are hidden.");
                ImGui.EndDisabled();
                ImGui.Unindent();

                ImGui.Separator();

                ImGui.Checkbox("Show silver coffers on current floor", ref _showSilverCoffers);
                ImGuiComponents.HelpMarker("Shows all the silver coffers visible to you on the current floor.\nThis is not synchronized with other players and not saved between floors/runs.\n\nExperimental feature.");
                ImGui.Indent();
                ImGui.BeginDisabled(!_showSilverCoffers);
                ImGui.Spacing();
                ImGui.ColorEdit4("Silver Coffer color", ref _silverCofferColor, ImGuiColorEditFlags.NoInputs);
                ImGui.Checkbox("Draw filled", ref _fillSilverCoffers);
                ImGui.EndDisabled();
                ImGui.Unindent();

                ImGui.Separator();

                save = ImGui.Button("Save");
                ImGui.SameLine();
                saveAndClose = ImGui.Button("Save & Close");

                ImGui.EndTabItem();
            }
        }

        private void DrawCommunityTab(ref bool saveAndClose)
        {
            if (BeginTabItemEx("Community", _switchToCommunityTab ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None))
            {
                _switchToCommunityTab = false;

                ImGui.TextWrapped("Ideally, we want to discover every potential trap and chest location in the game, but doing this alone is very tedious. Floor 51-60 has over 230 trap locations and over 200 coffer locations - and we don't know if that map is complete. Higher floors naturally see fewer runs, making solo attempts to map the place much harder.");
                ImGui.TextWrapped("You can decide whether you want to share traps and chests you find with the community, which likewise also will let you see chests and coffers found by other players. This can be changed at any time. No data regarding your FFXIV character or account is ever sent to our server.");

                ImGui.RadioButton("Upload my discoveries, show traps & coffers other players have discovered", ref _mode, (int)Configuration.EMode.Online);
                ImGui.RadioButton("Never upload discoveries, show only traps and coffers I found myself", ref _mode, (int)Configuration.EMode.Offline);
                saveAndClose = ImGui.Button("Save & Close");

                ImGui.Separator();

                ImGui.BeginDisabled(Service.Configuration.Mode != Configuration.EMode.Online);
                if (ImGui.Button("Test Connection"))
                    TestConnection();

                if (_connectionText != null)
                    ImGui.Text(_connectionText);

                ImGui.EndDisabled();
                ImGui.EndTabItem();
            }
        }

        private void DrawImportTab()
        {
            if (ImGui.BeginTabItem("Import"))
            {
                ImGui.TextWrapped("Using an export is useful if you're unable to connect to the server, or don't wish to share your findings.");
                ImGui.TextWrapped("Exports are (currently) generated manually, and they only include traps and hoard coffers encountered by 5 or more people. This may lead to higher floors having very sporadic coverage, but commonly run floors (e.g. PotD 51-60, HoH 21-30) are closer to complete.");
                ImGui.TextWrapped("If you aren't offline, importing a file won't have any noticeable effect.");
                ImGui.Separator();
                ImGui.TextWrapped("Exports are available from https://github.com/carvelli/PalacePal/releases/ (as *.pal files).");
                if (ImGui.Button("Visit GitHub"))
                    GenericHelpers.ShellStart("https://github.com/carvelli/PalacePal/releases/latest");
                ImGui.Separator();
                ImGui.Text("File to Import:");
                ImGui.SameLine();
                ImGui.InputTextWithHint("", "Path to *.pal file", ref _openImportPath, 260);
                ImGui.SameLine();
                if (ImGuiComponents.IconButton(FontAwesomeIcon.Search))
                {
                    _importDialog.OpenFileDialog("Palace Pal - Import", "Palace Pal (*.pal) {.pal}", (success, paths) =>
                    {
                        if (success && paths.Count == 1)
                        {
                            _openImportPath = paths.First();
                        }
                    }, selectionCountMax: 1, startPath: _openImportDialogStartPath, isModal: false);
                    _openImportDialogStartPath = null; // only use this once, FileDialogManager will save path between calls
                }

                ImGui.BeginDisabled(string.IsNullOrEmpty(_openImportPath) || !File.Exists(_openImportPath));
                if (ImGui.Button("Start Import"))
                    DoImport(_openImportPath);
                ImGui.EndDisabled();

                var importHistory = Service.Configuration.ImportHistory.OrderByDescending(x => x.ImportedAt).ThenBy(x => x.Id).FirstOrDefault();
                if (importHistory != null)
                {
                    ImGui.Separator();
                    ImGui.TextWrapped($"Your last import was on {importHistory.ImportedAt}, which added the trap/hoard coffer database from {importHistory.RemoteUrl} created on {importHistory.ExportedAt:d}.");
                    ImGui.TextWrapped("If you think that was a mistake, you can remove all locations only found in the import (any location you've seen yourself is not changed).");
                    if (ImGui.Button("Undo Import"))
                        UndoImport(importHistory.Id);
                }

                ImGui.EndTabItem();
            }
        }

        private void DrawExportTab()
        {
            if (Service.RemoteApi.HasRoleOnCurrentServer("export:run") && ImGui.BeginTabItem("Export"))
            {
                string todaysFileName = $"export-{DateTime.Today:yyyy-MM-dd}.pal";
                if (string.IsNullOrEmpty(_saveExportPath) && !string.IsNullOrEmpty(_saveExportDialogStartPath))
                    _saveExportPath = Path.Join(_saveExportDialogStartPath, todaysFileName);

                ImGui.TextWrapped($"Export all markers from {RemoteApi.RemoteUrl}:");
                ImGui.Text("Save as:");
                ImGui.SameLine();
                ImGui.InputTextWithHint("", "Path to *.pal file", ref _saveExportPath, 260);
                ImGui.SameLine();
                if (ImGuiComponents.IconButton(FontAwesomeIcon.Search))
                {
                    _importDialog.SaveFileDialog("Palace Pal - Export", "Palace Pal (*.pal) {.pal}", todaysFileName, "pal", (success, path) =>
                    {
                        if (success && !string.IsNullOrEmpty(path))
                        {
                            _saveExportPath = path;
                        }
                    }, startPath: _saveExportDialogStartPath, isModal: false);
                    _saveExportDialogStartPath = null; // only use this once, FileDialogManager will save path between calls
                }

                ImGui.BeginDisabled(string.IsNullOrEmpty(_saveExportPath) || File.Exists(_saveExportPath));
                if (ImGui.Button("Start Export"))
                    DoExport(_saveExportPath);
                ImGui.EndDisabled();

                ImGui.EndTabItem();
            }
        }

        private void DrawDebugTab()
        {
            if (ImGui.BeginTabItem("Debug"))
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
                    ImGui.Text("You are NOT in a deep dungeon.");

                ImGui.Separator();

                if (ImGui.Button("Draw trap & coffer circles around self"))
                    DrawDebugItems();

                ImGui.EndTabItem();
            }
        }

        private void DrawDebugItems()
        {
            try
            {
                Vector3? pos = Service.ClientState.LocalPlayer?.Position;
                if (pos != null)
                {
                    var elements = new List<Element>
                    {
                        Plugin.CreateSplatoonElement(Marker.EType.Trap, pos.Value, _trapColor),
                        Plugin.CreateSplatoonElement(Marker.EType.Hoard, pos.Value, _hoardColor),
                    };

                    if (!Splatoon.AddDynamicElements("PalacePal.Test", elements.ToArray(), new long[] { Environment.TickCount64 + 10000 }))
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

        /// <summary>
        /// None of the default BeginTabItem methods allow using flags without making the tab have a close button for some reason.
        /// </summary>
        private unsafe static bool BeginTabItemEx(string label, ImGuiTabItemFlags flags)
        {
            int labelLength = Encoding.UTF8.GetByteCount(label);
            byte* labelPtr = stackalloc byte[labelLength + 1];
            byte[] labelBytes = Encoding.UTF8.GetBytes(label);

            Marshal.Copy(labelBytes, 0, (IntPtr)labelPtr, labelLength);
            labelPtr[labelLength] = 0;

            return ImGuiNative.igBeginTabItem(labelPtr, null, flags) != 0;
        }

        internal void TestConnection()
        {
            Task.Run(async () =>
            {
                _connectionText = "Testing...";
                _switchToCommunityTab = true;

                CancellationTokenSource cts = new CancellationTokenSource();
                cts.CancelAfter(TimeSpan.FromSeconds(60));

                try
                {
                    _connectionText = await Service.RemoteApi.VerifyConnection(cts.Token);
                }
                catch (Exception e)
                {
                    PluginLog.Error(e, "Could not establish remote connection");
                    _connectionText = e.ToString();
                }
            });
        }

        internal void DoImport(string sourcePath)
        {
            Service.Plugin.EarlyEventQueue.Enqueue(new QueuedImport(sourcePath));
        }

        internal void UndoImport(Guid importId)
        {
            Service.Plugin.EarlyEventQueue.Enqueue(new QueuedUndoImport(importId));
        }

        internal void DoExport(string destinationPath)
        {
            Task.Run(async () =>
            {
                try
                {
                    (bool success, ExportRoot export) = await Service.RemoteApi.DoExport();
                    if (success)
                    {
                        using var output = File.Create(destinationPath);
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
