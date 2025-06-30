using System.IO;

namespace ModuWeb;

/// <summary>
/// Represents a base class for modules providing routing, CORS configuration, and lifecycle hooks.
/// </summary>
public abstract class ModuleBase
{
    /// <summary>
    /// Gets the list of allowed CORS origins for this module.
    /// Override to specify custom allowed origins.
    /// </summary>
    public virtual string[] WithOriginsCors { get; } = Array.Empty<string>();

    /// <summary>
    /// Gets the list of allowed CORS headers for this module.
    /// Override to specify custom allowed headers.
    /// </summary>
    public virtual string[] WithHeadersCors { get; } = Array.Empty<string>();

    /// <summary>
    /// Internal collection of routes mapped for this module.
    /// </summary>

    protected readonly RouteDictionary _routes = new();
    
    /// <summary>
    /// Maps a HTTP method and path to a request handler for this module.
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
    /// Handles an incoming HTTP request by routing it to the appropriate handler.
    /// Returns 404 if route not found or 405 if method not allowed.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="modulePath">The route path relative to the module base.</param>
    /// <param name="method">The HTTP method of the request.</param>
    public virtual async Task Handle(HttpContext context, string modulePath, string method)
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

    /// <summary>
    /// Called when the module is loaded.
    /// Override to perform initialization tasks.
    /// </summary>
    public virtual async Task OnModuleLoad() { }

    /// <summary>
    /// Called when the module is unloaded.
    /// Override to perform cleanup tasks.
    /// </summary>
    public virtual async Task OnModuleUnLoad() { }
}
