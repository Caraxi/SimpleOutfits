using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Newtonsoft.Json;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.GameData.Actors;
using Penumbra.GameData.Enums;
using Penumbra.Mods;
using SimpleOutfits.Interop;
using SimpleOutfits.Interop.Glamourer;
using SimpleOutfits.Outfits;

namespace SimpleOutfits.Services;

public class OutfitManager(ActorManager actorManager, GlamourerHelper glamourerHelper, IClientState clientState, CollectionManager collectionManager, ItemManager itemManager, IDalamudPluginInterface pluginInterface, IPluginLog pluginLog) {
    public DirectoryInfo OutfitDirectory { get; } = new(Path.Join(pluginInterface.GetPluginConfigDirectory(), "Outfits"));

    private ReadOnlyDictionary<string, SavedOutfit>? savedOutfits;

    public ReadOnlyDictionary<string, SavedOutfit> GetSavedOutfits() {
        if (savedOutfits != null) return savedOutfits;
        var outfits = new Dictionary<string, SavedOutfit>();

        if (OutfitDirectory.Exists) {
            foreach (var outfitFile in OutfitDirectory.GetFiles("*.json", SearchOption.AllDirectories)) {
                outfits.Add(Path.GetRelativePath(OutfitDirectory.FullName, outfitFile.FullName[..^outfitFile.Extension.Length]), new SavedOutfit(outfitFile, this));
            }
        }

        savedOutfits = new ReadOnlyDictionary<string, SavedOutfit>(outfits);
        return savedOutfits;
    }

    public bool TryGetOutfitForLocalCharacter([NotNullWhen(true)] out IOutfit? outfit) {
        return TryGetOutfit(clientState.LocalPlayer, out outfit);
    }

    private bool TryGetCollection(IGameObject gameObject, [NotNullWhen(true)] out ModCollection? collection) {
        collection = null;

        var identifier = actorManager.FromObject(gameObject, true, false, false);
        if (!identifier.IsValid) return false;

        collection = collectionManager.Active.Individual(identifier);
        return true;
    }

    public bool TryGetOutfit(IGameObject? gameObject, [NotNullWhen(true)] out IOutfit? outfitInterface) {
        outfitInterface = null;
        if (gameObject == null) return false;
        if (gameObject.GameObjectId == 0xE0000000) return false;

        var outfit = new ActiveOutfit { OwnerName = gameObject.Name.TextValue, GlamourerState = glamourerHelper.GetState(gameObject) ?? new GlamourerState() };

        if (TryGetCollection(gameObject, out var collection)) {
            var modifiedItems = collection.GetChangedItems();
            foreach (var (slot, i) in outfit.GlamourerState.Equipment.Items) {
                var item = itemManager.Resolve(slot, i.ItemId);
                if (modifiedItems.TryGetValue(item.Name, out var modInfo) && modInfo.Item1.Count > 0) {
                    var slotConfigs = new Dictionary<string, OutfitModConfig>();

                    foreach (var m in modInfo.Item1) {
                        if (m is not Mod mod) continue;
                        var modSettings = collection.Settings[mod.Index]?.ConvertToShareable(mod);
                        if (modSettings != null) {
                            slotConfigs.Add(mod.Identifier, modSettings.Value);
                        }
                    }

                    if (slotConfigs.Count > 0) outfit.EquipModConfigs.Add(slot, slotConfigs);
                }
            }

            var customize = outfit.GlamourerState.Customize;

            var genderRace = Names.CombinedRace((Gender)(customize.Gender.Value + 1), (ModelRace)(customize.Race.Value + (customize.Clan.Value == 1 ? 0 : 1)));
            var hairstyleName = $"Customization: {genderRace.Split().Item2.ToName()} {genderRace.Split().Item1.ToName()} Hair (Hair) {outfit.GlamourerState.Customize.Hairstyle.Value}";

            if (modifiedItems.TryGetValue(hairstyleName, out var hairstyleMods)) {
                foreach (var m in hairstyleMods.Item1) {
                    if (m is not Mod mod) continue;
                    var modSettings = collection.Settings[mod.Index]?.ConvertToShareable(mod);
                    if (modSettings != null) {
                        outfit.HairModConfigs.Add(mod.Identifier, modSettings.Value);
                    }
                }
            }
        }

        outfitInterface = outfit;
        return true;
    }

    public bool TryLoadSavedOutfit(FileInfo file, [NotNullWhen(true)] out Outfit? outfit) {
        outfit = null;
        try {
            if (!file.Exists) return false;
            var json = File.ReadAllText(file.FullName);
            outfit = JsonConvert.DeserializeObject<Outfit>(json);
            return outfit != null;
        } catch (Exception ex) {
            pluginLog.Error(ex, "Error loading Outfit");
            return false;
        }
    }

    public bool TrySaveOutfit(Outfit outfit, string name, [NotNullWhen(false)] out string? errorMessage, [NotNullWhen(true)] out SavedOutfit? savedOutfit, bool overwrite = false) {
        errorMessage = null;
        savedOutfit = null;
        try {
            if (string.IsNullOrWhiteSpace(name)) {
                errorMessage = "Name is invalid";
                return false;
            }

            var file = new FileInfo(Path.Join(pluginInterface.GetPluginConfigDirectory(), "Outfits", name.Trim() + ".json"));
            if (file.Exists && overwrite == false) {
                errorMessage = "Outfit already exists";
                return false;
            }

            var j1 = JsonConvert.SerializeObject(outfit);
            var o = JsonConvert.DeserializeObject<Outfit>(j1);
            var j2 = JsonConvert.SerializeObject(o, Formatting.Indented);

            file.Directory?.Create();
            File.WriteAllText(file.FullName, j2);
            savedOutfits = null;
            if (GetSavedOutfits().TryGetValue(name.Trim(), out savedOutfit)) {
                return true;
            }

            errorMessage = "Failed to save outfit - Unknown Error";
            return false;
        } catch (Exception ex) {
            pluginLog.Error(ex, "Error saving outfit");
            errorMessage = ex.Message;
            return false;
        }
    }
}
