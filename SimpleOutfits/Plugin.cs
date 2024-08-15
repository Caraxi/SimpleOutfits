using System;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Penumbra.Collections.Manager;
using Penumbra.GameData.Actors;
using Penumbra.GameData.Data;
using Penumbra.GameData.DataContainers;
using Penumbra.Mods.Manager;
using Penumbra.Services;
using SimpleOutfits.BootstrapPlugin;
using SimpleOutfits.Interop;
using SimpleOutfits.Services;

namespace SimpleOutfits;

public class Plugin : IBootstrapPlugin {
    private ServiceManager serviceManager;
    private WindowSystem windowSystem = new(nameof(SimpleOutfits));
    private ConfigWindow configWindow;

    public static IPluginLog Log;

    public Plugin(IDalamudPluginInterface pluginInterface, IPluginLog pluginLog, Penumbra.Penumbra penumbra, OtterGui.Services.ServiceManager penumbraServiceManager) {
        Log = pluginLog;
        serviceManager = pluginInterface.Create<ServiceManager>(penumbra, penumbraServiceManager) ?? throw new Exception($"Failed to create {nameof(ServiceManager)}");

        serviceManager.AddExisting(penumbra);
        serviceManager.AddPenumbraService<CollectionManager>();
        serviceManager.AddPenumbraService<ActorManager>();
        serviceManager.AddPenumbraService<CommunicatorService>();
        serviceManager.AddPenumbraService<ObjectIdentification>();
        serviceManager.AddPenumbraService<ItemData>();
        serviceManager.AddPenumbraService<DictStain>();
        serviceManager.AddPenumbraService<DictBonusItems>();
        serviceManager.AddPenumbraService<ModManager>();

        serviceManager.GetOrCreateService<GlamourerHelper>();
        serviceManager.GetOrCreateService<ItemManager>();
        serviceManager.GetOrCreateService<OutfitManager>();

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
