using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets2;
using Penumbra.Api.Enums;
using Penumbra.Mods.Manager;
using Penumbra.Services;
using SimpleOutfitsPlugin.Helpers;
using SimpleOutfitsPlugin.Interop.Glamourer;
using SimpleOutfitsPlugin.Outfits;
using SimpleOutfitsPlugin.Services;
using SimpleOutfitsPlugin.Sheets;

namespace SimpleOutfitsPlugin;

public class ConfigWindow(IClientState clientState, ITextureProvider textureProvider, IDataManager dataManager, ItemManager itemManager, OutfitManager outfitManager, CommunicatorService communicatorService, ModManager modManager) : SimpleWindow($"{nameof(SimpleOutfitsPlugin)}#{nameof(ConfigWindow)}", "Simple Outfits") {
    private IOutfit? _selectedOutfit;

    public override void DrawContents() {
        if (clientState.LocalPlayer == null) return;

        if (ImGui.BeginChild("outfitSelector", new Vector2(200, ImGui.GetContentRegionAvail().Y), true)) {
            ImGui.TreeNodeEx($"Active Outfit", ImGuiTreeNodeFlags.SpanFullWidth | ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.Bullet | ImGuiTreeNodeFlags.NoTreePushOnOpen | (_selectedOutfit is ActiveOutfit ? ImGuiTreeNodeFlags.Selected : ImGuiTreeNodeFlags.None));
            if (ImGui.IsItemClicked())
                if (outfitManager.TryGetOutfitForLocalCharacter(out var o))
                    _selectedOutfit = o;

            ImGui.Separator();

            foreach (var (outfitIdentifier, outfit) in outfitManager.GetSavedOutfits().DrawFolderTree()) {
                ImGui.TreeNodeEx($"{outfit.Name}###savedOutfit_{outfitIdentifier}", ImGuiTreeNodeFlags.SpanFullWidth | ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.Bullet | ImGuiTreeNodeFlags.NoTreePushOnOpen | (outfit.Equals(_selectedOutfit) ? ImGuiTreeNodeFlags.Selected : ImGuiTreeNodeFlags.None));
                if (ImGui.IsItemClicked()) _selectedOutfit = outfit;

                /*
                if (ImGui.Selectable(, outfit.Equals(selectedOutfit))) {
                    selectedOutfit = outfit;
                }
                */
            }
        }

        ImGui.EndChild();
        ImGui.SameLine();
        if (ImGui.BeginChild("outfitViewer", ImGui.GetContentRegionAvail(), true)) DrawOutfitView(_selectedOutfit);

        ImGui.EndChild();
    }

    private string _popupSaveOutfitInputText = string.Empty;
    private string? _popupSavedOutfitError = null;

    public void DrawOutfitView(IOutfit? outfitInterface) {
        var outfit = outfitInterface?.AsOutfit();
        if (outfit == null) return;

        if (outfit is ActiveOutfit) {
            if (ImGui.Button("Save Outfit")) {
                ImGui.OpenPopup("popupSaveOutfit");
                _popupSaveOutfitInputText = string.Empty;
            }

            if (ImGui.BeginPopup("popupSaveOutfit")) {
                if (ImGui.IsWindowAppearing()) ImGui.SetKeyboardFocusHere();

                if (ImGui.InputTextWithHint("##outfitName", "Outfit Name", ref _popupSaveOutfitInputText, 128, ImGuiInputTextFlags.EnterReturnsTrue))
                    if (outfitManager.TrySaveOutfit(outfit, _popupSaveOutfitInputText, out _popupSavedOutfitError, out var savedOutfit)) {
                        _selectedOutfit = savedOutfit;
                        ImGui.CloseCurrentPopup();
                    }

                if (!string.IsNullOrEmpty(_popupSavedOutfitError)) ImGui.TextColored(ImGuiColors.DalamudRed, _popupSavedOutfitError);

                ImGui.EndPopup();
            }

            ImGui.Separator();
        }

        var hairMakeType = dataManager.GetExcelSheet<HairMakeTypeExt>()!.FirstOrDefault(h => h.Race.Row == outfit.GlamourerState.Customize.Race.Value && h.Tribe.Row == outfit.GlamourerState.Customize.Clan.Value && h.Gender == outfit.GlamourerState.Customize.Gender.Value);
        var hairCustomize = hairMakeType?.HairStyles?.FirstOrDefault(h => h.Value?.FeatureID == outfit.GlamourerState.Customize.Hairstyle.Value)?.Value;
        ShowSlot("Hairstyle", $"Hairstyle #{outfit.GlamourerState.Customize.Hairstyle.Value}", hairCustomize?.Icon ?? 0, outfit.HairModConfigs);

        foreach (var (slot, i) in outfit.GlamourerState.Equipment.Items) {
            var item = itemManager.Resolve(slot, i.ItemId);
            if (!outfit.EquipModConfigs.TryGetValue(slot, out var mods)) mods = new Dictionary<string, OutfitModConfig>();
            ShowSlot($"{slot}", item.Name, item.IconId.Id, mods, (i.Stain, i.Stain2), outfit.GlamourerState.Materials.Where(s => s.Key.ToEquipSlot() == slot).ToDictionary());
        }
    }

