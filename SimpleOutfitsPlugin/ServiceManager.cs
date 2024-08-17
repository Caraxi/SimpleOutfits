using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace SimpleOutfitsPlugin;

public interface IInitializeable {
    public void Initialize();
}

public class ServiceManager : IDisposable {
    private readonly IPluginLog _pluginLog;
    private readonly Dictionary<Type, object> _services = [];
    private readonly Dictionary<Type, object> _unownedServices = [];
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly OtterGui.Services.ServiceManager _penumbraServiceManager;

    public ServiceManager(IDalamudPluginInterface pluginInterface, IPluginLog pluginLog, OtterGui.Services.ServiceManager penumbraServiceManager) {
        this._pluginInterface = pluginInterface;
        this._pluginLog = pluginLog;
        this._penumbraServiceManager = penumbraServiceManager;
        _services.Add(typeof(ServiceManager), this);
    }

    public void AddExisting(object service) {
        if (_unownedServices.ContainsKey(service.GetType())) throw new Exception("Service already exists");
        _unownedServices.Add(service.GetType(), service);
    }

    public void AddPenumbraService<T>() where T : class {
        AddExisting(_penumbraServiceManager.GetService<T>());
    }

    public T GetOrCreateService<T>() where T : class {
        if (_services.ContainsKey(typeof(T))) return _services[typeof(T)] as T ?? throw new Exception("Service dictionary is corrupt.");
        var service = _services[typeof(T)] = Create<T>();
        return service as T ?? throw new Exception($"Failed to create service {nameof(T)}");
    }

    public T Create<T>(params object[] extras) where T : class {
        var a = new List<object>();
        a.AddRange(extras);
        a.AddRange(_services.Values);
        a.AddRange(_unownedServices.Values);

        foreach (var s in a) _pluginLog.Debug($"Available Service: {s.GetType()}");

        var obj = _pluginInterface.Create<T>(a.ToArray()) ?? throw new Exception($"Failed to create {nameof(T)}");
        if (obj is IInitializeable initializeable) initializeable.Initialize();
        return obj;
    }

    public void Dispose() {
        foreach (var service in _services.Values.Reverse()) {
            if (service == this) continue;
            if (service is IDisposable disposable) disposable.Dispose();
        }

        _unownedServices.Clear();
    }
}
