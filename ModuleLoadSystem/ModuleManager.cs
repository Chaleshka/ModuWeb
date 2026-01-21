using ModuWeb.Events;
using System.Collections.Concurrent;
using System.Runtime.Loader;
using ModuWeb.ModuleLoadSystem;

namespace ModuWeb;

/// <summary>
/// Manages dynamic loading, unloading, and reloading of modules from .dll files.
/// </summary>
internal class ModuleManager
{
    private static ModuleManager _instance;
    public static ModuleManager Instance
    {
        get => _instance ?? throw new InvalidOperationException("ModuleManager is not initialized.");
        set
        {
            if (_instance == null)
            {
                _instance = value;
                _instance.LoadAllModules();
            }
        }
    }

    internal readonly ConcurrentDictionary<string, (ModuleBase Module, AssemblyLoadContext Context)> modules = new();
    private readonly ConcurrentDictionary<string, string> _modulesNameToPath = new();

    private readonly string _modulesDirectory;
    private readonly string _dependenciesDirectory;
    private readonly string _workingDirectory;
    private readonly string _workingDependenciesDirectory;
    private readonly ModuleWatcher _watcher;
    private readonly string[] _moduleOrder;

    /// <summary>
    /// Initializes the module manager and loads modules from the specified directory.
    /// </summary>
    /// <param name="modulesDirectory">Directory containing the module .dll files.</param>
    internal ModuleManager(string modulesDirectory, string[] order)
    {
        _modulesDirectory = modulesDirectory;
        _dependenciesDirectory = Path.Combine(modulesDirectory, "dependencies");
        _workingDirectory = Path.Combine(modulesDirectory, "temp");
        _workingDependenciesDirectory = Path.Combine(_workingDirectory, "dependencies");

        PrepareDirectories();
        //LoadAllModules();
        _moduleOrder = order;

        _watcher = new(_modulesDirectory);
        return;
    }
        
    private void PrepareDirectories()
    {
        if (!Directory.Exists(_modulesDirectory))
            Directory.CreateDirectory(_modulesDirectory);

        if (!Directory.Exists(_dependenciesDirectory))
            Directory.CreateDirectory(_dependenciesDirectory);


        CreateOrCleanDirectory(_workingDirectory, true);
        CreateOrCleanDirectory(_workingDependenciesDirectory);
    }

    private static void CreateOrCleanDirectory(string path, bool hidden = false)
    {
        if (!Directory.Exists(path))
        {
            var dir = Directory.CreateDirectory(path);
            if (hidden)
                dir.Attributes |= FileAttributes.Hidden;
        }
        else
        {
            foreach (var file in Directory.GetFiles(path))
                File.Delete(file);
        }
    }


    public ModuleBase? GetModule(string name)
        => modules.TryGetValue(name.ToLower(), out var module) ? module.Module : null;
    public List<ModuleBase> GetModules()
        => modules.Values.Select(p => p.Module).ToList();

    /// <summary>
    /// Loads all available modules from the modules directory.
    /// </summary>
    internal void LoadAllModules()
    {
        var allFiles = Directory.GetFiles(_modulesDirectory, "*.dll", SearchOption.TopDirectoryOnly);
        var orderedFiles = new List<string>();
        var otherFiles = new List<string>();

        foreach (var moduleName in _moduleOrder)
        {
            var file = allFiles.FirstOrDefault(f =>
                Path.GetFileNameWithoutExtension(f).Equals(moduleName, StringComparison.OrdinalIgnoreCase));
            if (file != null)
            {
                orderedFiles.Add(file);
            }
        }

        otherFiles.AddRange(allFiles.Where(f =>
            !orderedFiles.Contains(f) &&
            !_moduleOrder.Contains(Path.GetFileNameWithoutExtension(f), StringComparer.OrdinalIgnoreCase)));

        foreach (var file in orderedFiles.Concat(otherFiles))
        {
            TryLoadModule(file);
        }
    }

