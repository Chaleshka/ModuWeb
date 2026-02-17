using ModuWeb.Extensions;

namespace ModuWeb;

/// <summary>
/// Middleware responsible for routing HTTP requests to dynamically loaded modules.
/// </summary>
public class ModuleMiddleware
{
    private static ModuleMiddleware _instance;
    public static ModuleMiddleware Instance
    {
        get => _instance ?? throw new InvalidOperationException("ModuleMiddleware is not initialized.");
        set
        {
            if (_instance == null)
                _instance = value;
        }
    }
    private readonly RequestDelegate _next;
    internal ModuleManager moduleManager => ModuleManager.Instance;
    private readonly string _basePath;

    /// <summary>
    /// Initializes the middleware with the next delegate and the base path.
    /// </summary>
    /// <param name="next">The next middleware delegate in the pipeline.</param>
    /// <param name="basePath">Base URL path prefix for module routing.</param>
    public ModuleMiddleware(RequestDelegate next, string basePath)
    {
        _next = next;
        _basePath = basePath == "/" ? "" : basePath;
        if (_basePath.Length > 0 && !_basePath.EndsWith("/"))
            _basePath += "/";

        Instance = this;
    }

    /// <summary>
    /// Returns the module based on the request URL and outputs the remaining path.
    /// </summary>
    /// <param name="url">Incoming request path.</param>
    /// <param name="modulePath">Remaining path after the module name.</param>
    /// <returns>A <see cref="ModuleBase"/>, or <c>null</c> if module not found.</returns>
    public ModuleBase? GetModuleFromUrl(string? url, out string modulePath)
    {
        modulePath = "";
        if (string.IsNullOrEmpty(url) || url == "/")
            return moduleManager.GetModule("index");

        url = url?.Trim('/');

        if (url.StartsWith("index"))
            return moduleManager.GetModule("index");
        if (!url.StartsWith(_basePath))
            return null;

        var modulePathString = url;
        if (!string.IsNullOrEmpty(_basePath))
            modulePathString = url.Replace(_basePath, "", 1);

        var modulePathElements = modulePathString.Split('/');
        var moduleName = modulePathElements[0];
        modulePath = string.Join("/", modulePathElements.Skip(1));

        return moduleManager.GetModule(moduleName);
    }

    /// <summary>
    /// Gets the module from the URL without returning the remaining path.
    /// </summary>
    public ModuleBase? GetModuleFromUrl(string? url) => GetModuleFromUrl(url, out _);

    /// <summary>
    /// Handles the incoming HTTP request and delegates it to the matched module.
    /// </summary>
    /// <param name="context">HTTP context of the request.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            var path = context.Request.Path;
            var remote = context.Connection.RemoteIpAddress?.ToString() ?? "?";
            var port = context.Connection.RemotePort;
            Logger.Info($"Request {context.Request.Method.ToUpper()} {path} from {remote}:{port}");

            var module = GetModuleFromUrl(path, out var modulePath);
            if (module != null)
            {
                if (modulePath.Length == 0 && path.HasValue && !path.Value!.EndsWith('/'))
                {
                    var redirect = path.Value + "/" + context.Request.QueryString;
                    context.Response.Redirect(redirect, permanent: false);
                    return;
                }
                await module.Handle(context, modulePath, context.Request.Method.ToUpper());
                return;
            }

            await _next(context);
        }
        catch (Exception ex)
        {
            Logger.Error($"Error processing request: {ex}");
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = 500;
                await context.Response.WriteAsync("Internal server error");
            }
        }
    }
}
