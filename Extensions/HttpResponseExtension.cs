using ModuWeb.ViewEngine;

namespace ModuWeb.Extensions;

public static class HttpResponseExtension
{
    /// <summary>
    /// Renders a Razor page and writes the result to the HTTP response.
    /// If viewData is null, automatically retrieves initial view data from the current module's GetInitialViewData method.
    /// </summary>
    /// <param name="response">The HTTP response to write to.</param>
    /// <param name="viewName">Name of the Razor view (e.g., "Views/Index.cshtml").</param>
    /// <param name="model">The model object to pass to the Razor view.</param>
    /// <param name="viewData">Optional additional view data dictionary. If null, GetInitialViewData from the module will be used.</param>
    /// <exception cref="InvalidOperationException">Thrown if no module is found in the current request context.</exception>
    public static async Task WriteRazorPageAsync(
        this HttpResponse response,
        string viewName,
        object? model = null,
        Dictionary<string, object>? viewData = null)
    {
        var context = response.HttpContext;

        if (!context.Items.TryGetValue("ModuWeb.CurrentModule", out var moduleObj) ||
            moduleObj is not ModuleBase module)
        {
            throw new InvalidOperationException(
                "Cannot render Razor page: no module found in the current request context. " +
                "This extension method can only be called from within a module handler.");
        }

        var viewEngine = context.RequestServices.GetRequiredService<IModuleViewEngine>();
        var moduleName = ModuleManager.Instance.GetModuleName(module);

        if (viewData == null)
        {
            viewData = module.GetInitialViewDataForExtension(context);
        }

        var html = await viewEngine.RenderModuleViewAsync(
            moduleName,
            viewName,
            model,
            viewData);

        response.ContentType = "text/html; charset=utf-8";
        await response.WriteAsync(html);
    }
}
