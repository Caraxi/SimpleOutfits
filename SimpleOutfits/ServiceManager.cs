using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace SimpleOutfits;

public interface IInitializeable {
    public void Initialize();
}

public class ServiceManager : IDisposable {
    private IPluginLog pluginLog;
    private Dictionary<Type, object> services = [];
    private Dictionary<Type, object> unownedServices = [];
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly OtterGui.Services.ServiceManager penumbraServiceManager;

    public ServiceManager(IDalamudPluginInterface pluginInterface, IPluginLog pluginLog, OtterGui.Services.ServiceManager penumbraServiceManager) {
        this.pluginInterface = pluginInterface;
        this.pluginLog = pluginLog;
        this.penumbraServiceManager = penumbraServiceManager;
        services.Add(typeof(ServiceManager), this);
    }

    public void AddExisting(object service) {
        if (unownedServices.ContainsKey(service.GetType())) throw new Exception("Service already exists");
        unownedServices.Add(service.GetType(), service);
    }

    public void AddPenumbraService<T>() where T : class {
        AddExisting(penumbraServiceManager.GetService<T>());
    }

    public T GetOrCreateService<T>() where T : class {
        if (services.ContainsKey(typeof(T))) return services[typeof(T)] as T ?? throw new Exception("Service dictionary is corrupt.");
        var service = services[typeof(T)] = Create<T>();
        return service as T ?? throw new Exception($"Failed to create service {nameof(T)}");
    }

    public T Create<T>(params object[] extras) where T : class {
        var a = new List<object>();
        a.AddRange(extras);
        a.AddRange(services.Values);
        a.AddRange(unownedServices.Values);

        foreach (var s in a) {
            pluginLog.Debug($"Available Service: {s.GetType()}");
        }

        var obj = pluginInterface.Create<T>(a.ToArray()) ?? throw new Exception($"Failed to create {nameof(T)}");
        if (obj is IInitializeable initializeable) initializeable.Initialize();
        return obj;
    }

    public void Dispose() {
        foreach (var service in services.Values.Reverse()) {
            if (service == this) continue;
            if (service is IDisposable disposable) {
                disposable.Dispose();
            }
        }

        unownedServices.Clear();
    }
}
