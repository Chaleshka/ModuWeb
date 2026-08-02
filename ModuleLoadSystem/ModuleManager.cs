using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;
using ModuWeb.Events;
using ModuWeb.ModuleLoadSystem;
using ModuWeb.ModuleMessenger;
using ModuWeb.ViewEngine;

namespace ModuWeb;

/// <summary>
/// Manages the transactional loading, replacement, and unloading of modules.
/// </summary>
internal sealed class ModuleManager : IDisposable
{
    private sealed class ModuleEntry
    {
        internal ModuleEntry(
            string moduleName,
            ModuleBase module,
            Assembly assembly,
            ModuleLoadContext context,
            string sourcePath,
            string packagePath,
            IReadOnlySet<string> dependencyNames)
        {
            ModuleName = moduleName;
            Module = module;
            Assembly = assembly;
            Context = context;
            SourcePath = sourcePath;
            PackagePath = packagePath;
            DependencyNames = dependencyNames;
        }

        internal string ModuleName { get; }
        internal ModuleBase Module { get; }
        internal Assembly Assembly { get; }
        internal ModuleLoadContext Context { get; }
        internal string SourcePath { get; }
        internal string PackagePath { get; }
        internal IReadOnlySet<string> DependencyNames { get; }
    }

    private sealed record DependencyFile(string Name, string Path, IReadOnlySet<string> References);

    private sealed class SharedDependencyEntry
    {
        internal SharedDependencyEntry(string name, string path, SharedDependencyLoadContext context, Assembly assembly)
        {
            Name = name;
            Path = path;
            Context = context;
            Assembly = assembly;
        }

        internal string Name { get; }
        internal string Path { get; }
        internal SharedDependencyLoadContext Context { get; }
        internal Assembly Assembly { get; }
    }

    internal sealed class ModuleRequestLease : IDisposable
    {
        private ModuleManager? _manager;
        private readonly ModuleBase _module;

