using System;
using Dalamud.Plugin;

namespace SimpleOutfitsPlugin.Helpers;

public class SimpleEvent : IDisposable {
    private readonly object _eventSubscriber;

    public delegate Penumbra.Api.Helpers.EventSubscriber GetPenumbraEventSubscriber(IDalamudPluginInterface pluginInterface, params Action[] actions);

    public delegate Glamourer.Api.Helpers.EventSubscriber GetGlamourerEventSubscriber(IDalamudPluginInterface pluginInterface, params Action[] actions);

    public SimpleEvent(IDalamudPluginInterface pluginInterface, GetPenumbraEventSubscriber getSubscriberFunction) {
        _eventSubscriber = getSubscriberFunction(pluginInterface, OnEventTriggered);
    }

    public SimpleEvent(IDalamudPluginInterface pluginInterface, GetGlamourerEventSubscriber getSubscriberFunction) {
        _eventSubscriber = getSubscriberFunction(pluginInterface, OnEventTriggered);
    }

    public event Action? Triggered;

    private void OnEventTriggered() {
        Triggered?.Invoke();
    }

    public void Dispose() {
        Triggered = null;
        if (_eventSubscriber is IDisposable disposable) disposable.Dispose();
    }
}
