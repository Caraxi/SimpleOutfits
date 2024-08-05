using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Glamourer.Api.Enums;
using Glamourer.Api.IpcSubscribers;
using SimpleOutfits.Helpers;
using SimpleOutfits.Interop.Glamourer;

namespace SimpleOutfits.Interop;

public class GlamourerHelper(IDalamudPluginInterface pluginInterface, IPluginLog log) {
    
    public SimpleEvent GlamourerInitialized { get; set; } =  new(pluginInterface, Initialized.Subscriber);
    public SimpleEvent GlamourerDisposed { get; set; } = new(pluginInterface, Disposed.Subscriber);
    
    private readonly ApiVersion getApiVersion = new(pluginInterface);
    public bool Available() {
        if (getApiVersion.Valid == false) return false;
        return getApiVersion.Invoke() is { Major: 1, Minor: >= 2 };
    }

    private readonly GetState getState = new(pluginInterface);
    public GlamourerState? GetState(IGameObject gameObject, out GlamourerApiEc? ec) {
        ec = null;
        if (!Available()) return null;
        var state = getState.Invoke(gameObject.ObjectIndex);
        ec = state.Item1;
        return ec != GlamourerApiEc.Success ? null : state.Item2;
    }

    public GlamourerState? GetState(IGameObject gameObject) => GetState(gameObject, out _);

}
