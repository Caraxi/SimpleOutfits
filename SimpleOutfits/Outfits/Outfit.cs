using System.Collections.Generic;
using Penumbra.GameData.Enums;
using Penumbra.Mods.Settings;
using SimpleOutfits.Interop.Glamourer;

namespace SimpleOutfits.Outfits;

public record OutfitModConfig(bool Enabled, ModPriority Priority, Dictionary<string, List<string>> Settings) {
    public static implicit operator OutfitModConfig((bool Enabled, ModPriority Priority, Dictionary<string, List<string>> Settings) a) => new(a.Enabled, a.Priority, a.Settings);
    public static implicit operator (bool Enabled, ModPriority Priority, Dictionary<string, List<string>> Settings)(OutfitModConfig a) => (a.Enabled, a.Priority, a.Settings);
}

public class Outfit : IOutfit {
    public Outfit AsOutfit() => this;

    public GlamourerState GlamourerState = new();
    public Dictionary<EquipSlot, Dictionary<string, OutfitModConfig>> EquipModConfigs = new();
    public Dictionary<string, OutfitModConfig> HairModConfigs = new();

    public override bool Equals(object? obj) {
        if (ReferenceEquals(obj, this)) return true;
        if (obj is Outfit o) return EquipModConfigs.Equals(o.EquipModConfigs) && HairModConfigs.Equals(o.HairModConfigs) && GlamourerState.Equals(o.GlamourerState);
        return false;
    }
}
