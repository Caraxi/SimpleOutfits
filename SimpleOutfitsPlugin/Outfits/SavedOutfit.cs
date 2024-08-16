using System;
using System.Diagnostics;
using System.IO;
using SimpleOutfitsPlugin.Helpers;
using SimpleOutfitsPlugin.Services;

namespace SimpleOutfitsPlugin.Outfits;

public class SavedOutfit(FileInfo file, OutfitManager outfitManager) : IOutfit, IPathedName {
    public string Name => file.Name[..^file.Extension.Length];

    public string NameWithPath => file.FullName[outfitManager.OutfitDirectory.FullName.Length..^file.Extension.Length];

    public FileInfo File => file;

    private Outfit? outfit;
    private Stopwatch lastAccessed = Stopwatch.StartNew();

    public Outfit AsOutfit() {
        if (lastAccessed.ElapsedMilliseconds > 10000) outfit = null;
        lastAccessed.Restart();
        if (outfit != null) return outfit;
        outfitManager.TryLoadSavedOutfit(file, out outfit);
        return outfit ?? throw new Exception("Failed to load Outfit");
    }

    public static implicit operator Outfit(SavedOutfit a) {
        return a.AsOutfit();
    }

    public override bool Equals(object? obj) {
        if (obj is SavedOutfit so) return so.File.FullName == File.FullName;
        return false;
    }
}
