using System.Reflection;
using System.Runtime.Loader;
using Dalamud.Plugin.Services;

namespace SimpleOutfits;

public class BootstrapLoadContext(IPluginLog pluginLog, string name, DirectoryInfo directoryInfo) : AssemblyLoadContext(true) {
    private record MonitoredPath(string Path, DateTime Modified);

    private readonly List<MonitoredPath> monitoredPaths = [];

    public Dictionary<string, Assembly> HandledAssemblies = new();

    protected override Assembly? Load(AssemblyName assemblyName) {
        pluginLog.Debug($"[{name}] Attempting to load {assemblyName.FullName}");

        if (assemblyName.Name == "FFXIVClientStructs") {
            var csFilePath = Path.Join(directoryInfo.FullName, "FFXIVClientStructs.dll");
            var csFile = new FileInfo(csFilePath);
            if (csFile.Exists) {
                pluginLog.Debug($"[{name}] Attempting to load custom FFXIVClientStructs from {csFile.FullName}");
                monitoredPaths.Add(new MonitoredPath(csFile.FullName, csFile.LastWriteTime));
                return LoadFromFile(csFile.FullName);
            }
        }

        if (assemblyName.Name != null && HandledAssemblies.TryGetValue(assemblyName.Name, out var load)) {
            pluginLog.Debug($"[{name}] Forwarded reference to {assemblyName.Name}");
            return load;
        }

        var filePath = Path.Join(directoryInfo.FullName, $"{assemblyName.Name}.dll");
        var file = new FileInfo(filePath);
        if (file.Exists) {
            try {
                pluginLog.Debug($"[{name}] Attempting to load {assemblyName.Name} from {file.FullName}");
                monitoredPaths.Add(new MonitoredPath(file.FullName, file.LastWriteTime));
                return LoadFromFile(file.FullName);
            } catch {
                //
            }
        }

        return base.Load(assemblyName);
    }

    public Assembly LoadFromFile(string filePath) {
        using var file = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var pdbPath = Path.ChangeExtension(filePath, ".pdb");
        if (!File.Exists(pdbPath)) return LoadFromStream(file);
        using var pdbFile = File.Open(pdbPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return LoadFromStream(file, pdbFile);
    }

    public bool DetectChanges() {
        foreach (var m in monitoredPaths) {
            var f = new FileInfo(m.Path);
            if (f.Exists && f.LastWriteTime != m.Modified) {
                pluginLog.Debug($"Loaded Assembly Changed: {f.FullName}");
                return true;
            }
        }

        return false;
    }

    public void AddHandle(string assemblyName, Assembly? assembly) {
        if (assembly == null) throw new Exception($"Null Assembly: {assemblyName}");
        HandledAssemblies.Add(assemblyName, assembly);
    }

    public void AddHandle(string assemblyName, Func<string, Assembly?> getFunc) => AddHandle(assemblyName, getFunc(assemblyName));
}