    /// <summary>
    /// Attempts to load a module from the specified path.
    /// </summary>
    /// <param name="originalPath">Original path to the .dll file.</param>
    /// <param name="twiceAccessError">Indicates retry after access conflict.</param>
    internal async Task TryLoadModule(string originalPath, bool twiceAccessError = false)
    {
        string moduleName = Path.GetFileNameWithoutExtension(originalPath).ToLower();

        if (modules.ContainsKey(moduleName))
            return;

        try
        {
            CopyDependenciesToWorkingDirectory();

            var loadContext = new ModuleLoadContext(_workingDependenciesDirectory);

            byte[] moduleBytes = File.ReadAllBytes(originalPath);
            using var stream = new MemoryStream(moduleBytes);

            var assembly = loadContext.LoadFromStream(stream);

            var moduleType = assembly.GetTypes()
                .FirstOrDefault(t => typeof(ModuleBase).IsAssignableFrom(t) && !t.IsAbstract);

            if (moduleType != null && Activator.CreateInstance(moduleType) is ModuleBase module)
            {
                modules[moduleName] = (module, loadContext);
                _modulesNameToPath[moduleName] = originalPath;
                await module.OnModuleLoad();
                Events.Events.ModuleLoadedSafeEvent.Invoke(new ModuleLoadedEventArgs(moduleName, module, assembly, loadContext, originalPath));
                Logger.Info($"Module loaded: {moduleName}");
            }
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("The process cannot access the file") && 
                ex.Message.Contains("because it is being used by another process.") && !twiceAccessError)
            {
                await Task.Delay(1000);
                await TryLoadModule(originalPath, true);
            }
            else
                Logger.Error($"Failed to load module from {originalPath}: {ex.Message}");
        }
    }

    /// <summary>
    /// Unloads the specified module and its context.
    /// </summary>
    /// <param name="name">Name of the module.</param>
    internal async Task UnloadModule(string name)
    {
        if (!modules.TryRemove(name, out var entry))
            return;

        WeakReference contextRef = new(entry.Context);
        await entry.Module.OnModuleUnload();
        entry.Context.Unload();
        Logger.Info($"Module unloaded: {name}");
        Events.Events.ModuleUnloadedSafeEvent.Invoke(new ModuleUnloadedEventArgs(name, entry.Module, entry.Context ));
        _modulesNameToPath.TryRemove(name, out _);

        _ = Task.Run(() =>
        {
            try
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();


                if (contextRef.IsAlive)
                    Logger.Warn($"Module '{name}' AssemblyLoadContext still alive! Possible reference leak.");
                
                string path = Path.Combine(_workingDirectory, name + ".dll");
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception ex)
            {
                Logger.Error($"Background cleanup failed for '{name}': {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Reloads a module by path.
    /// </summary>
    /// <param name="path">Path to the module's .dll file.</param>
    internal async Task ReloadModule(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path).ToLower();
        await UnloadModule(name);
        await Task.Delay(200);
        await TryLoadModule(path);
    }

    private string CopyToWorkingDirectory(string sourcePath, string targetDirectory)
    {
        var name = Path.GetFileName(sourcePath);
        var dest = Path.Combine(targetDirectory, name);

        File.Copy(sourcePath, dest, overwrite: true);
        return dest;
    }

    private void CopyDependenciesToWorkingDirectory()
    {
        foreach (var file in Directory.GetFiles(_dependenciesDirectory, "*.dll"))
        {
            var fileName = Path.GetFileName(file);
            var destination = Path.Combine(_workingDependenciesDirectory, fileName);

            var sourceTime = File.GetLastWriteTimeUtc(file);
            var destTime = File.Exists(destination) ? File.GetLastWriteTimeUtc(destination) : DateTime.MinValue;

            if (!File.Exists(destination) || sourceTime > destTime)
                File.Copy(file, destination, true);
        }
    }
}
