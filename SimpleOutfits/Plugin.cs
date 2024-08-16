using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace SimpleOutfits;

public class Plugin : IDalamudPlugin {
    private readonly IPluginLog _pluginLog;
    private BootstrapLoadContext? _loadContext;
    private IBootstrapPlugin? _loadedPlugin;
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly IFramework _framework;

    private IList? _installedPluginsList;

    private IDalamudPlugin? _penumbraPlugin;
    private object? _penumbraLocalPlugin;

    private readonly Dictionary<string, (Assembly assembly, IDalamudPlugin plugin, object localPlugin)?> _monitoredPlugins = new() { ["Penumbra"] = null, ["Glamourer"] = null };

    private bool TryGetLoadedPlugin(string internalName, [NotNullWhen(true)] out IDalamudPlugin? plugin, [NotNullWhen(true)] out object? localPlugin) {
        plugin = null;
        localPlugin = null;
        if (_installedPluginsList == null) {
            var dalamudAssembly = _pluginInterface.GetType().Assembly;
            var service1T = dalamudAssembly.GetType("Dalamud.Service`1");
            if (service1T == null) throw new Exception("Failed to get Service<T> Type");
            var pluginManagerT = dalamudAssembly.GetType("Dalamud.Plugin.Internal.PluginManager");
            if (pluginManagerT == null) throw new Exception("Failed to get PluginManager Type");

            var serviceInterfaceManager = service1T.MakeGenericType(pluginManagerT);
            var getter = serviceInterfaceManager.GetMethod("Get", BindingFlags.Static | BindingFlags.Public);
            if (getter == null) throw new Exception("Failed to get Get<Service<PluginManager>> method");

            var pluginManager = getter.Invoke(null, null);

            if (pluginManager == null) throw new Exception("Failed to get PluginManager instance");

            var installedPluginsListField = pluginManager.GetType().GetField("installedPluginsList", BindingFlags.NonPublic | BindingFlags.Instance);
            if (installedPluginsListField == null) throw new Exception("Failed to get installedPluginsList field");

            _installedPluginsList = (IList?)installedPluginsListField.GetValue(pluginManager);
            if (_installedPluginsList == null) throw new Exception("Failed to get installedPluginsList value");
        }

        PropertyInfo? internalNameProperty = null;

        foreach (var v in _installedPluginsList) {
            internalNameProperty ??= v?.GetType().GetProperty("InternalName");
            if (internalNameProperty == null) continue;
            var installedInternalName = internalNameProperty.GetValue(v) as string;
            if (installedInternalName == internalName && v != null) {
                plugin = v.GetType().GetField("instance", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(v) as IDalamudPlugin;
                localPlugin = v;
                if (plugin != null) return true;
            }
        }

        return false;
    }

    private static (MemberInfo?, object?) GetMember(MemberInfo? originInfo, object origin, params string[] path) {
        if (path.Length == 0) return (originInfo, origin);
        var pathStepSplit = (path[0] + ":0").Split(":");
        var step = pathStepSplit[0];
        if (!ushort.TryParse(pathStepSplit[1], out var stepIndex)) throw new Exception("Invalid Step");

        var members = origin.GetType().GetMember(step, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
        if (members.Length <= stepIndex) return (null, null);

        var next = members[stepIndex];

        switch (next.MemberType) {
            case MemberTypes.Field:
                var fi = (FieldInfo)next;
                var fv = fi.GetValue(origin);
                if (fv == null) throw new Exception("Null Value");
                return GetMember(members[stepIndex], fv, path[1..]);
            case MemberTypes.Property:
                var pi = (PropertyInfo)next;
                if (!pi.CanRead) throw new Exception("Cannot Read Property");
                var pv = pi.GetValue(origin);
                if (pv == null) throw new Exception("Null Value");
                return GetMember(members[stepIndex], pv, path[1..]);
            default:
                throw new Exception($"Unsupported Member Type: {next.MemberType}");
        }
    }

    private static bool TryGetMember([NotNullWhen(true)] out MemberInfo? member, out object? value, object origin, params string[] path) {
        try {
            (member, value) = GetMember(null, origin, path);
            return member != null;
        } catch {
            (member, value) = (null, null);
            return false;
        }
    }

    private static bool TryGetMemberValue([NotNullWhen(true)] out object? value, object origin, params string[] path) {
        return TryGetMember(out _, out value, origin, path) && value != null;
    }

    private static bool TryGetMemberValue<T>([NotNullWhen(true)] out T? value, object origin, params string[] path) where T : class {
        value = null;
        if (!TryGetMemberValue(out var valueObj, origin, path)) return false;
        value = valueObj as T;
        return value != null;
    }

    private static Type? GetServiceType(IEnumerable serviceCollection, string serviceTypeName) {
        foreach (var s in serviceCollection) {
            if (s.GetType().GetProperty("ServiceType")?.GetValue(s) is not Type serviceType) continue;
            if (serviceType.FullName == serviceTypeName) return serviceType;
        }

        return null;
    }

    private static bool TryGetServiceType(IEnumerable serviceCollection, string serviceTypeName, [NotNullWhen(true)] out Type? type) {
        type = GetServiceType(serviceCollection, serviceTypeName);
        return type != null;
    }

    private Stopwatch timeSinceUpdate = Stopwatch.StartNew();

    public Plugin(IDalamudPluginInterface pluginInterface, IPluginLog pluginLog, IFramework framework) {
        _pluginInterface = pluginInterface;
        _pluginLog = pluginLog;
        _framework = framework;

        stateWatcherCancellationTokenSource = new CancellationTokenSource();
        stateWatcherTask = Task.Run(StateWatcher, stateWatcherCancellationTokenSource.Token);
    }

    private void CheckMonitoredPlugins(out bool changeDetected, out bool allAvailable) {
        changeDetected = false;
        foreach (var pluginInternalName in _monitoredPlugins.Keys)
            if (TryGetLoadedPlugin(pluginInternalName, out var plugin, out var localPlugin)) {
                if (_monitoredPlugins[pluginInternalName] == null) {
                    _pluginLog.Info($"Monitored Plugin State Changed: '{pluginInternalName}' is now available.");
                    _monitoredPlugins[pluginInternalName] = (plugin.GetType().Assembly, plugin, localPlugin);
                    changeDetected = true;
                } else if (_monitoredPlugins[pluginInternalName]?.plugin != plugin || _monitoredPlugins[pluginInternalName]?.assembly != plugin.GetType().Assembly || _monitoredPlugins[pluginInternalName]?.localPlugin != localPlugin) {
                    _pluginLog.Info($"Monitored Plugin State Changed: '{pluginInternalName}' has changed.");
                    _monitoredPlugins[pluginInternalName] = (plugin.GetType().Assembly, plugin, localPlugin);
                    changeDetected = true;
                }
            } else {
                if (_monitoredPlugins[pluginInternalName] == null) continue;
                _pluginLog.Info($"Monitored Plugin State Changed: '{pluginInternalName}' is no longer available.");
                _monitoredPlugins[pluginInternalName] = null;
                changeDetected = true;
            }

        allAvailable = _monitoredPlugins.Values.All(v => v != null);
    }

    private CancellationTokenSource stateWatcherCancellationTokenSource;
    private Task stateWatcherTask;

    private async void StateWatcher() {
        var sw = Stopwatch.StartNew();
        try {
            while (!stateWatcherCancellationTokenSource.IsCancellationRequested) {
                await Task.Delay(50, stateWatcherCancellationTokenSource.Token);
                if (stateWatcherCancellationTokenSource.IsCancellationRequested) return;
                sw.Restart();
                CheckMonitoredPlugins(out var changeDetected, out var allAvailable);
                if (changeDetected || allAvailable == false) RequestReload(allAvailable);
            }
        } catch (Exception ex) {
            if (ex is TaskCanceledException) return;
            _pluginLog.Error(ex, "Error in StateWatcher");
        }
    }

    private void RequestReload(bool monitoredPluginsAvailable) {
        try {
            UnloadPlugin();
        } catch (Exception ex) {
            _pluginLog.Error(ex, "Error Unloading Plugin");
            return;
        }

        if (monitoredPluginsAvailable)
            try {
                _framework.RunOnTick(BootPlugin, TimeSpan.FromSeconds(1));
            } catch (Exception ex) {
                _pluginLog.Error(ex, "Error Loading Plugin");
            }
    }

    public void BootPlugin() {
        _pluginLog.Debug("Booting SimpleOutfits");
        if (_pluginInterface.AssemblyLocation.Directory == null) throw new Exception("Assembly Location is Invalid");
        _loadContext = new BootstrapLoadContext(_pluginLog, "SimpleOutfits", _pluginInterface.AssemblyLocation.Directory);

        _loadContext.AddHandle("SimpleOutfits", typeof(Plugin).Assembly);
        _loadContext.AddHandle("Dalamud", typeof(IDalamudPluginInterface).Assembly);

        if (!TryGetLoadedPlugin("Penumbra", out _penumbraPlugin, out _penumbraLocalPlugin)) {
            _pluginLog.Error("Could Not find Penumbra");
            return;
        }

        if (!TryGetMemberValue(out var penumbraServiceManager, _penumbraPlugin, "_services")) {
            _pluginLog.Error("Could Not find Penumbra._services");
            return;
        }

        if (!TryGetMemberValue<IEnumerable>(out var serviceCollection, penumbraServiceManager, "_collection")) {
            _pluginLog.Error("Could not find Penumbra._services._collection");
            return;
        }

        var abstractions = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => {
            if (a.GetName().Name != "Microsoft.Extensions.DependencyInjection.Abstractions") return false;
            var extensionsType = a.GetType("Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions");
            if (extensionsType == null) return false;
            if (!extensionsType.GetMethods().Any((m) => m.Name == "Add" && serviceCollection.GetType().IsAssignableTo(m.ReturnType))) return false;
            return true;
        });

        var collection = serviceCollection.Cast<object>().ToList();

        if (!TryGetServiceType(collection, "Penumbra.GameData.Interop.ObjectManager", out var objectManagerType)) {
            _pluginLog.Error("Could not find Penumbra.GameData.Interop.ObjectManager");
            return;
        }

        if (!TryGetServiceType(collection, "Penumbra.Api.Api.IPenumbraApi", out var penumbraApiType)) {
            _pluginLog.Error("Could not find Penumbra.Api.Api.IPenumbraApi");
            return;
        }

        _pluginLog.Warning($"{penumbraApiType.Assembly.GetName().Name}");

        var byteStringType = objectManagerType.Assembly.GetType("Penumbra.GameData.Interop.Actor")?.GetProperty("Utf8Name")?.PropertyType;

        if (byteStringType == null) {
            _pluginLog.Error("Could not find Penumbra.GameData.Interop.Actor.Utf8Name");
            return;
        }

        if (abstractions == null) {
            _pluginLog.Error("Could not find Microsoft.Extensions.DependencyInjection.Abstractions");
            return;
        }

        _loadContext.AddHandle("Microsoft.Extensions.DependencyInjection", serviceCollection.GetType().Assembly);
        _loadContext.AddHandle("Microsoft.Extensions.DependencyInjection.Abstractions", abstractions);
        _loadContext.AddHandle("Penumbra", _penumbraPlugin.GetType().Assembly);
        _loadContext.AddHandle("Penumbra.GameData", objectManagerType.Assembly);
        _loadContext.AddHandle("Penumbra.Api", penumbraApiType.Assembly);
        _loadContext.AddHandle("Penumbra.String", byteStringType.Assembly);
        _loadContext.AddHandle("OtterGui", penumbraServiceManager.GetType().Assembly);

        var loadedAssembly = _loadContext.LoadFromFile(Path.Join(_pluginInterface.AssemblyLocation.Directory.FullName, "SimpleOutfitsPlugin.dll"));

        var t = loadedAssembly.GetTypes().FirstOrDefault(t => t.IsAssignableTo(typeof(IBootstrapPlugin)));
        if (t == null) {
            _pluginLog.Error("Failed to find a IBootstrapPlugin class.");
            return;
        }

        var createMethod = _pluginInterface.GetType().GetMethod("Create")?.MakeGenericMethod(t);
        if (createMethod == null) {
            _pluginLog.Error($"Failed to create DalamudPluginInterface.Create<{t.Name}> method.");
            return;
        }

        _loadedPlugin = createMethod.Invoke(_pluginInterface, [new[] { _penumbraPlugin, penumbraServiceManager }]) as IBootstrapPlugin;
    }

    public void UnloadPlugin() {
        if (_loadedPlugin != null) {
            _pluginLog.Warning("Unloading SimpleOutfits");
            _loadedPlugin?.Dispose();
        }

        _loadContext?.Unload();
        _penumbraPlugin = null;
        _penumbraLocalPlugin = null;
        _loadedPlugin = null;
        _loadContext = null;
    }

    public void Dispose() {
        stateWatcherCancellationTokenSource.Cancel();
        stateWatcherTask.Wait();
        try {
            UnloadPlugin();
        } catch (Exception ex) {
            _pluginLog.Error(ex, "Error in Dispose of Bootstrapped Plugin");
        }
    }
}
