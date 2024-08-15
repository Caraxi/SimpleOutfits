namespace SimpleOutfits.Outfits;

public class ActiveOutfit : Outfit {
    public string OwnerName { get; init; }

    public override bool Equals(object? obj) {
        if (obj is ActiveOutfit ao) return ao.OwnerName == OwnerName;
        return false;
    }
}
