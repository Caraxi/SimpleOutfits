using System.Collections.Generic;

namespace SimpleOutfits.Interop.Glamourer;

// ReSharper disable UnassignedField.Global
public class GlamourerEquipment {
    public GlamourerItem MainHand = new();
    public GlamourerItem OffHand = new();
    public GlamourerItem Head = new();
    public GlamourerItem Body = new();
    public GlamourerItem Hands = new();
    public GlamourerItem Legs = new();
    public GlamourerItem Feet = new();
    public GlamourerItem Ears = new();
    public GlamourerItem Neck = new();
    public GlamourerItem Wrists = new();
    public GlamourerItem RFinger = new();
    public GlamourerItem LFinger = new();
    
    public GlamourerVisibility Hat = new();
    public GlamourerVisorState Visor = new();
    public GlamourerVisibility Weapon = new();
    
    public IEnumerable<GlamourerItem> Items {
        get {
            yield return Head;
            yield return Body;
            yield return Hands;
            yield return Legs;
            yield return Feet;
            yield return Ears;
            yield return Neck;
            yield return Wrists;
            yield return RFinger;
            yield return LFinger;
            yield return MainHand;
            yield return OffHand;
        }
    }
    
    
}
