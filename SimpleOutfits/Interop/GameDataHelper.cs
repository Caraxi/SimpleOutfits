using System.Collections.Generic;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using Lumina.Excel.GeneratedSheets;
using SimpleOutfits.Interop.Glamourer;

namespace SimpleOutfits.Interop;

public class GameDataHelper(IDataManager dataManager) {
    private readonly Dictionary<uint, string> itemNames = new();
    private readonly Dictionary<uint, ushort> itemIcons = new();
    
    public string GetItemName(GlamourerItem item) {
        if ((item.ItemId & 0xFFFFFF00) == 0xFFFFFF00) return "Nothing";
        if ((item.ItemId & 0xFFFFFE00) == 0xFFFFFE00) return "Smallclothes (NPC)";
        return item.ItemId is >= uint.MinValue and <= uint.MaxValue ? GetItemName((uint)item.ItemId) : $"GlamourerItem#{item.ItemId}";
    }
    
    public string GetItemName(uint itemId) {
        if (itemNames.TryGetValue(itemId, out var name)) return name;
        name = dataManager.GetExcelSheet<Item>()?.GetRow(itemId)?.Name.ToDalamudString().TextValue ?? $"Item#{itemId:X}";
        itemNames[itemId] = name;
        return name;
    }

    public uint GetItemIcon(GlamourerItem item) {
        return item.ItemId switch {
            0xFFFFFF7C or 0xFFFFFEFC => GetItemIcon(10032),
            0xFFFFFF7B or 0xFFFFFEFB => GetItemIcon(10033),
            0xFFFFFF7A or 0xFFFFFEFA => GetItemIcon(10034),
            0xFFFFFF78 or 0xFFFFFEF8 => GetItemIcon(10035),
            0xFFFFFF77 or 0xFFFFFEF7 => GetItemIcon(10036),
            0xFFFFFF76 or 0xFFFFFEF6 => GetItemIcon(9293),
            0xFFFFFF75 or 0xFFFFFEF5 => GetItemIcon(9292),
            0xFFFFFF74 or 0xFFFFFEF4 => GetItemIcon(9294),
            0xFFFFFF73 or 0xFFFFFEF3 => GetItemIcon(9295),
            >= uint.MinValue and <= uint.MaxValue => GetItemIcon((uint)item.ItemId),
            _ => 0,
        };
    }
    
    public uint GetItemIcon(uint itemId) {
        if (itemIcons.TryGetValue(itemId, out var icon)) return icon;
        icon = dataManager.GetExcelSheet<Item>()?.GetRow(itemId)?.Icon ?? ushort.MinValue;
        itemIcons[itemId] = icon;
        return icon;
    }
    
}
