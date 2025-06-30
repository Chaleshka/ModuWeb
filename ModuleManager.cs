using System.Collections.Concurrent;
using System.Runtime.Loader;

namespace ModuWeb;

/// <summary>
/// Manages dynamic loading, unloading, and reloading of modules from .dll files.
/// </summary>
public class ModuleManager
{
    private static ModuleManager _instance;
    public static ModuleManager Instance
    {
        get => _instance ?? throw new InvalidOperationException("ModuleManager is not initialized.");
        set
        {
            if (_instance == null)
                _instance = value;
        }
    }

    private readonly ConcurrentDictionary<string, (ModuleBase Module, AssemblyLoadContext Context)> _modules = new();
    private readonly ConcurrentDictionary<string, string> _modulesNameToPath = new();

    private readonly string _modulesDirectory;
    private readonly string _dependenciesDirectory;
    private readonly string _workingDirectory;
    private readonly string _workingDependenciesDirectory;
    private readonly ModuleWatcher _watcher;

    /// <summary>
    /// Initializes the module manager and loads modules from the specified directory.
    /// </summary>
    /// <param name="modulesDirectory">Directory containing the module .dll files.</param>
    internal ModuleManager(string modulesDirectory)
    {
        _modulesDirectory = modulesDirectory;
        _dependenciesDirectory = Path.Combine(modulesDirectory, "dependencies");
        _workingDirectory = Path.Combine(modulesDirectory, "temp");
        _workingDependenciesDirectory = Path.Combine(_workingDirectory, "dependencies");

        PrepareDirectories();
        LoadAllModules();

        _watcher = new (_modulesDirectory);
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
        => _modules.TryGetValue(name.ToLower(), out var module) ? module.Module : null;
    public List<ModuleBase> GetModules()
        => _modules.Values.Select(p => p.Module).ToList();

    /// <summary>
    /// Loads all available modules from the modules directory.
    /// </summary>
    internal void LoadAllModules()
    {
        foreach (var file in Directory.GetFiles(_modulesDirectory, "*.dll", SearchOption.TopDirectoryOnly).OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase))
        {
            TryLoadModule(file);
        }
    }

    /// <summary>
    /// Attempts to load a module from the specified path.
    /// </summary>
    /// <param name="originalPath">Original path to the .dll file.</param>
    /// <param name="twiceAccesError">Indicates retry after access conflict.</param>
    internal async void TryLoadModule(string originalPath, bool twiceAccesError = false)
    {
        string moduleName = Path.GetFileNameWithoutExtension(originalPath).ToLower();

        if (_modules.ContainsKey(moduleName))
            return;

        try
        {
            string tempModulePath = CopyToWorkingDirectory(originalPath, _workingDirectory);
            CopyDependenciesToWorkingDirectory();

            var loadContext = new ModuleLoadContext(tempModulePath, _workingDependenciesDirectory);
            var assembly = loadContext.LoadFromAssemblyPath(tempModulePath);

            var moduleType = assembly.GetTypes()
                .FirstOrDefault(t => typeof(ModuleBase).IsAssignableFrom(t) && !t.IsAbstract);

            if (moduleType != null && Activator.CreateInstance(moduleType) is ModuleBase module)
            {
                _modules[moduleName] = (module, loadContext);
                _modulesNameToPath[moduleName] = originalPath;
                await module.OnModuleLoad();
                Logger.Info($"Module loaded: {moduleName}");
            }
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("The process cannot access the file") && 
                ex.Message.Contains("because it is being used by another process.") && !twiceAccesError)
            {
                await Task.Delay(1000);
                TryLoadModule(originalPath, true);
            }
            else
                Logger.Error($"Failed to load module from {originalPath}: {ex.Message}");
        }
    }

    /// <summary>
    /// Unloads the specified module and its context.
    /// </summary>
    /// <param name="name">Name of the module.</param>
    internal async void UnloadModule(string name)
    {
        if (!_modules.TryRemove(name, out var entry))
            return;

        WeakReference contextRef = new(entry.Context);
        await entry.Module.OnModuleLoad();
        entry.Context.Unload();
        Logger.Info($"Module unloaded: {name}");

        await Task.Run(() =>
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();


            if (contextRef.IsAlive)
                Logger.Warn($"Module '{name}' AssemblyLoadContext still alive! Possible reference leak.");
                
            try
            {
                string path = Path.Combine(_workingDirectory, name + ".dll");
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to delete module file '{name}': {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Reloads a module by path.
    /// </summary>
    /// <param name="path">Path to the module's .dll file.</param>
    internal async void ReloadModule(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path).ToLower();
        UnloadModule(name);
        await Task.Delay(200);
        TryLoadModule(path);
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
            File.Copy(file, destination, true);
        }
    }
}
