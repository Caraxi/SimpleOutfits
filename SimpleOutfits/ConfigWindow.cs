using System.Diagnostics;
using System.Numerics;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets2;
using Penumbra.Collections.Manager;
using Penumbra.GameData.Actors;
using SimpleOutfits.Helpers;
using SimpleOutfits.Interop;
using SimpleOutfits.Interop.Glamourer;

namespace SimpleOutfits;

public class ConfigWindow(IClientState clientState, GlamourerHelper glamourerHelper, GameDataHelper gameDataHelper, ITextureProvider textureProvider, IDataManager dataManager, CollectionManager collectionManager, ActorManager actorManager) : SimpleWindow($"{nameof(SimpleOutfits)}#{nameof(ConfigWindow)}", "Simple Outfits") {


    private GlamourerState? GlamourerState;
    
    private readonly Stopwatch stateAge = Stopwatch.StartNew();

    private void UpdateState() {
        stateAge.Restart();

        if (clientState.LocalPlayer == null) return;

        // var collection = collectionManager.GetCollection(clientState.LocalPlayer);
        GlamourerState = glamourerHelper.GetState(clientState.LocalPlayer);

        // if (collection == null || state == null) return;
        // currentOutfit = collection.CreateOutfit(state);
    }

    public override void Draw() {
        
        
        if (clientState.LocalPlayer == null) {
            ImGui.Text("Not Logged In");
            return;
        }

        if (GlamourerState == null || stateAge.ElapsedMilliseconds > 1000) {
            UpdateState();
        }



        var collection = collectionManager.Active.Individual(actorManager.GetCurrentPlayer());
        
        ImGui.Text($"Collections:");
        foreach (var inheritFrom in collection.GetFlattenedInheritance()) {
            ImGui.Text($" - {inheritFrom.Name}");
        }
        
        if (GlamourerState == null) {
            ImGui.Text("No State");
        } else {
            ImGui.Text("Items:");
            using (ImRaii.PushIndent()) {
                var idx = 0;
                foreach (var i in GlamourerState.Equipment.Items) {
                    using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.One))
                    using (ImRaii.PushId($"StateItem_{idx++}")) {
                        var tex = textureProvider.GetFromGameIcon(gameDataHelper.GetItemIcon(i)).GetWrapOrEmpty();

                        ImGui.Image(tex.ImGuiHandle, new Vector2(ImGui.GetTextLineHeight() * 2 + ImGui.GetStyle().FramePadding.Y * 4 + ImGui.GetStyle().ItemSpacing.Y));
                        ImGui.SameLine();

                        using (ImRaii.Group()) {
                            var itemName = gameDataHelper.GetItemName(i);
                            ImGui.SetNextItemWidth(280 * ImGuiHelpers.GlobalScale);
                            ImGui.InputText("##itemName", ref itemName, 24, ImGuiInputTextFlags.ReadOnly);
                            var s = ImGui.GetItemRectSize();
                            StainButton(i.Stain, new Vector2(s.Y));
                            ImGui.SameLine();
                            StainButton(i.Stain2, new Vector2(s.Y));
                            
                            
                        }
                    }
                }
            }
        }
    }

    private bool StainButton(byte stainId, Vector2 size, bool isSelected = false) {
        var stain = dataManager.GetExcelSheet<Stain>()?.GetRow(stainId);
        return StainButton(stain, size, isSelected);
    }

    private bool StainButton(Stain? stain, Vector2 size, bool isSelected = false) {
        ImGui.Dummy(size);

        var drawOffset = size / 1.5f;
        var drawOffset2 = size / 2.25f;
        var pos = ImGui.GetItemRectMin();
        var center = ImGui.GetItemRectMin() + ImGui.GetItemRectSize() / 2;
        var dl = ImGui.GetWindowDrawList();
        var texture = textureProvider.GetFromGame("ui/uld/ListColorChooser_hr1.tex").GetWrapOrEmpty();

        if (stain == null || stain.RowId == 0) {
            dl.AddImage(texture.ImGuiHandle, center - drawOffset2, center + drawOffset2, new Vector2(0.8333333f, 0.3529412f), new Vector2(0.9444444f, 0.47058824f), 0x80FFFFFF);
            dl.AddImage(texture.ImGuiHandle, center - drawOffset, center + drawOffset, new Vector2(0.27777f, 0.3529f), new Vector2(0.55555f, 0.64705f));
            if (ImGui.IsItemHovered()) {
                dl.AddImage(texture.ImGuiHandle, center - drawOffset, center + drawOffset, new Vector2(0.55555f, 0.3529f), new Vector2(0.83333f, 0.64705f));
            }

            return ImGui.IsItemClicked();
        }

        var b = stain.Color & 255;
        var g = (stain.Color >> 8) & 255;
        var r = (stain.Color >> 16) & 255;
        var stainVec4 = new Vector4(r / 255f, g / 255f, b / 255f, 1f);
        var stainColor = ImGui.GetColorU32(stainVec4);

        dl.AddImage(texture.ImGuiHandle, center - drawOffset, center + drawOffset, new Vector2(0, 0.3529f), new Vector2(0.27777f, 0.6470f), stainColor);
        if (stain.Unknown1) {
            dl.PushClipRect(center - drawOffset2, center + drawOffset2);
            ImGui.ColorConvertRGBtoHSV(stainVec4.X, stainVec4.Y, stainVec4.Z, out var h, out var s, out var v);
            ImGui.ColorConvertHSVtoRGB(h, s, v - 0.5f, out var dR, out var dG, out var dB);
            ImGui.ColorConvertHSVtoRGB(h, s, v + 0.8f, out var bR, out var bG, out var bB);
            var dColor = ImGui.GetColorU32(new Vector4(dR, dG, dB, 1));
            var bColor = ImGui.GetColorU32(new Vector4(bR, bG, bB, 1));
            var tr = pos + size with { Y = 0 };
            var bl = pos + size with { X = 0 };
            var opacity = 0U;
            for (var x = 3; x < size.X; x++) {
                if (opacity < 0xF0_00_00_00U) opacity += 0x08_00_00_00U;
                dl.AddLine(tr + new Vector2(0, x), bl + new Vector2(x, 0), opacity | (0x00A0A0A0 & dColor), 2);
                dl.AddLine(tr - new Vector2(0, x), bl - new Vector2(x, 0), opacity | (0x00FFFFFF & bColor), 2);
            }

            dl.PopClipRect();
        }

        dl.AddImage(texture.ImGuiHandle, center - drawOffset, center + drawOffset, new Vector2(0.27777f, 0.3529f), new Vector2(0.55555f, 0.64705f));
        if ((isSelected) || ImGui.IsItemHovered()) {
            dl.AddImage(texture.ImGuiHandle, center - drawOffset, center + drawOffset, new Vector2(0.55555f, 0.3529f), new Vector2(0.83333f, 0.64705f));
        }

        return ImGui.IsItemClicked();
    }
}
