﻿using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Dalamud.Plugin.Services;
using Lumina.Excel;
using Penumbra.GameData.Data;
using Penumbra.GameData.DataContainers;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;

// Lifted directly from Glamourer
//      https://github.com/Ottermandias/Glamourer/blob/1cc7c2f0cd6a4bc738044a63227c9c6e7ff19848/Glamourer/Services/ItemManager.cs

namespace SimpleOutfits.Services;

public class ItemManager {
    public const string Nothing = "Nothing";
    public const string SmallClothesNpc = "Smallclothes (NPC)";
    public const ushort SmallClothesNpcModel = 9903;

    public readonly ObjectIdentification ObjectIdentification;
    public readonly ExcelSheet<Lumina.Excel.GeneratedSheets.Item> ItemSheet;
    public readonly DictStain Stains;
    public readonly ItemData ItemData;
    public readonly DictBonusItems DictBonusItems;

    public readonly EquipItem DefaultSword;

    public ItemManager(IDataManager gameData, ObjectIdentification objectIdentification, ItemData itemData, DictStain stains, DictBonusItems dictBonusItems) {
        ItemSheet = gameData.GetExcelSheet<Lumina.Excel.GeneratedSheets.Item>()!;
        ObjectIdentification = objectIdentification;
        ItemData = itemData;
        Stains = stains;
        DictBonusItems = dictBonusItems;
        DefaultSword = EquipItem.FromMainhand(ItemSheet.GetRow(1601)!);
    }

    public static ItemId NothingId(EquipSlot slot) => uint.MaxValue - 128 - (uint)slot.ToSlot();

    public static ItemId SmallclothesId(EquipSlot slot) => uint.MaxValue - 256 - (uint)slot.ToSlot();

    public static ItemId NothingId(FullEquipType type) => uint.MaxValue - 384 - (uint)type;

    public static EquipItem NothingItem(EquipSlot slot) => new(Nothing, NothingId(slot), 0, 0, 0, 0, slot.ToEquipType(), 0, 0, 0);

    public static EquipItem NothingItem(FullEquipType type) => new(Nothing, NothingId(type), 0, 0, 0, 0, type, 0, 0, 0);

    public static EquipItem SmallClothesItem(EquipSlot slot) => new(SmallClothesNpc, SmallclothesId(slot), 0, SmallClothesNpcModel, 0, 1, slot.ToEquipType(), 0, 0, 0);

    public EquipItem Resolve(EquipSlot slot, CustomItemId itemId) {
        slot = slot.ToSlot();
        if (itemId == NothingId(slot))
            return NothingItem(slot);
        if (itemId == SmallclothesId(slot))
            return SmallClothesItem(slot);

        if (!itemId.IsItem) {
            var item = EquipItem.FromId(itemId);
            item = slot is EquipSlot.MainHand or EquipSlot.OffHand ? Identify(slot, item.PrimaryId, item.SecondaryId, item.Variant) : Identify(slot, item.PrimaryId, item.Variant);
            return item;
        } else if (!ItemData.TryGetValue(itemId.Item, slot, out var item)) {
            return EquipItem.FromId(itemId);
        } else {
            if (item.Type.ToSlot() != slot)
                return new EquipItem(string.Intern($"Invalid #{itemId}"), itemId, item.IconId, item.PrimaryId, item.SecondaryId, item.Variant, 0, 0, 0, 0);

            return item;
        }
    }

    public EquipItem Resolve(FullEquipType type, ItemId itemId) {
        if (itemId == NothingId(type))
            return NothingItem(type);

        if (!ItemData.TryGetValue(itemId, type is FullEquipType.Shield ? EquipSlot.MainHand : EquipSlot.OffHand, out var item))
            return EquipItem.FromId(itemId);

        if (item.Type != type)
            return new EquipItem(string.Intern($"Invalid #{itemId}"), itemId, item.IconId, item.PrimaryId, item.SecondaryId, item.Variant, 0, 0, 0, 0);

        return item;
    }

