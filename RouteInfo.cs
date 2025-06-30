
namespace ModuWeb;

/// <summary>
/// Represents an HTTP route with a specific method and path.
/// Used to register and identify endpoints within modules.
/// </summary>
public class RouteInfo
{
    /// <summary>
    /// The HTTP method of the route (e.g., GET, POST).
    /// Always stored in uppercase.
    /// </summary>
    public readonly string Method;

    /// <summary>
    /// The relative URL path of the route (e.g., "users/info").
    /// Leading and trailing slashes are removed.
    /// </summary>
    public readonly string Path;

    /// <summary>
    /// Initializes a new instance of the <see cref="RouteInfo"/> class
    /// with the specified HTTP method and route path.
    /// </summary>
    /// <param name="method">The HTTP method (e.g., GET, POST).</param>
    /// <param name="path">The URL path relative to the base module path.</param>
    public RouteInfo(string method, string path)
    {
        Method = method.ToUpper();
        Path = path.Trim('/');
    }

    public override bool Equals(object? obj) => 
        obj is RouteInfo data && Method == data.Method &&Path == data.Path;

    public override int GetHashCode() =>
        HashCode.Combine(Method, Path);
}
