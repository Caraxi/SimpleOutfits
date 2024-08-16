using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin;
using Glamourer.Api.Enums;
using Glamourer.Api.IpcSubscribers;
using SimpleOutfitsPlugin.Helpers;
using SimpleOutfitsPlugin.Interop.Glamourer;

namespace SimpleOutfitsPlugin.Interop;

public class GlamourerHelper(IDalamudPluginInterface pluginInterface) {
    public SimpleEvent GlamourerInitialized { get; set; } = new(pluginInterface, Initialized.Subscriber);
    public SimpleEvent GlamourerDisposed { get; set; } = new(pluginInterface, Disposed.Subscriber);

    private readonly ApiVersion getApiVersion = new(pluginInterface);

    public bool Available() {
        if (getApiVersion.Valid == false) return false;
        return getApiVersion.Invoke() is { Major: 1, Minor: >= 2 };
    }

    private readonly GetState getState = new(pluginInterface);
    private readonly GetStateBase64 getStateBase64 = new(pluginInterface);

    public GlamourerState? GetState(IGameObject gameObject, out GlamourerApiEc? ec) {
        ec = null;
        if (!Available()) return null;
        var state = getState.Invoke(gameObject.ObjectIndex);
        ec = state.Item1;
        return ec != GlamourerApiEc.Success ? null : state.Item2;
    }

    public GlamourerState? GetState(IGameObject gameObject) {
        return GetState(gameObject, out _);
    }
}
