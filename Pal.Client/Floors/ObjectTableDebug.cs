using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Gui;
using Dalamud.Plugin;
using ImGuiNET;

namespace Pal.Client.Floors
{
    /// <summary>
    /// This isn't very useful for running deep dungeons normally, but it is for plugin dev.
    ///
    /// Needs the corresponding beta feature to be enabled.
    /// </summary>
    internal sealed class ObjectTableDebug : IDisposable
    {
        public const string FeatureName = nameof(ObjectTableDebug);

        private readonly DalamudPluginInterface _pluginInterface;
        private readonly ObjectTable _objectTable;
        private readonly GameGui _gameGui;
        private readonly ClientState _clientState;

        public ObjectTableDebug(DalamudPluginInterface pluginInterface, ObjectTable objectTable, GameGui gameGui, ClientState clientState)
        {
            _pluginInterface = pluginInterface;
            _objectTable = objectTable;
            _gameGui = gameGui;
            _clientState = clientState;

            _pluginInterface.UiBuilder.Draw += Draw;
        }

        private void Draw()
        {
            int index = 0;
            foreach (GameObject obj in _objectTable)
            {
                if (obj is EventObj eventObj && string.IsNullOrEmpty(eventObj.Name.ToString()))
                {
                    ++index;
                    int model = Marshal.ReadInt32(obj.Address + 128);

                    if (_gameGui.WorldToScreen(obj.Position, out var screenCoords))
                    {
                        // So, while WorldToScreen will return false if the point is off of game client screen, to
                        // to avoid performance issues, we have to manually determine if creating a window would
                        // produce a new viewport, and skip rendering it if so
                        float distance = DistanceToPlayer(obj.Position);
                        var objectText =
                            $"{obj.Address.ToInt64():X}:{obj.ObjectId:X}[{index}]\nkind: {obj.ObjectKind} sub: {obj.SubKind}\nmodel: {model}\nname: {obj.Name}\ndata id: {obj.DataId}";

                        var screenPos = ImGui.GetMainViewport().Pos;
                        var screenSize = ImGui.GetMainViewport().Size;

                        var windowSize = ImGui.CalcTextSize(objectText);

                        // Add some extra safety padding
                        windowSize.X += ImGui.GetStyle().WindowPadding.X + 10;
                        windowSize.Y += ImGui.GetStyle().WindowPadding.Y + 10;

                        if (screenCoords.X + windowSize.X > screenPos.X + screenSize.X ||
                            screenCoords.Y + windowSize.Y > screenPos.Y + screenSize.Y)
                            continue;

                        if (distance > 50f)
                            continue;

                        ImGui.SetNextWindowPos(new Vector2(screenCoords.X, screenCoords.Y));

                        ImGui.SetNextWindowBgAlpha(Math.Max(1f - (distance / 50f), 0.2f));
                        if (ImGui.Begin(
                                $"PalacePal_{nameof(ObjectTableDebug)}_{index}",
                                ImGuiWindowFlags.NoDecoration |
                                ImGuiWindowFlags.AlwaysAutoResize |
                                ImGuiWindowFlags.NoSavedSettings |
                                ImGuiWindowFlags.NoMove |
                                ImGuiWindowFlags.NoMouseInputs |
                                ImGuiWindowFlags.NoDocking |
                                ImGuiWindowFlags.NoFocusOnAppearing |
                                ImGuiWindowFlags.NoNav))
                            ImGui.Text(objectText);
                        ImGui.End();
                    }
                }
            }
        }

        private float DistanceToPlayer(Vector3 center)
            => Vector3.Distance(_clientState.LocalPlayer?.Position ?? Vector3.Zero, center);

        public void Dispose()
        {
            _pluginInterface.UiBuilder.Draw -= Draw;
        }
    }
}