    public EquipItem Resolve(FullEquipType type, CustomItemId id) => id.IsItem ? Resolve(type, id.Item) : EquipItem.FromId(id);

    public EquipItem Identify(EquipSlot slot, PrimaryId id, Variant variant) {
        slot = slot.ToSlot();
        if (slot.ToIndex() == uint.MaxValue)
            return new EquipItem($"Invalid ({id.Id}-{variant})", 0, 0, id, 0, variant, 0, 0, 0, 0);

        switch (id.Id) {
            case 0: return NothingItem(slot);
            case SmallClothesNpcModel: return SmallClothesItem(slot);
            default:
                var item = ObjectIdentification.Identify(id, 0, variant, slot).FirstOrDefault();
                return item.Valid ? item : EquipItem.FromIds(0, 0, id, 0, variant, slot.ToEquipType());
        }
    }

    public BonusItem Identify(BonusItemFlag slot, PrimaryId id, Variant variant) {
        var index = slot.ToIndex();
        if (index == uint.MaxValue)
            return new BonusItem($"Invalid ({id.Id}-{variant})", 0, 0, id, variant, slot);

        if (id.Id == 0)
            return BonusItem.Empty(slot);

        return ObjectIdentification.Identify(id, variant, slot).FirstOrDefault(new BonusItem($"Invalid ({id.Id}-{variant})", 0, 0, id, variant, slot));
    }

    public BonusItem Resolve(BonusItemFlag slot, BonusItemId id) => IsBonusItemValid(slot, id, out var item) ? item : new BonusItem($"Invalid ({id.Id})", 0, id, 0, 0, slot);

    /// <summary> Return the default offhand for a given mainhand, that is for both handed weapons, return the correct offhand part, and for everything else Nothing. </summary>
    public EquipItem GetDefaultOffhand(EquipItem mainhand) {
        var offhandType = mainhand.Type.ValidOffhand();
        if (offhandType.IsOffhandType())
            return Resolve(offhandType, mainhand.ItemId);

        return NothingItem(offhandType);
    }

    public EquipItem Identify(EquipSlot slot, PrimaryId id, SecondaryId type, Variant variant, FullEquipType mainhandType = FullEquipType.Unknown) {
        if (slot is EquipSlot.OffHand) {
            var weaponType = mainhandType.ValidOffhand();
            if (id.Id == 0)
                return NothingItem(weaponType);
        }

        if (slot is not EquipSlot.MainHand and not EquipSlot.OffHand)
            return new EquipItem($"Invalid ({id.Id}-{type.Id}-{variant})", 0, 0, id, type, variant, 0, 0, 0, 0);

        var item = ObjectIdentification.Identify(id, type, variant, slot).FirstOrDefault(i => i.Type.ToSlot() == slot);
        return item.Valid ? item : EquipItem.FromIds(0, 0, id, type, variant, slot.ToEquipType());
    }

