using ModuWeb.ViewEngine;

namespace ModuWeb;

/// <summary>
/// Represents a base class for modules providing routing, CORS configuration, and lifecycle hooks.
/// </summary>
public abstract class ModuleBase
{
    private static ulong _moduleCounter = 0;
    /// <summary>
    /// Gets the name of module that will be used into core. <br/>
    /// MUST be unique name.
    /// </summary>
    public virtual string ModuleName { get; } = $"Module{Interlocked.Increment(ref _moduleCounter)}";

    /// <summary>
    /// Gets the list of allowed CORS origins for this module. <br/>
    /// Override to specify custom allowed origins.
    /// </summary>
    public virtual string[] WithOriginsCors { get; } = Array.Empty<string>();

    /// <summary>
    /// Gets the list of allowed CORS headers for this module. <br/>
    /// Override to specify custom allowed headers.
    /// </summary>
    public virtual string[] WithHeadersCors { get; } = Array.Empty<string>();

    /// <summary>
    /// Gets a value indicating failed CORS requests be blocked for this module. <br/>
    /// Override to specify failed requests be blocked.
    /// </summary>
    public virtual bool BlockFailedCorsRequests { get; } = false;

    /// <summary>
    /// Internal collection of routes mapped for this module.
    /// </summary>

    protected readonly RouteDictionary _routes = new();
    
    /// <summary>
    /// Maps HTTP method and path to a request handler for this module.
    /// </summary>
    /// <param name="path">Relative route path (leading/trailing slashes are trimmed).</param>
    /// <param name="method">HTTP method (e.g., GET, POST).</param>
    /// <param name="handler">Asynchronous handler function to process the request.</param>
    protected void Map(string path, string method, Func<HttpContext, Task> handler)
    {
        path = path.Trim('/');
        _routes.Add(path, method, handler);
    }

    /// <summary>
    /// Handles an incoming HTTP request by routing it to the appropriate handler. <br/>
    /// Returns 404 if route not found or 405 if method not allowed.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="modulePath">The route path relative to the module base.</param>
    /// <param name="method">The HTTP method of the request.</param>
    public virtual async Task Handle(HttpContext context, string modulePath, string method)
    {
        try
        {
            if (!_routes.ContainsPath(modulePath))
            {
                context.Response.StatusCode = 404;
                return;
            }

            if (!_routes.ContainsMethod(modulePath, method))
            {
                context.Response.StatusCode = 405;
                return;
            }

            await _routes.GetHandler(modulePath, method).Invoke(context);

        }
        catch (Exception ex)
        {
            Logger.Error("Module handler throw " + ex);
        }
    }

    /// <summary>
    /// Called when the module is loaded. <br/>
    /// Override to perform initialization tasks.
    /// </summary>
    public virtual async Task OnModuleLoad() { }

    /// <summary>
    /// Called when the module is unloaded. <br/>
    /// Override to perform cleanup tasks.
    /// </summary>
    public virtual async Task OnModuleUnload() { }

    /// <summary>
    /// Registers Razor views for this module using the provided view engine. <br/>
    /// Override in derived modules to register embedded .cshtml resources.
    /// </summary>
    /// <param name="viewEngine">The module view engine instance.</param>
    public virtual void RegisterViews(IModuleViewEngine viewEngine) { }

    /// <summary>
    /// Provides initial view data for Razor views rendered by this module. <br/>
    /// Override to supply common values like base paths, locale, or settings.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <returns>A dictionary of initial view data.</returns>
    protected virtual Dictionary<string, object> GetInitialViewData(HttpContext context)
        => new();

    /// <summary>
    /// Public method to retrieve initial view data for use by extension methods.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <returns>A dictionary of initial view data.</returns>
    internal Dictionary<string, object> GetInitialViewDataForExtension(HttpContext context)
        => GetInitialViewData(context);
}
