using System.Collections.Concurrent;

namespace ModuWeb.ModuleLoadSystem;

/// <summary>
/// Watches module and shared dependency DLLs, then debounces writes before reloading.
/// </summary>
internal sealed class ModuleWatcher : IDisposable
{
    private readonly string _modulesDirectory;
    private readonly string _dependenciesDirectory;
    private readonly FileSystemWatcher _watcher;
    private readonly FileSystemWatcher _dependencyWatcher;
    private readonly ConcurrentDictionary<string, DateTime> _pendingChanges = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DateTime> _pendingDependencyChanges = new(StringComparer.OrdinalIgnoreCase);
    private readonly CancellationTokenSource _cts = new();
    private Task? _monitorTask;
    private int _started;

    private static readonly TimeSpan StabilityDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);

    internal ModuleWatcher(string modulesDirectory)
    {
        _modulesDirectory = modulesDirectory;
        _dependenciesDirectory = Path.Combine(modulesDirectory, "dependencies");
        _watcher = new FileSystemWatcher(_modulesDirectory, "*.dll")
        {
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
            EnableRaisingEvents = false
        };

        _watcher.Created += OnChanged;
        _watcher.Changed += OnChanged;
        _watcher.Deleted += OnDeleted;
        _watcher.Renamed += OnRenamed;
        _watcher.Error += OnError;

        _dependencyWatcher = new FileSystemWatcher(_dependenciesDirectory, "*.dll")
        {
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
            EnableRaisingEvents = false
        };
        _dependencyWatcher.Created += OnDependencyChanged;
        _dependencyWatcher.Changed += OnDependencyChanged;
        _dependencyWatcher.Deleted += OnDependencyChanged;
        _dependencyWatcher.Renamed += OnDependencyRenamed;
        _dependencyWatcher.Error += OnError;
    }

    internal void Start()
    {
        if (Interlocked.Exchange(ref _started, 1) != 0)
            return;

        _monitorTask = Task.Run(MonitorPendingChangesAsync);
        _watcher.EnableRaisingEvents = true;
        _dependencyWatcher.EnableRaisingEvents = true;
    }

    private async Task MonitorPendingChangesAsync()
    {
        while (!_cts.Token.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            foreach (var change in _pendingChanges.ToArray())
            {
                if (now - change.Value < StabilityDelay || !_pendingChanges.TryRemove(change.Key, out _))
                    continue;

                if (!File.Exists(change.Key))
                {
                    _ = UnloadSafelyAsync(change.Key);
                }
                else if (IsFileReady(change.Key) && HasFileBeenStable(change.Key, StabilityDelay))
                {
                    _ = ReloadSafelyAsync(change.Key);
                }
                else
                {
                    _pendingChanges[change.Key] = now;
                }
            }

            var changedDependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var change in _pendingDependencyChanges.ToArray())
            {
                if (now - change.Value < StabilityDelay || !_pendingDependencyChanges.TryRemove(change.Key, out _))
                    continue;

                if (!File.Exists(change.Key) || (IsFileReady(change.Key) && HasFileBeenStable(change.Key, StabilityDelay)))
                {
                    changedDependencies.Add(Path.GetFileNameWithoutExtension(change.Key));
                    if (File.Exists(change.Key))
                    {
                        try
                        {
                            var assemblyName = System.Reflection.AssemblyName.GetAssemblyName(change.Key).Name;
                            if (!string.IsNullOrWhiteSpace(assemblyName))
                                changedDependencies.Add(assemblyName);
                        }
                        catch (BadImageFormatException)
                        {
                            // The module manager will log and ignore unmanaged DLLs.
                        }
                    }
                }
                else
                    _pendingDependencyChanges[change.Key] = now;
            }

            if (changedDependencies.Count > 0)
                _ = ReloadModulesUsingDependenciesSafelyAsync(changedDependencies);

            try
            {
                await Task.Delay(PollInterval, _cts.Token);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    private static async Task ReloadSafelyAsync(string path)
    {
        try
        {
            await ModuleManager.Instance.ReloadModule(path);
        }
        catch (Exception ex)
        {
            Logger.Error($"Module reload failed for '{path}': {ex}");
        }
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        if (IsModulePath(e.FullPath))
            _pendingChanges[e.FullPath] = DateTime.UtcNow;
    }

    private void OnDeleted(object sender, FileSystemEventArgs e)
    {
        if (!IsModulePath(e.FullPath))
            return;

        _pendingChanges[e.FullPath] = DateTime.UtcNow;
    }

    private void OnDependencyChanged(object sender, FileSystemEventArgs e)
    {
        if (IsDependencyPath(e.FullPath))
            _pendingDependencyChanges[e.FullPath] = DateTime.UtcNow;
    }

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        if (IsModulePath(e.OldFullPath))
            OnDeleted(sender, new FileSystemEventArgs(WatcherChangeTypes.Deleted, _modulesDirectory, Path.GetFileName(e.OldFullPath)));
        if (IsModulePath(e.FullPath))
            OnChanged(sender, new FileSystemEventArgs(WatcherChangeTypes.Created, _modulesDirectory, Path.GetFileName(e.FullPath)));
    }

    private void OnDependencyRenamed(object sender, RenamedEventArgs e)
    {
        if (IsDependencyPath(e.OldFullPath))
            OnDependencyChanged(sender, new FileSystemEventArgs(WatcherChangeTypes.Deleted, _dependenciesDirectory, Path.GetFileName(e.OldFullPath)));
        if (IsDependencyPath(e.FullPath))
            OnDependencyChanged(sender, new FileSystemEventArgs(WatcherChangeTypes.Created, _dependenciesDirectory, Path.GetFileName(e.FullPath)));
    }

    private void OnError(object sender, ErrorEventArgs e)
    {
        Logger.Warn($"Module file watcher lost events and will rescan: {e.GetException().Message}");
        _ = RescanSafelyAsync();
    }

    private static async Task UnloadSafelyAsync(string sourcePath)
    {
        try
        {
            await ModuleManager.Instance.UnloadModuleBySourcePathAsync(sourcePath);
        }
        catch (Exception ex)
        {
            Logger.Error($"Module unload failed for '{sourcePath}': {ex}");
        }
    }

    private static async Task RescanSafelyAsync()
    {
        try
        {
            await ModuleManager.Instance.RescanModulesAsync();
        }
        catch (Exception ex)
        {
            Logger.Error($"Module rescan failed: {ex}");
        }
    }

    private static async Task ReloadModulesUsingDependenciesSafelyAsync(IReadOnlyCollection<string> dependencyNames)
    {
        try
        {
            Logger.Info($"Shared module dependencies changed: {string.Join(", ", dependencyNames)}. Reloading their consumers.");
            await ModuleManager.Instance.ReloadModulesUsingDependenciesAsync(dependencyNames);
        }
        catch (Exception ex)
        {
            Logger.Error($"Reload after shared dependency change failed: {ex}");
        }
    }

    private bool IsModulePath(string path)
        => string.Equals(Path.GetDirectoryName(path), _modulesDirectory, StringComparison.OrdinalIgnoreCase);

    private bool IsDependencyPath(string path)
        => string.Equals(Path.GetDirectoryName(path), _dependenciesDirectory, StringComparison.OrdinalIgnoreCase);

    private static bool IsFileReady(string path)
    {
        try
        {
            using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool HasFileBeenStable(string path, TimeSpan duration)
    {
        try
        {
            return DateTime.UtcNow - File.GetLastWriteTimeUtc(path) >= duration;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        _watcher.EnableRaisingEvents = false;
        _dependencyWatcher.EnableRaisingEvents = false;
        _cts.Cancel();
        _watcher.Dispose();
        _dependencyWatcher.Dispose();
        _cts.Dispose();
    }
}
