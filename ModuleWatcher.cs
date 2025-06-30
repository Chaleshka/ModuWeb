using System.Collections.Concurrent;

namespace ModuWeb;

/// <summary>
/// Watches the modules directory for changes to .dll files and triggers module reloads or unloads.
/// </summary>
internal class ModuleWatcher : IDisposable
{
    private readonly string _modulesDirectory;
    private readonly string _modulesWorkingDirectory;
    private readonly FileSystemWatcher _watcher;
    private readonly ConcurrentDictionary<string, DateTime> _pendingChanges = new();
    private readonly CancellationTokenSource _cts = new();

    private static readonly TimeSpan StabilityDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Initializes a new instance of <see cref="ModuleWatcher"/> for the specified modules directory.
    /// Automatically starts watching for changes and runs a background task for delayed processing.
    /// </summary>
    /// <param name="modulesDirectory">The root directory containing module DLLs.</param>
    internal ModuleWatcher(string modulesDirectory)
    {
        _modulesDirectory = modulesDirectory;
        _modulesWorkingDirectory = Path.Combine(modulesDirectory, "temp");
        _watcher = new FileSystemWatcher(_modulesDirectory, "*.dll")
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
            EnableRaisingEvents = true
        };

        _watcher.Created += OnChanged;
        _watcher.Changed += OnChanged;
        _watcher.Deleted += OnDeleted;
        _watcher.Renamed += OnRenamed;

        Task.Run(() => MonitorPendingChanges());
    }

    /// <summary>
    /// Periodically checks for pending file changes to avoid reloading modules too frequently.
    /// </summary>
    private async Task MonitorPendingChanges()
    {
        while (!_cts.Token.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;

            foreach (var kvp in _pendingChanges.ToArray())
            {
                string path = kvp.Key;
                DateTime firstDetected = kvp.Value;

                if ((now - firstDetected) > StabilityDelay)
                {
                    if (_pendingChanges.TryRemove(path, out _))
                    {
                        if (IsFileReady(path) && HasFileBeenStable(path, StabilityDelay))
                        {
                            ModuleManager.Instance.ReloadModule(path);
                        }
                        else
                            _pendingChanges[path] = now;
                    }
                }
            }

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

    /// <summary>
    /// Called when a file is created or changed.
    /// Marks the file as pending to be reloaded after a short delay.
    /// </summary>
    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        if (e.FullPath.Contains(_modulesWorkingDirectory))
            return;

        _pendingChanges[e.FullPath] = DateTime.UtcNow;
    }

    /// <summary>
    /// Called when a file is deleted.
    /// Triggers unloading of the corresponding module if applicable.
    /// </summary>
    private void OnDeleted(object sender, FileSystemEventArgs e)
    {
        if (_pendingChanges.ContainsKey(e.FullPath) || 
            e.FullPath.Contains(_modulesWorkingDirectory))
            return;

        var name = Path.GetFileNameWithoutExtension(e.FullPath).ToLower();
        ModuleManager.Instance.UnloadModule(name);
    }

    /// <summary>
    /// Called when a file is renamed.
    /// </summary>
    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        if (e.FullPath.Contains(_modulesWorkingDirectory))
            return;

        OnDeleted(sender, new FileSystemEventArgs(WatcherChangeTypes.Deleted, Path.GetDirectoryName(e.OldFullPath)!, Path.GetFileName(e.OldFullPath)));
        OnChanged(sender, new FileSystemEventArgs(WatcherChangeTypes.Created, Path.GetDirectoryName(e.FullPath)!, Path.GetFileName(e.FullPath)));
    }

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
            var lastWrite = File.GetLastWriteTimeUtc(path);
            return (DateTime.UtcNow - lastWrite) >= duration;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        _watcher.Dispose();
        _cts.Cancel();
        _cts.Dispose();
    }
}