    /// <summary> Returns whether an item id represents a valid item for a slot and gives the item. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool IsItemValid(EquipSlot slot, CustomItemId itemId, out EquipItem item) {
        item = Resolve(slot, itemId);
        return item.Valid;
    }

    /// <summary> Returns whether a bonus item id represents a valid item for a slot and gives the item. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool IsBonusItemValid(BonusItemFlag slot, BonusItemId itemId, out BonusItem item) {
        if (itemId.Id != 0)
            return DictBonusItems.TryGetValue(itemId, out item) && slot == item.Slot;

        item = BonusItem.Empty(slot);
        return true;
    }

    /// <summary>
    /// Check whether an item id resolves to an existing item of the correct slot (which should not be weapons.)
    /// The returned item is either the resolved correct item, or the Nothing item for that slot.
    /// The return value is an empty string if there was no problem and a warning otherwise.
    /// </summary>
    public string ValidateItem(EquipSlot slot, CustomItemId itemId, out EquipItem item, bool allowUnknown) {
        if (slot is EquipSlot.MainHand or EquipSlot.OffHand)
            throw new Exception("Internal Error: Used armor functionality for weapons.");

        if (!itemId.IsItem) {
            var (id, _, variant, _) = itemId.Split;
            item = Identify(slot, id, variant);
            return allowUnknown ? string.Empty : $"The item {itemId} yields an unknown item.";
        }

        if (IsItemValid(slot, itemId.Item, out item))
            return string.Empty;

        item = NothingItem(slot);
        return $"The {slot.ToName()} item {itemId} does not exist, reset to Nothing.";
    }

    /// <summary> Returns whether a stain id is a valid stain. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool IsStainValid(StainId stain) => stain.Id == 0 || Stains.ContainsKey(stain);

    /// <summary>
    /// Check whether a stain id is an existing stain. 
    /// The returned stain id is either the input or 0.
    /// The return value is an empty string if there was no problem and a warning otherwise.
    /// </summary>
    public string ValidateStain(StainIds stains, out StainIds ret, bool allowUnknown) {
        if (allowUnknown || stains.All(IsStainValid)) {
            ret = stains;
            return string.Empty;
        }

        ret = StainIds.None;
        return $"The Stain {stains} does not exist, reset to unstained.";
    }

    /// <summary> Returns whether an offhand is valid given the required offhand type. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool IsOffhandValid(FullEquipType offType, ItemId offId, out EquipItem off) {
        off = Resolve(offType, offId);
        return offType == FullEquipType.Unknown || off.Valid;
    }

    /// <summary> Returns whether an offhand is valid given mainhand. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool IsOffhandValid(in EquipItem main, ItemId offId, out EquipItem off) => IsOffhandValid(main.Type.ValidOffhand(), offId, out off);

    /// <summary>
    /// Check whether a combination of an item id for a mainhand and for an offhand is valid.
    /// The returned items are either the resolved correct items,
    /// the correct mainhand and an appropriate offhand (implicit offhand or nothing),
    /// or the default sword and a nothing offhand.
    /// The return value is an empty string if there was no problem and a warning otherwise.
    /// </summary>
    public string ValidateWeapons(CustomItemId mainId, CustomItemId offId, out EquipItem main, out EquipItem off, bool allowUnknown) {
        var ret = string.Empty;
        if (!mainId.IsItem) {
            var (id, weapon, variant, _) = mainId.Split;
            main = Identify(EquipSlot.MainHand, id, weapon, variant);
            if (!allowUnknown) {
                ret = $"The item {mainId} yields an unknown item, reset to default sword.";
                main = DefaultSword;
            }
        } else if (!IsItemValid(EquipSlot.MainHand, mainId.Item, out main)) {
            main = DefaultSword;
            ret = $"The mainhand weapon {mainId} does not exist, reset to default sword.";
        }

        if (!offId.IsItem) {
            var (id, weapon, variant, _) = offId.Split;
            off = Identify(EquipSlot.OffHand, id, weapon, variant, main.Type);
            if (!allowUnknown) {
                if (!FullEquipTypeExtensions.OffhandTypes.Contains(main.Type.ValidOffhand())) {
                    main = DefaultSword;
                    off = NothingItem(FullEquipType.Shield);
                    return $"The offhand weapon {offId} does not exist, but no default could be restored, reset mainhand to default sword and offhand to nothing.";
                }

                if (ret.Length > 0)
                    ret += '\n';
                ret += $"The item {offId} yields an unknown item, reset to implied offhand.";
                off = GetDefaultOffhand(main);
            }
        } else if (!IsOffhandValid(main.Type.ValidOffhand(), offId.Item, out off)) {
            if (!FullEquipTypeExtensions.OffhandTypes.Contains(main.Type.ValidOffhand())) {
                main = DefaultSword;
                off = NothingItem(FullEquipType.Shield);
                return $"The offhand weapon {offId} does not exist, but no default could be restored, reset mainhand to default sword and offhand to nothing.";
            }

            if (ret.Length > 0)
                ret += '\n';
            ret += $"The offhand weapon {mainId} does not exist or is of invalid type, reset to default sword.";
            off = GetDefaultOffhand(main);
        }

        return ret;
    }
}
