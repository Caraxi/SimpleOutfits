using System;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Penumbra.Collections.Manager;
using Penumbra.GameData.Actors;
using SimpleOutfits.BootstrapPlugin;
using SimpleOutfits.Helpers;
using SimpleOutfits.Interop;

namespace SimpleOutfits;

public class Plugin : IBootstrapPlugin {

    private ServiceManager serviceManager;
    private WindowSystem windowSystem = new(nameof(SimpleOutfits));
    private ConfigWindow configWindow;
    
    
    public Plugin(IDalamudPluginInterface pluginInterface, IPluginLog pluginLog, Penumbra.Penumbra penumbra, OtterGui.Services.ServiceManager penumbraServiceManager) {
        serviceManager = pluginInterface.Create<ServiceManager>(penumbra, penumbraServiceManager) ?? throw new Exception($"Failed to create {nameof(ServiceManager)}");
        
        serviceManager.GetOrCreateService<GameDataHelper>();
        serviceManager.GetOrCreateService<GlamourerHelper>();
        
        serviceManager.AddExisting(penumbra);
        serviceManager.AddPenumbraService<CollectionManager>();
        serviceManager.AddPenumbraService<ActorManager>();
        
        
        
        configWindow = serviceManager.Create<ConfigWindow>();


        
        

        configWindow.IsOpen = true;
        
        windowSystem.AddWindow(configWindow);

        pluginInterface.UiBuilder.Draw += windowSystem.Draw;
    }
    
    

    public void Dispose() {
        windowSystem.RemoveAllWindows();
        serviceManager.Dispose();
    }
}
