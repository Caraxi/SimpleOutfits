using System;
using System.Collections.Generic;
using System.Reflection;
using OtterGui.Classes;
using Penumbra.Collections;
using Penumbra.GameData.Data;
using Penumbra.Mods.Editor;

namespace SimpleOutfitsPlugin.Interop;

public static class PenumbraAccessHelper {
    private static PropertyInfo? _collectionChangedItems;

    public static IReadOnlyDictionary<string, (SingleArray<IMod>, IIdentifiedObjectData?)> GetChangedItems(this ModCollection collection) {
        if (!collection.HasCache) return new Dictionary<string, (SingleArray<IMod>, IIdentifiedObjectData?)>();
        _collectionChangedItems ??= typeof(ModCollection).GetProperty("ChangedItems", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        if (_collectionChangedItems == null || !_collectionChangedItems.CanRead) throw new Exception("Failed to find readable ChangedItems property");
        return _collectionChangedItems.GetValue(collection) as IReadOnlyDictionary<string, (SingleArray<IMod>, IIdentifiedObjectData?)> ?? throw new Exception("Failed to get value of ChangedItems");
    }
}
