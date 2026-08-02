using System.Reflection;
using System.Runtime.Loader;

namespace ModuWeb.ModuleLoadSystem;

/// <summary>
/// Owns one generation of a shared managed dependency.
/// References to other shared dependencies are supplied by the manager so every
/// module receives the same assembly instance for a given dependency generation.
/// </summary>
internal sealed class SharedDependencyLoadContext : AssemblyLoadContext
{
    private readonly IReadOnlyDictionary<string, Assembly> _referencedDependencies;

    internal SharedDependencyLoadContext(string dependencyName, IReadOnlyDictionary<string, Assembly> referencedDependencies)
        : base($"shared:{dependencyName}:{Guid.NewGuid():N}", isCollectible: true)
    {
        _referencedDependencies = referencedDependencies;
    }

    internal Assembly LoadAssembly(string assemblyPath)
    {
        using var stream = File.Open(assemblyPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return LoadFromStream(stream);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
        => assemblyName.Name is not null && _referencedDependencies.TryGetValue(assemblyName.Name, out var assembly)
            ? assembly
            : null;
}
