using System.Reflection;
using System.Text;
using RazorLight;

namespace ModuWeb.ViewEngine;

/// <summary>
/// Razor view engine that drops compiled-template references whenever module views change.
/// </summary>
public class ModuleViewEngine : IModuleViewEngine
{
    private readonly object _sync = new();
    private RazorLightEngine _engine = CreateEngine();
    private readonly Dictionary<string, Dictionary<string, string>> _templateCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, long> _generations = new(StringComparer.OrdinalIgnoreCase);

    public void RegisterModuleViews(string moduleName, Assembly moduleAssembly)
    {
        ArgumentException.ThrowIfNullOrEmpty(moduleName);
        ArgumentNullException.ThrowIfNull(moduleAssembly);

        var templates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var resourceName in moduleAssembly.GetManifestResourceNames().Where(name => name.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase)))
        {
            var content = LoadEmbeddedResource(moduleAssembly, resourceName);
            if (content is null)
                continue;

            var viewKey = ResourceNameToViewKey(moduleAssembly.GetName().Name, resourceName);
            templates[viewKey] = content;
            templates[viewKey + ".cshtml"] = content;
        }

        lock (_sync)
        {
            _templateCache[moduleName] = templates;
            _generations[moduleName] = _generations.GetValueOrDefault(moduleName) + 1;
            ResetEngine();
        }
    }

    public void UnregisterModuleViews(string moduleName)
    {
        ArgumentException.ThrowIfNullOrEmpty(moduleName);
        lock (_sync)
        {
            if (!_templateCache.Remove(moduleName))
                return;

            _generations.Remove(moduleName);
            ResetEngine();
        }
    }

    public async Task<string> RenderModuleViewAsync(string moduleName, string viewName, object? model = null,
        Dictionary<string, object>? viewData = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(moduleName);
        ArgumentException.ThrowIfNullOrEmpty(viewName);

        string templateContent;
        string cacheKey;
        RazorLightEngine engine;
        lock (_sync)
        {
            if (!_templateCache.TryGetValue(moduleName, out var templates))
                throw new InvalidOperationException($"Module '{moduleName}' views are not registered.");

            var normalizedViewName = viewName.Replace('\\', '/').TrimStart('/');
            if (!TryGetTemplateContent(templates, normalizedViewName, out templateContent))
                throw new InvalidOperationException($"View '{viewName}' not found in module '{moduleName}'.");

            cacheKey = $"{moduleName}:{_generations[moduleName]}:{normalizedViewName}";
            engine = _engine;
        }

        var renderModel = CreateRenderModel(model, viewData);
        return await engine.CompileRenderStringAsync(cacheKey, templateContent, renderModel ?? new object());
    }

    private static RazorLightEngine CreateEngine() => new RazorLightEngineBuilder().UseMemoryCachingProvider().Build();

    private void ResetEngine() => _engine = CreateEngine();

    private static object? CreateRenderModel(object? model, Dictionary<string, object>? viewData)
    {
        if (viewData is null || viewData.Count == 0)
            return model;

        var expando = new System.Dynamic.ExpandoObject();
        var values = (IDictionary<string, object?>)expando;
        if (model is not null)
        {
            foreach (var property in model.GetType().GetProperties())
                values[property.Name] = property.GetValue(model);
        }
        foreach (var value in viewData)
            values[value.Key] = value.Value;
        return expando;
    }

    private static bool TryGetTemplateContent(Dictionary<string, string> templates, string viewName, out string content)
    {
        if (templates.TryGetValue(viewName, out content!))
            return true;

        var alternativeKey = viewName.Replace("/", ".");
        foreach (var template in templates)
        {
            if (template.Key.EndsWith(alternativeKey, StringComparison.OrdinalIgnoreCase) ||
                template.Key.Replace(".", "/").EndsWith(viewName, StringComparison.OrdinalIgnoreCase))
            {
                content = template.Value;
                return true;
            }
        }

        content = null!;
        return false;
    }

    private static string? LoadEmbeddedResource(Assembly assembly, string resourceName)
    {
        try
        {
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is null)
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
        var path = resourceName;
        if (!string.IsNullOrEmpty(assemblyName) && path.StartsWith(assemblyName + ".", StringComparison.Ordinal))
            path = path[(assemblyName.Length + 1)..];
        if (path.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase))
            path = path[..^".cshtml".Length];
        return path.Replace(".", "/");
    }
}
