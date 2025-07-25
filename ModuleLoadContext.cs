using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Loader;

namespace ModuWeb;

/// <summary>
/// Custom <see cref="AssemblyLoadContext"/> for loading individual modules and resolving their dependencies.
/// </summary>
internal class ModuleLoadContext : AssemblyLoadContext
{
    private static readonly ConcurrentDictionary<string, WeakReference<Assembly>> _dependencyCache = new();
    private readonly string _dependenciesPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModuleLoadContext"/> class with the specified module path and dependency directory.
    /// </summary>
    /// <param name="dependenciesPath">Path to the directory containing module-specific dependencies.</param>
    internal ModuleLoadContext(string dependenciesPath) : base(isCollectible: true)
    {
        _dependenciesPath = dependenciesPath;
    }

    /// <summary>
    /// Returns and loads assemblies using the provided <see cref="AssemblyDependencyResolver"/>.
    /// </summary>
    /// <param name="assemblyName">The name of the assembly to resolve.</param>
    /// <returns>The loaded assembly, or <c>null</c> if resolution failed.</returns>
    protected override Assembly Load(AssemblyName assemblyName)
    {
        if (_dependencyCache.TryGetValue(assemblyName.Name, out var weakRef) &&
            weakRef.TryGetTarget(out Assembly cachedAssembly))
        {
            return cachedAssembly;
        }

        string depPath = Path.Combine(_dependenciesPath, $"{assemblyName.Name}.dll");
        if (File.Exists(depPath))
        {
            byte[] bytes = File.ReadAllBytes(depPath);
            var assembly = LoadFromStream(new MemoryStream(bytes));

            _dependencyCache[assemblyName.Name] = new WeakReference<Assembly>(assembly);
            return assembly;
        }

        return base.Load(assemblyName);
    }
}
