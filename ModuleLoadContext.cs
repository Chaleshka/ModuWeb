using System.Reflection;
using System.Runtime.Loader;

namespace ModuWeb;

/// <summary>
/// Custom <see cref="AssemblyLoadContext"/> for loading individual modules and resolving their dependencies.
/// </summary>
internal class ModuleLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;
    private readonly string _dependenciesPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModuleLoadContext"/> class with the specified module path and dependency directory.
    /// </summary>
    /// <param name="modulePath">Path to the main module assembly (.dll).</param>
    /// <param name="dependenciesPath">Path to the directory containing module-specific dependencies.</param>
    internal ModuleLoadContext(string modulePath, string dependenciesPath)
        : base(isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(modulePath);
        _dependenciesPath = dependenciesPath;

        Resolving += (context, name) =>
        {
            var depPath = Path.Combine(_dependenciesPath, $"{name.Name}.dll");
            return File.Exists(depPath) ? LoadFromAssemblyPath(depPath) : null;
        };
    }

    /// <summary>
    /// Returns and loads assemblies using the provided <see cref="AssemblyDependencyResolver"/>.
    /// </summary>
    /// <param name="assemblyName">The name of the assembly to resolve.</param>
    /// <returns>The loaded assembly, or <c>null</c> if resolution failed.</returns>
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        string? path = _resolver.ResolveAssemblyToPath(assemblyName);
        return path != null ? LoadFromAssemblyPath(path) : null;
    }
}