        internal ModuleRequestLease(ModuleManager manager, ModuleBase module)
        {
            _manager = manager;
            _module = module;
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref _manager, null)?.ReleaseRequestLease(_module);
        }
    }

    private static ModuleManager? _instance;
    internal static ModuleManager Instance
    {
        get => _instance ?? throw new InvalidOperationException("ModuleManager is not initialized.");
        set
        {
            if (_instance is not null)
                throw new InvalidOperationException("ModuleManager is already initialized.");

            _instance = value;
        }
    }

    private readonly ConcurrentDictionary<string, ModuleEntry> _modules = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _moduleLocks = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<ModuleBase, int> _activeRequests = new();
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly Dictionary<string, SharedDependencyEntry> _sharedDependencies = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _modulesDirectory;
    private readonly string _dependenciesDirectory;
    private readonly string _workingDirectory;
    private readonly ModuleWatcher _watcher;
    private readonly string[] _moduleOrder;
    private readonly IServiceProvider? _serviceProvider;
    private int _started;
    private int _disposed;

    internal ModuleManager(string modulesDirectory, string[] order, IServiceProvider? serviceProvider)
    {
        _modulesDirectory = modulesDirectory;
        _dependenciesDirectory = Path.Combine(modulesDirectory, "dependencies");
        _workingDirectory = Path.Combine(modulesDirectory, "temp");
        _moduleOrder = order;
        _serviceProvider = serviceProvider;

        PrepareDirectories();
        _watcher = new ModuleWatcher(_modulesDirectory);
    }

    internal async Task StartAsync()
    {
        ThrowIfDisposed();
        if (Interlocked.Exchange(ref _started, 1) != 0)
            return;

        await LoadAllModulesAsync();
        _watcher.Start();
    }

    private void PrepareDirectories()
    {
        Directory.CreateDirectory(_modulesDirectory);
        Directory.CreateDirectory(_dependenciesDirectory);

        if (Directory.Exists(_workingDirectory))
            Directory.Delete(_workingDirectory, recursive: true);

        Directory.CreateDirectory(_workingDirectory);
    }

    internal ModuleBase? GetModule(string name)
        => _modules.TryGetValue(name, out var entry) ? entry.Module : null;

    internal string? GetModuleName(ModuleBase module)
        => _modules.FirstOrDefault(pair => ReferenceEquals(pair.Value.Module, module)).Key;

    internal List<ModuleBase> GetModules()
        => _modules.Values.Select(entry => entry.Module).ToList();

    internal bool IsModuleContextActive(AssemblyLoadContext? context)
        => context is not null && _modules.Values.Any(entry => ReferenceEquals(entry.Context, context));

    internal ModuleRequestLease? TryAcquireRequestLease(ModuleBase module)
    {
        _activeRequests.AddOrUpdate(module, 1, static (_, count) => count + 1);

        if (_modules.Values.Any(entry => ReferenceEquals(entry.Module, module)))
            return new ModuleRequestLease(this, module);

        ReleaseRequestLease(module);
        return null;
    }

    private void ReleaseRequestLease(ModuleBase module)
    {
        while (_activeRequests.TryGetValue(module, out var count))
        {
            if (count <= 1)
            {
                if (_activeRequests.TryRemove(new KeyValuePair<ModuleBase, int>(module, count)))
                    return;
            }
            else if (_activeRequests.TryUpdate(module, count - 1, count))
            {
                return;
            }
        }
    }

    private async Task LoadAllModulesAsync()
    {
        var allFiles = Directory.GetFiles(_modulesDirectory, "*.dll", SearchOption.TopDirectoryOnly);
        var orderedFiles = _moduleOrder
            .Select(moduleName => allFiles.FirstOrDefault(file =>
                Path.GetFileNameWithoutExtension(file).Equals(moduleName, StringComparison.OrdinalIgnoreCase)))
            .Where(file => file is not null)
            .Cast<string>();
        var orderedSet = new HashSet<string>(orderedFiles, StringComparer.OrdinalIgnoreCase);
        var otherFiles = allFiles.Where(file => !orderedSet.Contains(file));

        foreach (var file in orderedFiles.Concat(otherFiles))
            await ReloadModule(file);
    }

    internal async Task RescanModulesAsync()
    {
        if (Volatile.Read(ref _disposed) != 0)
            return;

        await _lifecycleGate.WaitAsync();
        try
        {
            var files = Directory.GetFiles(_modulesDirectory, "*.dll", SearchOption.TopDirectoryOnly);
            var existingPaths = files
                .Select(Path.GetFullPath)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in _modules.ToArray().Where(entry => !existingPaths.Contains(entry.Value.SourcePath)))
                await UnloadModuleCoreAsync(entry.Key);

            foreach (var file in files)
                await ReloadModuleCoreAsync(file);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    internal async Task ReloadModule(string path)
    {
        await _lifecycleGate.WaitAsync();
        try
        {
            await ReloadModuleCoreAsync(path);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    /// <summary>
    /// Reloads only active modules whose direct or transitive dependency graph contains a changed DLL.
    /// </summary>
    internal async Task ReloadModulesUsingDependenciesAsync(IReadOnlyCollection<string> dependencyNames)
    {
        if (Volatile.Read(ref _disposed) != 0)
            return;

        var changedDependencies = dependencyNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (changedDependencies.Count == 0)
            return;

        await _lifecycleGate.WaitAsync();
        try
        {
            var availableDependencies = GetAvailableDependencies();
            var normalizedDependencies = NormalizeDependencyNames(changedDependencies, availableDependencies);
            var dependenciesToReload = GetDependentDependencies(normalizedDependencies, availableDependencies);
            var entries = _modules.ToArray()
                .Where(entry => entry.Value.DependencyNames.Overlaps(dependenciesToReload))
                .OrderBy(entry => GetModuleOrder(entry.Value))
                .ThenBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (entries.Length == 0)
            {
                RemoveSharedDependencies(dependenciesToReload);
                Logger.Info($"No active modules use changed shared dependencies: {string.Join(", ", dependenciesToReload)}.");
                return;
            }

            var missingDependencies = dependenciesToReload
                .Where(dependencyName => _sharedDependencies.ContainsKey(dependencyName) && !availableDependencies.ContainsKey(dependencyName))
                .ToArray();
            if (missingDependencies.Length > 0)
            {
                throw new FileNotFoundException(
                    $"Shared dependencies required by active modules were removed: {string.Join(", ", missingDependencies)}.",
                    _dependenciesDirectory);
            }

            var requiredDependencies = GetRequiredDependencyNames(entries.Select(entry => entry.Value), availableDependencies);
            var dependencySnapshot = CreateSharedDependencySnapshot(
                availableDependencies,
                requiredDependencies,
                dependenciesToReload);
            var candidates = new List<ModuleEntry>();
            try
            {
                foreach (var entry in entries)
                {
                    if (!File.Exists(entry.Value.SourcePath))
                        continue;

                    candidates.Add(await CreateAndInitializeCandidateAsync(entry.Value.SourcePath, dependencySnapshot));
                }

                var previousDependencies = _sharedDependencies.Values.ToArray();
                await ReplaceModulesTransactionallyAsync(candidates);

                foreach (var entry in entries.Where(entry => !File.Exists(entry.Value.SourcePath)))
                    await UnloadModuleCoreAsync(entry.Key);

                ReplaceSharedDependencies(dependencySnapshot);
                UnloadUnusedSharedDependencies(previousDependencies, _sharedDependencies.Values);

                Logger.Info($"Reloaded {candidates.Count} module(s) after shared dependency change: {string.Join(", ", dependenciesToReload)}.");
            }
            catch
            {
                foreach (var candidate in candidates)
                {
                    if (_modules.TryGetValue(candidate.ModuleName, out var active) && ReferenceEquals(active, candidate))
                        continue;

                    await CleanupEntryAsync(candidate.ModuleName, candidate, unregisterViews: false, notify: false);
                }

                UnloadUnusedSharedDependencies(dependencySnapshot.Values, _sharedDependencies.Values);
                throw;
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    private int GetModuleOrder(ModuleEntry entry)
    {
        var index = Array.FindIndex(_moduleOrder, configuredName =>
            configuredName.Equals(entry.ModuleName, StringComparison.OrdinalIgnoreCase) ||
            configuredName.Equals(Path.GetFileNameWithoutExtension(entry.SourcePath), StringComparison.OrdinalIgnoreCase));
        return index < 0 ? int.MaxValue : index;
    }

    private static HashSet<string> GetRequiredDependencyNames(
        IEnumerable<ModuleEntry> entries,
        IReadOnlyDictionary<string, DependencyFile> availableDependencies)
    {
        var requiredDependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dependencyName in entries.SelectMany(entry => entry.DependencyNames))
        {
            if (availableDependencies.TryGetValue(dependencyName, out var dependency))
                requiredDependencies.Add(dependency.Name);
        }

        return requiredDependencies;
    }

    private async Task ReloadModuleCoreAsync(string path)
    {
        if (Volatile.Read(ref _disposed) != 0 || !File.Exists(path))
            return;

        var sourcePath = Path.GetFullPath(path);
        ModuleEntry? candidate = null;
        IReadOnlyDictionary<string, SharedDependencyEntry>? dependencySnapshot = null;
        try
        {
            var availableDependencies = GetAvailableDependencies();
            var requiredDependencies = GetRequiredDependencies(sourcePath, availableDependencies)
                .Select(dependency => dependency.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            dependencySnapshot = CreateSharedDependencySnapshot(
                availableDependencies,
                requiredDependencies,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            candidate = await CreateAndInitializeCandidateAsync(sourcePath, dependencySnapshot);
            var moduleLock = _moduleLocks.GetOrAdd(candidate.ModuleName, static _ => new SemaphoreSlim(1, 1));
            await moduleLock.WaitAsync();
            try
            {
                await ReplaceModuleAsync(candidate);
                ReplaceSharedDependencies(dependencySnapshot);
                candidate = null;
            }
            finally
            {
                moduleLock.Release();
            }
        }
        catch (Exception ex)
        {
            if (candidate is not null)
                await CleanupEntryAsync(candidate.ModuleName, candidate, unregisterViews: false, notify: false);

            if (dependencySnapshot is not null)
                UnloadUnusedSharedDependencies(dependencySnapshot.Values, _sharedDependencies.Values);

            Logger.Error($"Failed to load module from '{path}': {ex}");
        }
    }

    private async Task ReplaceModuleAsync(ModuleEntry candidate)
    {
        var moduleName = candidate.ModuleName;
        var sourcePath = candidate.SourcePath;
        var hadPrevious = _modules.TryGetValue(moduleName, out var previous);

        if (hadPrevious && !PathsEqual(previous!.SourcePath, sourcePath))
            throw new InvalidOperationException($"Module name '{moduleName}' is already used by '{previous.SourcePath}'.");

        var previousForSource = _modules
            .FirstOrDefault(entry => PathsEqual(entry.Value.SourcePath, sourcePath));
        var sourceNameChanged = previousForSource.Value is not null &&
            !previousForSource.Key.Equals(moduleName, StringComparison.OrdinalIgnoreCase);

        if (hadPrevious)
        {
            if (!_modules.TryUpdate(moduleName, candidate, previous!))
            {
                throw new InvalidOperationException($"Module '{moduleName}' changed while it was being reloaded.");
            }
        }
        else if (!_modules.TryAdd(moduleName, candidate))
        {
            throw new InvalidOperationException($"Module '{moduleName}' was loaded concurrently.");
        }

        try
        {
            RegisterViews(moduleName, candidate);
        }
        catch
        {
            if (hadPrevious)
            {
                _modules.TryUpdate(moduleName, previous!, candidate);
                RestoreViews(moduleName, previous!);
            }
            else
            {
                _modules.TryRemove(new KeyValuePair<string, ModuleEntry>(moduleName, candidate));
            }

            throw;
        }

        Events.Events.ModuleLoadedSafeEvent.Invoke(new ModuleLoadedEventArgs(moduleName, sourcePath));
        Logger.Info(hadPrevious ? $"Module reloaded: {moduleName}" : $"Module loaded: {moduleName}");

        if (hadPrevious)
            await CleanupEntryAsync(moduleName, previous!, unregisterViews: false, notify: true);

        if (sourceNameChanged && previousForSource.Value is { } previousSourceEntry && _modules.TryRemove(previousForSource))
            await CleanupEntryAsync(previousForSource.Key, previousSourceEntry, unregisterViews: true, notify: true);
    }

    private async Task ReplaceModulesTransactionallyAsync(IReadOnlyList<ModuleEntry> candidates)
    {
        if (candidates.Count == 0)
            return;

        var previousEntries = new Dictionary<string, ModuleEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidates)
        {
            if (!previousEntries.TryAdd(candidate.ModuleName, null!))
                throw new InvalidOperationException($"Multiple modules resolved to the name '{candidate.ModuleName}' during a shared dependency reload.");

            if (!_modules.TryGetValue(candidate.ModuleName, out var previous) ||
                !PathsEqual(previous.SourcePath, candidate.SourcePath))
            {
                throw new InvalidOperationException(
                    $"Module identity changed from '{Path.GetFileNameWithoutExtension(candidate.SourcePath)}' to '{candidate.ModuleName}' during a shared dependency reload.");
            }

            previousEntries[candidate.ModuleName] = previous;
        }

        try
        {
            foreach (var candidate in candidates)
            {
                var previous = previousEntries[candidate.ModuleName];
                if (!_modules.TryUpdate(candidate.ModuleName, candidate, previous))
                    throw new InvalidOperationException($"Module '{candidate.ModuleName}' changed while its shared dependency consumers were being reloaded.");
            }

            foreach (var candidate in candidates)
                RegisterViews(candidate.ModuleName, candidate);
        }
        catch
        {
            foreach (var candidate in candidates)
            {
                if (previousEntries.TryGetValue(candidate.ModuleName, out var previous))
                    _modules.TryUpdate(candidate.ModuleName, previous, candidate);
            }

            foreach (var previous in previousEntries)
            {
                try
                {
                    RestoreViews(previous.Key, previous.Value);
                }
                catch (Exception restoreException)
                {
                    Logger.Error($"Failed to restore views for module '{previous.Key}': {restoreException}");
                }
            }

            throw;
        }

        foreach (var candidate in candidates)
        {
            var previous = previousEntries[candidate.ModuleName];
            Events.Events.ModuleLoadedSafeEvent.Invoke(new ModuleLoadedEventArgs(candidate.ModuleName, candidate.SourcePath));
            Logger.Info($"Module reloaded: {candidate.ModuleName}");
            await CleanupEntryAsync(candidate.ModuleName, previous, unregisterViews: false, notify: true);
        }
    }

    private async Task<ModuleEntry> CreateAndInitializeCandidateAsync(
        string sourcePath,
        IReadOnlyDictionary<string, SharedDependencyEntry> sharedDependencies)
    {
        var fallbackModuleName = Path.GetFileNameWithoutExtension(sourcePath);
        var dependencies = GetRequiredDependencies(sourcePath, GetAvailableDependencies());
        var packagePath = CopyModulePackage(fallbackModuleName, sourcePath);
        var moduleAssemblyPath = Path.Combine(packagePath, Path.GetFileName(sourcePath));
        var dependencyAssemblies = sharedDependencies.ToDictionary(dependency => dependency.Key, dependency => dependency.Value.Assembly,
            StringComparer.OrdinalIgnoreCase);
        var context = new ModuleLoadContext(moduleAssemblyPath, dependencyAssemblies);
        ModuleBase? module = null;
        var moduleName = fallbackModuleName;

        try
        {
            var assembly = context.LoadMainAssembly(moduleAssemblyPath);
            var moduleType = assembly.GetTypes()
                .SingleOrDefault(type => typeof(ModuleBase).IsAssignableFrom(type) && !type.IsAbstract);

            if (moduleType is null)
                throw new InvalidOperationException($"No concrete {nameof(ModuleBase)} implementation was found.");

            module = Activator.CreateInstance(moduleType) as ModuleBase
                ?? throw new InvalidOperationException($"Unable to create module type '{moduleType.FullName}'.");

            module.SetFallbackModuleName(fallbackModuleName);
            moduleName = ValidateModuleName(module.ModuleName, sourcePath);
            await module.OnModuleLoad();
            return new ModuleEntry(moduleName, module, assembly, context, sourcePath, packagePath,
                dependencies
                    .SelectMany(dependency => new[] { dependency.Name, Path.GetFileNameWithoutExtension(dependency.Path) })
                    .ToHashSet(StringComparer.OrdinalIgnoreCase));
        }
        catch
        {
            if (module is not null)
            {
                try
                {
                    await module.OnModuleUnload();
                }
                catch (Exception cleanupException)
                {
                    Logger.Error($"Cleanup of failed module '{moduleName}' also failed: {cleanupException}");
                }
            }

            RemoveContextRegistrations(context);
            UnloadContext(context, packagePath, moduleName);
            throw;
        }
    }

    private Dictionary<string, DependencyFile> GetAvailableDependencies()
    {
        var availableDependencies = new Dictionary<string, DependencyFile>(StringComparer.OrdinalIgnoreCase);
        foreach (var dependencyPath in Directory.GetFiles(_dependenciesDirectory, "*.dll", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var dependencyName = AssemblyName.GetAssemblyName(dependencyPath).Name;
                if (!string.IsNullOrWhiteSpace(dependencyName))
                    availableDependencies.TryAdd(dependencyName, new DependencyFile(
                        dependencyName,
                        dependencyPath,
                        GetReferencedAssemblyNames(dependencyPath).ToHashSet(StringComparer.OrdinalIgnoreCase)));
            }
            catch (BadImageFormatException)
            {
                Logger.Warn($"Shared dependency '{dependencyPath}' is not a managed assembly and was ignored.");
            }
        }

        return availableDependencies;
    }

    private IReadOnlyCollection<DependencyFile> GetRequiredDependencies(
        string sourcePath,
        IReadOnlyDictionary<string, DependencyFile> availableDependencies)
    {

        var requiredDependencies = new Dictionary<string, DependencyFile>(StringComparer.OrdinalIgnoreCase);
        var visitedAssemblies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hostAssemblyName = typeof(ModuleBase).Assembly.GetName().Name;

        void VisitReferences(string assemblyPath)
        {
            foreach (var referenceName in GetReferencedAssemblyNames(assemblyPath))
            {
                if (referenceName.Equals(hostAssemblyName, StringComparison.OrdinalIgnoreCase) ||
                    !availableDependencies.TryGetValue(referenceName, out var dependency) ||
                    !visitedAssemblies.Add(dependency.Name))
                {
                    continue;
                }

                requiredDependencies.Add(dependency.Name, dependency);
                VisitReferences(dependency.Path);
            }
        }

        VisitReferences(sourcePath);
        return requiredDependencies.Values.ToArray();
    }

    private static HashSet<string> GetDependentDependencies(
        IReadOnlySet<string> changedDependencies,
        IReadOnlyDictionary<string, DependencyFile> availableDependencies)
    {
        var dependenciesToReload = new HashSet<string>(changedDependencies, StringComparer.OrdinalIgnoreCase);
        var pending = new Queue<string>(changedDependencies);

        while (pending.TryDequeue(out var dependencyName))
        {
            foreach (var dependency in availableDependencies.Values.Where(candidate => candidate.References.Contains(dependencyName)))
            {
                if (dependenciesToReload.Add(dependency.Name))
                    pending.Enqueue(dependency.Name);
            }
        }

        return dependenciesToReload;
    }

    private HashSet<string> NormalizeDependencyNames(
        IReadOnlySet<string> changedDependencies,
        IReadOnlyDictionary<string, DependencyFile> availableDependencies)
    {
        var normalizedDependencies = new HashSet<string>(changedDependencies, StringComparer.OrdinalIgnoreCase);

        foreach (var changedDependency in changedDependencies)
        {
            foreach (var dependency in availableDependencies.Values)
            {
                if (dependency.Name.Equals(changedDependency, StringComparison.OrdinalIgnoreCase) ||
                    Path.GetFileNameWithoutExtension(dependency.Path).Equals(changedDependency, StringComparison.OrdinalIgnoreCase))
                {
                    normalizedDependencies.Add(dependency.Name);
                }
            }

            foreach (var dependency in _sharedDependencies.Values)
            {
                if (dependency.Name.Equals(changedDependency, StringComparison.OrdinalIgnoreCase) ||
                    Path.GetFileNameWithoutExtension(dependency.Path).Equals(changedDependency, StringComparison.OrdinalIgnoreCase))
                {
                    normalizedDependencies.Add(dependency.Name);
                }
            }
        }

        return normalizedDependencies;
    }

    private IReadOnlyDictionary<string, SharedDependencyEntry> CreateSharedDependencySnapshot(
        IReadOnlyDictionary<string, DependencyFile> availableDependencies,
        IReadOnlySet<string> requiredDependencies,
        IReadOnlySet<string> dependenciesToReload)
    {
        var snapshot = _sharedDependencies
            .Where(pair => !dependenciesToReload.Contains(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        var createdEntries = new List<SharedDependencyEntry>();
        var building = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        SharedDependencyEntry BuildDependency(string dependencyName)
        {
            if (snapshot.TryGetValue(dependencyName, out var existing))
                return existing;

            if (!availableDependencies.TryGetValue(dependencyName, out var dependency))
                throw new FileNotFoundException($"Shared dependency '{dependencyName}' was not found in '{_dependenciesDirectory}'.");

            if (!building.Add(dependencyName))
                throw new InvalidOperationException($"Circular shared dependency reference involving '{dependencyName}' is not supported.");

            try
            {
                var references = dependency.References
                    .Where(availableDependencies.ContainsKey)
                    .ToDictionary(referenceName => referenceName, BuildDependency, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(pair => pair.Key, pair => pair.Value.Assembly, StringComparer.OrdinalIgnoreCase);
                var context = new SharedDependencyLoadContext(dependency.Name, references);
                var assembly = context.LoadAssembly(dependency.Path);
                var entry = new SharedDependencyEntry(dependency.Name, dependency.Path, context, assembly);
                snapshot[dependency.Name] = entry;
                createdEntries.Add(entry);
                return entry;
            }
            finally
            {
                building.Remove(dependencyName);
            }
        }

        try
        {
            foreach (var dependencyName in requiredDependencies)
                BuildDependency(dependencyName);

            return snapshot;
        }
        catch
        {
            UnloadUnusedSharedDependencies(createdEntries, _sharedDependencies.Values);
            throw;
        }
    }

    private void ReplaceSharedDependencies(IReadOnlyDictionary<string, SharedDependencyEntry> snapshot)
    {
        _sharedDependencies.Clear();
        foreach (var dependency in snapshot)
            _sharedDependencies[dependency.Key] = dependency.Value;
    }

    private void RemoveSharedDependencies(IReadOnlySet<string> dependencyNames)
    {
        var removedEntries = dependencyNames
            .Where(_sharedDependencies.ContainsKey)
            .Select(dependencyName => _sharedDependencies[dependencyName])
            .ToArray();

        foreach (var dependencyName in dependencyNames)
            _sharedDependencies.Remove(dependencyName);

        UnloadUnusedSharedDependencies(removedEntries, _sharedDependencies.Values);
    }

    private static void UnloadUnusedSharedDependencies(
        IEnumerable<SharedDependencyEntry> entries,
        IEnumerable<SharedDependencyEntry> retainedEntries)
    {
        var retainedContexts = retainedEntries.Select(entry => entry.Context).ToHashSet();
        foreach (var entry in entries.Where(entry => !retainedContexts.Contains(entry.Context)))
            entry.Context.Unload();
    }

    private static IEnumerable<string> GetReferencedAssemblyNames(string assemblyPath)
    {
        using var stream = File.OpenRead(assemblyPath);
        using var reader = new PEReader(stream);
        if (!reader.HasMetadata)
            throw new BadImageFormatException($"'{assemblyPath}' does not contain managed assembly metadata.");

        var metadata = reader.GetMetadataReader();
        foreach (var handle in metadata.AssemblyReferences)
        {
            var reference = metadata.GetAssemblyReference(handle);
            var name = metadata.GetString(reference.Name);
            if (!string.IsNullOrWhiteSpace(name))
                yield return name;
        }
    }

    private string CopyModulePackage(string packageName, string sourcePath)
    {
        var packagePath = Path.Combine(_workingDirectory, packageName, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(packagePath);

        File.Copy(sourcePath, Path.Combine(packagePath, Path.GetFileName(sourcePath)), overwrite: false);

        return packagePath;
    }

    private void RegisterViews(string moduleName, ModuleEntry entry)
    {
        if (_serviceProvider?.GetService<IModuleViewEngine>() is not { } viewEngine)
            return;

        viewEngine.UnregisterModuleViews(moduleName);
        viewEngine.RegisterModuleViews(moduleName, entry.Assembly);
        entry.Module.RegisterViews(viewEngine);
    }

    private void RestoreViews(string moduleName, ModuleEntry entry)
    {
        if (_serviceProvider?.GetService<IModuleViewEngine>() is not { } viewEngine)
            return;

        viewEngine.UnregisterModuleViews(moduleName);
        viewEngine.RegisterModuleViews(moduleName, entry.Assembly);
        entry.Module.RegisterViews(viewEngine);
    }

    internal async Task UnloadModule(string name)
    {
        await _lifecycleGate.WaitAsync();
        try
        {
            await UnloadModuleCoreAsync(name);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    internal async Task UnloadModuleBySourcePathAsync(string sourcePath)
    {
        await _lifecycleGate.WaitAsync();
        try
        {
            var normalizedPath = Path.GetFullPath(sourcePath);
            foreach (var entry in _modules.ToArray().Where(entry => PathsEqual(entry.Value.SourcePath, normalizedPath)))
                await UnloadModuleCoreAsync(entry.Key);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    private async Task UnloadModuleCoreAsync(string name)
    {
        var moduleLock = _moduleLocks.GetOrAdd(name, static _ => new SemaphoreSlim(1, 1));
        await moduleLock.WaitAsync();
        try
        {
            if (_modules.TryRemove(name, out var entry))
                await CleanupEntryAsync(name, entry, unregisterViews: true, notify: true);
        }
        finally
        {
            moduleLock.Release();
        }
    }

    private async Task CleanupEntryAsync(string moduleName, ModuleEntry entry, bool unregisterViews, bool notify)
    {
        await WaitForRequestsToFinishAsync(entry.Module, moduleName);

        try
        {
            await entry.Module.OnModuleUnload();
        }
        catch (Exception ex)
        {
            Logger.Error($"Module '{moduleName}' failed during unload: {ex}");
        }

        if (notify)
            Events.Events.ModuleUnloadedSafeEvent.Invoke(new ModuleUnloadedEventArgs(moduleName));

        if (unregisterViews && _serviceProvider?.GetService<IModuleViewEngine>() is { } viewEngine)
            viewEngine.UnregisterModuleViews(moduleName);

        RemoveContextRegistrations(entry.Context);
        UnloadContext(entry.Context, entry.PackagePath, moduleName);
        Logger.Info($"Module unloaded: {moduleName}");
    }

    private async Task WaitForRequestsToFinishAsync(ModuleBase module, string moduleName)
    {
        var timeout = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (_activeRequests.TryGetValue(module, out var count) && count > 0 && DateTime.UtcNow < timeout)
            await Task.Delay(50);

        if (_activeRequests.TryGetValue(module, out var active) && active > 0)
            Logger.Warn($"Module '{moduleName}' still has {active} active request(s); unloading after the 30 second drain timeout.");
    }

    private static void RemoveContextRegistrations(AssemblyLoadContext context)
    {
        Events.Events.RemoveModuleHandlers(context);
        ModuleMessenger.ModuleMessenger.RemoveModuleHandlers(context);
    }

    private static void UnloadContext(AssemblyLoadContext context, string packagePath, string moduleName)
    {
        context.Unload();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                Directory.Delete(packagePath, recursive: true);
                return;
            }
            catch (IOException) when (attempt < 2)
            {
                Thread.Sleep(100);
            }
            catch (UnauthorizedAccessException) when (attempt < 2)
            {
                Thread.Sleep(100);
            }
            catch (Exception ex)
            {
                Logger.Warn($"Could not remove temporary package for module '{moduleName}': {ex.Message}");
                return;
            }
        }

        Logger.Warn($"Could not remove temporary package for module '{moduleName}' after three attempts.");
    }

    private static string ValidateModuleName(string? moduleName, string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(moduleName))
            throw new InvalidOperationException($"Module '{sourcePath}' has an empty {nameof(ModuleBase.ModuleName)}.");

        if (moduleName.IndexOfAny(['/', '\\']) >= 0)
            throw new InvalidOperationException($"Module '{sourcePath}' has invalid {nameof(ModuleBase.ModuleName)} '{moduleName}'. Module names cannot contain path separators.");

        return moduleName;
    }

    private static bool PathsEqual(string left, string right)
        => string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _watcher.Dispose();
        foreach (var moduleName in _modules.Keys.ToArray())
            UnloadModule(moduleName).GetAwaiter().GetResult();

        foreach (var dependency in _sharedDependencies.Values)
            dependency.Context.Unload();
        _sharedDependencies.Clear();

        foreach (var moduleLock in _moduleLocks.Values)
            moduleLock.Dispose();

        _lifecycleGate.Dispose();
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
    }
}
