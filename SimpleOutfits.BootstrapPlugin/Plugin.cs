using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace SimpleOutfits.BootstrapPlugin;

public class Plugin : IDalamudPlugin {
    private readonly IPluginLog pluginLog;
    private BootstrapLoadContext? loadContext;
    private IBootstrapPlugin? loadedPlugin;
    private readonly IDalamudPluginInterface pluginInterface;
    
    private IList? installedPluginsList;
    
    
    private IDalamudPlugin? penumbraPlugin;
    private object? penumbraLocalPlugin;
    
    private bool TryGetLoadedPlugin(string internalName, [NotNullWhen(true)] out IDalamudPlugin? plugin, [NotNullWhen(true)] out object? localPlugin) {
        plugin = null;
        localPlugin = null;
        if (installedPluginsList == null) {
            var dalamudAssembly = pluginInterface.GetType().Assembly;
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
        
            installedPluginsList = (IList?) installedPluginsListField.GetValue(pluginManager);
            if (installedPluginsList == null) throw new Exception("Failed to get installedPluginsList value");
        }

        PropertyInfo? internalNameProperty = null;
        
        foreach (var v in installedPluginsList) {
            internalNameProperty ??= v?.GetType().GetProperty("InternalName");
            if (internalNameProperty == null) continue;
            var installedInternalName = internalNameProperty.GetValue(v) as string;
            if (installedInternalName == internalName) {
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
                var fi = (FieldInfo) next;
                var fv = fi.GetValue(origin);
                if (fv == null) throw new Exception("Null Value");
                return GetMember(members[stepIndex], fv, path[1..]);
            case MemberTypes.Property:
                var pi = (PropertyInfo)next;
                if (!pi.CanRead) throw new Exception("Cannot Read Property");
                var pv = pi.GetValue(origin);
                if (pv == null) throw new Exception("Null Value");
                return GetMember(members[stepIndex], pv, path[1..]);
                break;
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

    private static bool TryGetMemberValue([NotNullWhen(true)] out object? value, object origin, params string[] path) => TryGetMember(out _, out value, origin, path) && value != null;

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
        this.pluginInterface = pluginInterface;
        this.pluginLog = pluginLog;
        framework.Update += FrameworkOnUpdate;
    }
    

    private void FrameworkOnUpdate(IFramework framework) {
        if (timeSinceUpdate.ElapsedMilliseconds < 1000) return;
        timeSinceUpdate.Restart();

        try {
            if (loadContext == null) {
                BootPlugin();
                return;
            }


            if (loadContext.DetectChanges()) {
                UnloadPlugin();
                return;
            }
            
        } catch (Exception ex) {
            pluginLog.Error(ex, "Error in Boostrap Update");
        }
        
    }

    public void BootPlugin() {
        pluginLog.Debug("Booting SimpleOutfits");
        if (pluginInterface.AssemblyLocation.Directory == null) throw new Exception("Assembly Location is Invalid");
        loadContext = new BootstrapLoadContext(pluginLog, "SimpleOutfits", pluginInterface.AssemblyLocation.Directory);

        loadContext.AddHandle("SimpleOutfits.BootstrapPlugin", typeof(Plugin).Assembly);
        loadContext.AddHandle("Dalamud", typeof(IDalamudPluginInterface).Assembly);

        if (!TryGetLoadedPlugin("Penumbra", out penumbraPlugin, out penumbraLocalPlugin)) {
            pluginLog.Error("Could Not find Penumbra");
            return;
        }

        if (!TryGetMemberValue(out var penumbraServiceManager,  penumbraPlugin, "_services")) {
            pluginLog.Error("Could Not find Penumbra._services");
            return;
        }

        if (!TryGetMemberValue<IEnumerable>(out var serviceCollection, penumbraServiceManager, "_collection")) {
            pluginLog.Error("Could not find Penumbra._services._collection");
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
            pluginLog.Error("Could not find Penumbra.GameData.Interop.ObjectManager");
            return;
        }
        
        if (!TryGetServiceType(collection, "Penumbra.Api.Api.IPenumbraApi", out var penumbraApiType)) {
            pluginLog.Error("Could not find Penumbra.Api.Api.IPenumbraApi");
            return;
        }
        
        pluginLog.Warning($"{penumbraApiType.Assembly.GetName().Name}");
        
        var byteStringType = objectManagerType.Assembly.GetType("Penumbra.GameData.Interop.Actor")?.GetProperty("Utf8Name")?.PropertyType;

        if (byteStringType == null) {
            pluginLog.Error("Could not find Penumbra.GameData.Interop.Actor.Utf8Name");
            return;
        }



        if (abstractions == null) {
            pluginLog.Error("Could not find Microsoft.Extensions.DependencyInjection.Abstractions");
            return;
        }
        
        
        loadContext.AddHandle("Microsoft.Extensions.DependencyInjection", serviceCollection.GetType().Assembly);
        loadContext.AddHandle("Microsoft.Extensions.DependencyInjection.Abstractions", abstractions);
        loadContext.AddHandle("Penumbra", penumbraPlugin.GetType().Assembly);
        loadContext.AddHandle("Penumbra.GameData", objectManagerType.Assembly);
        loadContext.AddHandle("Penumbra.Api", penumbraApiType.Assembly);
        loadContext.AddHandle("Penumbra.String", byteStringType.Assembly);
        loadContext.AddHandle("OtterGui", penumbraServiceManager.GetType().Assembly);
        
        var loadedAssembly = loadContext.LoadFromFile(Path.Join(pluginInterface.AssemblyLocation.Directory.FullName, "SimpleOutfits.dll"));

        var t = loadedAssembly.GetTypes().FirstOrDefault(t => t.IsAssignableTo(typeof(IBootstrapPlugin)));
        if (t == null) {
            pluginLog.Error("Failed to find a IBootstrapPlugin class.");
            return;
        }

        var createMethod = pluginInterface.GetType().GetMethod("Create")?.MakeGenericMethod(t);
        if (createMethod == null) {
            pluginLog.Error($"Failed to create DalamudPluginInterface.Create<{t.Name}> method.");
            return;
        }
        
        

        loadedPlugin = createMethod.Invoke(pluginInterface, [ new[] { penumbraPlugin, penumbraServiceManager } ]) as IBootstrapPlugin;
    }

    public void UnloadPlugin() {
        pluginLog.Warning("Unloading SimpleOutfits");
        loadedPlugin?.Dispose();
        loadContext?.Unload();
        penumbraPlugin = null;
        penumbraLocalPlugin = null;
        loadedPlugin = null;
        loadContext = null;
    }
    
    public void Dispose() {
        try {
           UnloadPlugin();
        } catch (Exception ex) {
            pluginLog.Error(ex, "Error in Dispose of Bootstrapped Plugin");
        }
    }
}
