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
        _basePath = basePath?.Trim('/') ?? string.Empty;

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

        var normalizedUrl = url.Trim('/');
        var modulePathString = normalizedUrl;
        if (!string.IsNullOrEmpty(_basePath))
        {
            if (normalizedUrl.Equals(_basePath, StringComparison.OrdinalIgnoreCase))
                return null;

            var basePathPrefix = _basePath + "/";
            if (!normalizedUrl.StartsWith(basePathPrefix, StringComparison.OrdinalIgnoreCase))
                return null;

            modulePathString = normalizedUrl[basePathPrefix.Length..];
        }

        if (string.IsNullOrEmpty(modulePathString))
            return null;

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
            var port = context.Connection.RemotePort.ToString();

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BEHIND_PROXY")))
            {
                remote = context.Request.Headers[Environment.GetEnvironmentVariable("CLIENT_IP_HEADER") ?? "X-Forwarded-For"].ToString();
                port = "?";
            }

            Logger.Info($"Request {context.Request.Method.ToUpper()} {path} from {remote}:{port}");

            var module = GetModuleFromUrl(path, out var modulePath);
            if (module != null)
            {
                using var requestLease = moduleManager.TryAcquireRequestLease(module);
                if (requestLease is null)
                {
                    context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                    return;
                }

                context.Items["ModuWeb.CurrentModule"] = module;

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