    private void ShowSlot(string slotName, string itemName, uint iconId, Dictionary<string, OutfitModConfig> configs, (byte Stain1, byte Stain2)? stains = null, Dictionary<MaterialValueIndex, GlamourerMaterial>? materials = null) {
        using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.One))
        using (ImRaii.PushId($"State_{slotName}")) {
            ImGui.Dummy(new Vector2(4) * ImGui.GetIO().FontGlobalScale);
            var tex = textureProvider.GetFromGameIcon(iconId).GetWrapOrEmpty();
            ImGui.Image(tex.ImGuiHandle, new Vector2(ImGui.GetTextLineHeight() * 2 + ImGui.GetStyle().FramePadding.Y * 4 + ImGui.GetStyle().ItemSpacing.Y));
            ImGui.SameLine();

            var s = new Vector2(280 * ImGuiHelpers.GlobalScale, ImGui.GetTextLineHeight() + ImGui.GetStyle().FramePadding.Y * 2);

            using (ImRaii.Group()) {
                ImGui.SetNextItemWidth(s.X - (materials is { Count: > 0 } ? s.Y + ImGui.GetStyle().ItemSpacing.X : 0) - (stains != null ? s.Y * 2 + ImGui.GetStyle().ItemSpacing.X * 2 : 0));

                ImGui.BeginGroup();
                ImGui.InputText("##itemName", ref itemName, 64, ImGuiInputTextFlags.ReadOnly);

                if (materials is { Count: > 0 }) {
                    ImGui.SameLine();
                    using (ImRaii.PushFont(UiBuilder.IconFont)) {
                        if (ImGui.Button(FontAwesomeIcon.Palette.ToIconString(), new Vector2(s.Y))) { }
                    }

                    if (ImGui.IsItemHovered()) {
                        ImGui.BeginTooltip();

                        ImGui.Text($"{slotName} Advanced Dyes");
                        ImGui.Separator();

                        using (ImRaii.PushColor(ImGuiCol.FrameBg, Vector4.Zero))
                        using (ImRaii.PushStyle(ImGuiStyleVar.CellPadding, new Vector2(3, ImGui.GetStyle().CellPadding.Y))) {
                            if (ImGui.BeginTable("materialsTable", 4)) {
                                foreach (var (materialSlot, material) in materials) {
                                    ImGui.TableNextColumn();
                                    var t = $"{materialSlot.MaterialString()} {materialSlot.RowString()}";
                                    ImGui.SetNextItemWidth(ImGui.CalcTextSize(t).X + ImGui.GetStyle().FramePadding.X * 2);
                                    ImGui.InputText("##material", ref t, 128, ImGuiInputTextFlags.ReadOnly);
                                    ImGui.TableNextColumn();
                                    ImGui.ColorButton("Diffuse", new Vector4(material.DiffuseR, material.DiffuseG, material.DiffuseB, 1));
                                    ImGui.SameLine();
                                    ImGui.ColorButton("Specular", new Vector4(material.SpecularR, material.SpecularG, material.SpecularB, 1));
                                    ImGui.SameLine();
                                    ImGui.ColorButton("Emissive", new Vector4(material.EmissiveR, material.EmissiveG, material.EmissiveB, 1));
                                    ImGui.TableNextColumn();
                                    ImGui.Text($"{material.Gloss}");
                                    ImGui.TableNextColumn();
                                    ImGui.TextUnformatted($"{material.SpecularA * 100}%");
                                }

                                ImGui.EndTable();
                            }
                        }

                        ImGui.EndTooltip();
                    }
                }

                if (stains != null) {
                    ImGui.SameLine();
                    StainButton(stains.Value.Stain1, new Vector2(s.Y));
                    ImGui.SameLine();
                    StainButton(stains.Value.Stain2, new Vector2(s.Y));
                }

                ImGui.EndGroup();

                var p = ImGui.GetItemRectMax();

                var modName = "Vanilla";

                if (configs.Count > 0) {
                    modName = configs.Count == 1 ? configs.Keys.First() : $"{configs.Count} Mods";

                    ImGui.SetNextItemWidth(p.X - ImGui.GetCursorScreenPos().X - ImGui.GetStyle().ItemSpacing.X - s.Y);
                    ImGui.InputText("##modInfo", ref modName, 64, ImGuiInputTextFlags.ReadOnly);

                    if (ImGui.IsItemHovered())
                        using (ImRaii.Tooltip()) {
                            foreach (var (modDir, modConfigs) in configs) {
                                if (configs.Count > 1) {
                                    ImGui.Text(modDir);
                                    if (ImGui.GetIO().KeyShift) ImGui.Separator();
                                }

                                if (configs.Count > 1 && ImGui.GetIO().KeyShift == false) continue;
                                using (ImRaii.PushIndent(1, configs.Count > 1)) {
                                    if (ImGui.BeginTable("modSettingsTable", 2)) {
                                        ImGui.TableNextColumn();
                                        ImGui.TextDisabled("Priority");
                                        ImGui.TableNextColumn();
                                        ImGui.Text($"{modConfigs.Priority}");
                                        foreach (var (g, l) in modConfigs.Settings) {
                                            ImGui.TableNextColumn();
                                            ImGui.TextDisabled($"{g}");
                                            ImGui.TableNextColumn();
                                            foreach (var sl in l) ImGui.Text(sl);
                                        }

                                        ImGui.EndTable();
                                    }

                                    ImGui.Spacing();
                                }
                            }

                            if (configs.Count > 1 && ImGui.GetIO().KeyShift == false) ImGui.TextDisabled("Hold SHIFT to show mod settings.");
                        }

                    ImGui.SameLine();

                    if (configs.Count == 1) {
                        using (ImRaii.PushFont(UiBuilder.IconFont)) {
                            if (ImGui.Button(FontAwesomeIcon.Link.ToIconString(), new Vector2(s.Y)))
                                if (modManager.TryGetMod(configs.Keys.First(), configs.Keys.First(), out var mod))
                                    communicatorService.SelectTab.Invoke(TabType.Mods, mod);
                        }
                    } else {
                        bool comboOpen;
                        using (ImRaii.PushColor(ImGuiCol.Text, Vector4.Zero)) {
                            comboOpen = ImGui.BeginCombo("##multiModLink", "", ImGuiComboFlags.NoPreview);
                        }

                        if (comboOpen) {
                            foreach (var (modDir, config) in configs) {
                                if (!ImGui.Selectable(modDir + $"##{modDir}")) continue;
                                if (modManager.TryGetMod(modDir, modDir, out var mod)) communicatorService.SelectTab.Invoke(TabType.Mods, mod);
                            }

                            ImGui.EndCombo();
                        }

                        ImGui.GetWindowDrawList().AddText(UiBuilder.IconFont, ImGui.GetFontSize(), ImGui.GetItemRectMin() + ImGui.GetStyle().FramePadding, ImGui.GetColorU32(ImGuiCol.Text), FontAwesomeIcon.Link.ToIconString());
                    }
                } else {
                    ImGui.SetNextItemWidth(p.X - ImGui.GetCursorScreenPos().X);
                    ImGui.InputText("##modInfo", ref modName, 64, ImGuiInputTextFlags.ReadOnly);
                }
            }
        }
    }

    private bool StainButton(byte stainId, Vector2 size, bool tooltip = true, bool isSelected = false) {
        var stain = dataManager.GetExcelSheet<Stain>()?.GetRow(stainId);
        return StainButton(stain, size, tooltip, isSelected);
    }

    private bool StainButton(Stain? stain, Vector2 size, bool tooltip = true, bool isSelected = false) {
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
            if (ImGui.IsItemHovered()) dl.AddImage(texture.ImGuiHandle, center - drawOffset, center + drawOffset, new Vector2(0.55555f, 0.3529f), new Vector2(0.83333f, 0.64705f));

            if (tooltip && ImGui.IsItemHovered()) ImGui.SetTooltip("No Dye");

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
        if (isSelected || ImGui.IsItemHovered()) dl.AddImage(texture.ImGuiHandle, center - drawOffset, center + drawOffset, new Vector2(0.55555f, 0.3529f), new Vector2(0.83333f, 0.64705f));

        if (tooltip && ImGui.IsItemHovered()) ImGui.SetTooltip(stain.Name.ToMacroString());

        return ImGui.IsItemClicked();
    }
}
