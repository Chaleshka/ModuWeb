namespace ModuWeb;

/// <summary>
/// Stores mappings between URL paths, HTTP methods, and their corresponding request handlers.
/// </summary>
public sealed class RouteDictionary
{

    private readonly Dictionary<string, List<string>> _routeMethods = new();

    private readonly Dictionary<string, List<Func<HttpContext, Task>>> _routeHandlers = new();

    /// <summary>
    /// Adds a handler for a specific path and HTTP method.
    /// </summary>
    /// <param name="path">The relative route path (slashes are trimmed).</param>
    /// <param name="method">The HTTP method (e.g. GET, POST). Case-insensitive.</param>
    /// <param name="handler">The function to handle the request.</param>
    public void Add(string path, string method, Func<HttpContext, Task> handler)
    {
        if (handler == null)
            throw new ArgumentNullException("The handler must not have a null value.");

        path = path.Trim('/'); method = method.ToUpper();

        List<string> routeMethods;
        List<Func<HttpContext, Task>> routeHandlers;

        if (!ContainsPath(path))
        {
            routeMethods = new List<string>();
            routeHandlers = new List<Func<HttpContext, Task>>();

            _routeMethods.Add(path, routeMethods);
            _routeHandlers.Add(path, routeHandlers);
        }
        else
        {
            routeMethods = _routeMethods[path];
            routeHandlers = _routeHandlers[path];
        }

        routeMethods.Add(method);
        routeHandlers.Add(handler);
    }

    /// <summary>
    /// Checks if the specified path exists in the route dictionary.
    /// </summary>
    /// <param name="path">The relative path to check.</param>
    /// <returns><c>true</c> if the path exists; otherwise, <c>false</c>.</returns>
    public bool ContainsPath(string path)
    {
        path = path.Trim('/');

        return _routeHandlers.ContainsKey(path);
    }

    /// <summary>
    /// Checks whether a handler is registered for the given path and HTTP method.
    /// </summary>
    /// <param name="path">The relative path.</param>
    /// <param name="method">The HTTP method.</param>
    /// <returns><c>true</c> if a handler exists; otherwise, <c>false</c>.</returns>
    public bool ContainsMethod(string path, string method)
    {
        path = path.Trim('/'); method = method.ToUpper();

        if (!ContainsPath(path))
            return false;
        return _routeMethods[path].Contains(method);
    }

    /// <summary>
    /// Returns the handler associated with a given path and HTTP method.
    /// </summary>
    /// <param name="path">The relative path.</param>
    /// <param name="method">The HTTP method.</param>
    /// <returns>The request handler, or <c>null</c> if not found.</returns>
    public Func<HttpContext, Task> GetHandler(string path, string method)
    {
        path = path.Trim('/'); method = method.ToUpper();

        if (!_routeMethods.ContainsKey(path))
            return null;

        var indexOfHandler = _routeMethods[path].IndexOf(method);
        return _routeHandlers[path][indexOfHandler];
    }

    /// <summary>
    /// Returns a list of HTTP methods registered for a given path.
    /// </summary>
    /// <param name="path">The relative path.</param>
    /// <returns>An array of HTTP methods, or <c>null</c> if path not found.</returns>
    public string[] GetMethods(string path)
    {
        path = path.Trim('/');

        if (_routeMethods.ContainsKey(path))
            return _routeMethods[path].ToArray();
        return null;
    }

    /// <summary>
    /// Returns a list of handlers registered for a given path.
    /// </summary>
    /// <param name="path">The relative path.</param>
    /// <returns>An array of handlers, or <c>null</c> if path not found.</returns>
    public Func<HttpContext, Task>[] Gethandlers(string path)
    {
        path = path.Trim('/');

        if (_routeHandlers.ContainsKey(path))
            return _routeHandlers[path].ToArray();
        return null;
    }
}
