using System.Reflection;
using System.Runtime.Loader;

namespace ModuWeb.ModuleLoadSystem;

/// <summary>
/// Collectible load context for one isolated module package.
/// </summary>
internal sealed class ModuleLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;
    private readonly IReadOnlyDictionary<string, string> _sharedDependencies;
    private readonly string _moduleDirectory;

    internal ModuleLoadContext(string moduleAssemblyPath, IReadOnlyDictionary<string, string> sharedDependencies)
        : base(Path.GetFileNameWithoutExtension(moduleAssemblyPath), isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(moduleAssemblyPath);
        _sharedDependencies = sharedDependencies;
        _moduleDirectory = Path.GetDirectoryName(moduleAssemblyPath)
            ?? throw new ArgumentException("The module assembly path must include a directory.", nameof(moduleAssemblyPath));
    }

    internal Assembly LoadMainAssembly(string moduleAssemblyPath)
        => LoadAssemblyWithoutFileLock(moduleAssemblyPath);

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath is not null)
            return LoadAssemblyWithoutFileLock(assemblyPath);

        var fallbackPath = Path.Combine(_moduleDirectory, $"{assemblyName.Name}.dll");
        if (File.Exists(fallbackPath))
            return LoadAssemblyWithoutFileLock(fallbackPath);

        return assemblyName.Name is not null && _sharedDependencies.TryGetValue(assemblyName.Name, out var sharedDependencyPath)
            ? LoadAssemblyWithoutFileLock(sharedDependencyPath)
            : null;
    }

    protected override nint LoadUnmanagedDll(string unmanagedDllName)
    {
        var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return libraryPath is null ? nint.Zero : LoadUnmanagedDllFromPath(libraryPath);
    }

    private Assembly LoadAssemblyWithoutFileLock(string assemblyPath)
    {
        using var stream = File.Open(assemblyPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return LoadFromStream(stream);
    }
}
