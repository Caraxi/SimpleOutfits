using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace SimpleOutfits.Interop.Glamourer;

public class GlamourerState {
    public GlamourerEquipment Equipment = new();
    public GlamourerBonuses? Bonus;
    public GlamourerCustomize? Customize;
    public GlamourerParameters? Parameters;
    public Dictionary<string, GlamourerMaterial>? Materials;
    
    
    
    
    public static implicit operator GlamourerState?(JObject? jObject) {
        return jObject == null ? new GlamourerState() : jObject.ToObject<GlamourerState>();
    }
    
    
    
    
    
}