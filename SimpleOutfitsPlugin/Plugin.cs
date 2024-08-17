using System;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Penumbra.Collections.Manager;
using Penumbra.GameData.Actors;
using Penumbra.GameData.Data;
using Penumbra.GameData.DataContainers;
using Penumbra.Mods.Manager;
using Penumbra.Services;
using SimpleOutfits;
using SimpleOutfitsPlugin.Interop;
using SimpleOutfitsPlugin.Services;

namespace SimpleOutfitsPlugin;

public class Plugin : IBootstrapPlugin {
    private readonly ServiceManager _serviceManager;
    private readonly WindowSystem _windowSystem = new(nameof(SimpleOutfitsPlugin));
    private readonly ConfigWindow _configWindow;

    public Plugin(IDalamudPluginInterface pluginInterface, Penumbra.Penumbra penumbra, OtterGui.Services.ServiceManager penumbraServiceManager) {
        _serviceManager = pluginInterface.Create<ServiceManager>(penumbra, penumbraServiceManager) ?? throw new Exception($"Failed to create {nameof(ServiceManager)}");

        _serviceManager.AddExisting(penumbra);
        _serviceManager.AddPenumbraService<CollectionManager>();
        _serviceManager.AddPenumbraService<ActorManager>();
        _serviceManager.AddPenumbraService<CommunicatorService>();
        _serviceManager.AddPenumbraService<ObjectIdentification>();
        _serviceManager.AddPenumbraService<ItemData>();
        _serviceManager.AddPenumbraService<DictStain>();
        _serviceManager.AddPenumbraService<DictBonusItems>();
        _serviceManager.AddPenumbraService<ModManager>();

        _serviceManager.GetOrCreateService<GlamourerHelper>();
        _serviceManager.GetOrCreateService<ItemManager>();
        _serviceManager.GetOrCreateService<OutfitManager>();

        _configWindow = _serviceManager.Create<ConfigWindow>();

        _configWindow.IsOpen = true;

        _windowSystem.AddWindow(_configWindow);

        pluginInterface.UiBuilder.Draw += _windowSystem.Draw;
        
        pluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        pluginInterface.UiBuilder.OpenMainUi += ToggleConfigUi;
        
    }

    private void ToggleConfigUi() {
        _configWindow.UncollapseOrToggle();
    }

    public void Dispose() {
        _windowSystem.RemoveAllWindows();
        _serviceManager.Dispose();
    }
}
