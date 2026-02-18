using System.Reflection;
using System.Text;
using RazorLight;

namespace ModuWeb.ViewEngine;

/// <summary>
/// Razor view engine for modules using RazorLight (runtime Razor compilation).
/// </summary>
public class ModuleViewEngine : IModuleViewEngine
{
    private readonly RazorLightEngine _engine;
    private readonly Dictionary<string, Assembly> _moduleAssemblies = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<string, string>> _templateCache = new(StringComparer.OrdinalIgnoreCase);

    public ModuleViewEngine()
    {
        _engine = new RazorLightEngineBuilder()
            .UseMemoryCachingProvider()
            .Build();
    }

    public void RegisterModuleViews(string moduleName, Assembly moduleAssembly)
    {
        ArgumentException.ThrowIfNullOrEmpty(moduleName);
        ArgumentNullException.ThrowIfNull(moduleAssembly);

        _moduleAssemblies[moduleName] = moduleAssembly;

        var templates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var resourceNames = moduleAssembly.GetManifestResourceNames()
            .Where(n => n.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase));

        foreach (var resourceName in resourceNames)
        {
            var content = LoadEmbeddedResource(moduleAssembly, resourceName);
            if (content == null)
                continue;

            var viewKey = ResourceNameToViewKey(moduleAssembly.GetName().Name, resourceName);
            templates[viewKey] = content;
            templates[viewKey + ".cshtml"] = content;
        }

        _templateCache[moduleName] = templates;
    }

    public async Task<string> RenderModuleViewAsync(string moduleName, string viewName, object? model = null,
        Dictionary<string, object>? viewData = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(moduleName);
        ArgumentException.ThrowIfNullOrEmpty(viewName);

        if (!_templateCache.TryGetValue(moduleName, out var templates))
            throw new InvalidOperationException($"Module '{moduleName}' views are not registered. Call RegisterModuleViews first.");

        var normalizedViewName = viewName.Replace('\\', '/').TrimStart('/');
        if (!TryGetTemplateContent(templates, moduleName, normalizedViewName, out var templateContent))
            throw new InvalidOperationException($"View '{viewName}' not found in module '{moduleName}'.");

        var cacheKey = $"{moduleName}:{normalizedViewName}";

        object? renderModel = model;
        if (viewData != null && viewData.Count > 0)
        {
            var expando = new System.Dynamic.ExpandoObject();
            var dict = (IDictionary<string, object?>)expando;
            if (model != null)
            {
                foreach (var p in model.GetType().GetProperties())
                    dict[p.Name] = p.GetValue(model);
                foreach (var kv in viewData)
                    dict[kv.Key] = kv.Value;
            }
            else
            {
                foreach (var kv in viewData)
                    dict[kv.Key] = kv.Value;
            }
            renderModel = expando;
        }

        return await _engine.CompileRenderStringAsync(cacheKey, templateContent, renderModel ?? new object());
    }

    private bool TryGetTemplateContent(Dictionary<string, string> templates, string moduleName, string viewName, out string? content)
    {
        if (templates.TryGetValue(viewName, out content!))
            return true;

        var altKey = viewName.Replace("/", ".");
        foreach (var kv in templates)
        {
            if (kv.Key.EndsWith(altKey, StringComparison.OrdinalIgnoreCase) ||
                kv.Key.Replace(".", "/").EndsWith(viewName, StringComparison.OrdinalIgnoreCase))
            {
                content = kv.Value;
                return true;
            }
        }

        content = null;
        return false;
    }

    private static string? LoadEmbeddedResource(Assembly assembly, string resourceName)
    {
        try
        {
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
                return null;
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return reader.ReadToEnd();
        }
        catch
        {
            return null;
        }
    }

    private static string ResourceNameToViewKey(string? assemblyName, string resourceName)
    {
        string path = resourceName;
        if (!string.IsNullOrEmpty(assemblyName))
        {
            var prefix = assemblyName + ".";
            if (path.StartsWith(prefix, StringComparison.Ordinal))
                path = path[prefix.Length..];
        }
        if (path.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase))
            path = path[..^".cshtml".Length];
        return path.Replace(".", "/");
    }
}
