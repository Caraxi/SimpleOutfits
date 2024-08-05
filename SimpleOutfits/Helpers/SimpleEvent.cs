using System;
using Dalamud.Plugin;

namespace SimpleOutfits.Helpers;


public class SimpleEvent : IDisposable {
    private readonly object eventSubscriber;

    public delegate Penumbra.Api.Helpers.EventSubscriber GetPenumbraEventSubscriber(IDalamudPluginInterface pluginInterface, params Action[] actions);
    public delegate Glamourer.Api.Helpers.EventSubscriber GetGlamourerEventSubscriber(IDalamudPluginInterface pluginInterface, params Action[] actions);
    
    public SimpleEvent(IDalamudPluginInterface pluginInterface, GetPenumbraEventSubscriber getSubscriberFunction) {
        eventSubscriber = getSubscriberFunction(pluginInterface, OnEventTriggered);
    }
    
    public SimpleEvent(IDalamudPluginInterface pluginInterface, GetGlamourerEventSubscriber getSubscriberFunction) {
        eventSubscriber = getSubscriberFunction(pluginInterface, OnEventTriggered);
    }
    
    public event Action? Triggered;
    private void OnEventTriggered() => Triggered?.Invoke();

    public void Dispose() {
        Triggered = null;
        if (eventSubscriber is IDisposable disposable) disposable.Dispose();
    }
    
}