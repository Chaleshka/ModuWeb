using System.Reflection;

namespace ModuWeb.ViewEngine;

/// <summary>
/// Service for rendering Razor views from modules at runtime.
/// </summary>
public interface IModuleViewEngine
{
    /// <summary>
    /// Registers views from a module assembly. Embedded .cshtml resources are loaded and cached.
    /// </summary>
    /// <param name="moduleName">Unique module name.</param>
    /// <param name="moduleAssembly">Assembly containing embedded view resources.</param>
    void RegisterModuleViews(string moduleName, System.Reflection.Assembly moduleAssembly);

    /// <summary>
    /// Renders a view for a registered module.
    /// </summary>
    /// <param name="moduleName">Module name passed to RegisterModuleViews.</param>
    /// <param name="viewName">View path (e.g. "Views/Index.cshtml").</param>
    /// <param name="model">Optional model.</param>
    /// <param name="viewData">Optional view data.</param>
    /// <returns>Rendered HTML string.</returns>
    Task<string> RenderModuleViewAsync(string moduleName, string viewName, object? model = null,
        Dictionary<string, object>? viewData = null);
}
