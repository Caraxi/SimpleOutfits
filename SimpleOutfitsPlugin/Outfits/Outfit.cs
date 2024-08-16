using System.Collections.Generic;
using Penumbra.GameData.Enums;
using Penumbra.Mods.Settings;
using SimpleOutfitsPlugin.Interop.Glamourer;

namespace SimpleOutfitsPlugin.Outfits;

public record OutfitModConfig(bool Enabled, ModPriority Priority, Dictionary<string, List<string>> Settings) {
    public static implicit operator OutfitModConfig((bool Enabled, ModPriority Priority, Dictionary<string, List<string>> Settings) a) {
        return new OutfitModConfig(a.Enabled, a.Priority, a.Settings);
    }

    public static implicit operator (bool Enabled, ModPriority Priority, Dictionary<string, List<string>> Settings)(OutfitModConfig a) {
        return (a.Enabled, a.Priority, a.Settings);
    }
}

public class Outfit : IOutfit {
    public Outfit AsOutfit() {
        return this;
    }

    public GlamourerState GlamourerState = new();
    public Dictionary<EquipSlot, Dictionary<string, OutfitModConfig>> EquipModConfigs = new();
    public Dictionary<string, OutfitModConfig> HairModConfigs = new();

    public override bool Equals(object? obj) {
        if (ReferenceEquals(obj, this)) return true;
        if (obj is Outfit o) return EquipModConfigs.Equals(o.EquipModConfigs) && HairModConfigs.Equals(o.HairModConfigs) && GlamourerState.Equals(o.GlamourerState);
        return false;
    }
}
