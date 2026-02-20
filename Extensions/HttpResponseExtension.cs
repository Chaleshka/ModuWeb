using System.Text.Json;
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

    /// <summary>
    /// Starts an SSE (Server-Sent Events) stream. Calls <paramref name="generator"/> in a loop
    /// until the client disconnects. Each returned object is serialized to JSON and sent as an SSE message.<br/>
    /// Return <c>null</c> from <paramref name="generator"/> to skip sending an event for that tick.
    /// </summary>
    /// <param name="response">The HTTP response.</param>
    /// <param name="generator">Async function called each tick. Receives <see cref="CancellationToken"/>; return data object or null to skip.</param>
    /// <param name="intervalMs">Delay between ticks in milliseconds (default 1000).</param>
    /// <param name="eventName">Optional SSE event name (the <c>event:</c> field). If null, browser receives it via <c>onmessage</c>.</param>
    public static async Task WriteSseAsync(
        this HttpResponse response,
        Func<CancellationToken, Task<object?>> generator,
        int intervalMs = 1000,
        string? eventName = null)
    {
        response.ContentType = "text/event-stream";
        response.Headers["Cache-Control"] = "no-cache";
        response.Headers["Connection"] = "keep-alive";

        var ct = response.HttpContext.RequestAborted;

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var data = await generator(ct);

                if (data != null)
                {
                    var json = JsonSerializer.Serialize(data, JsonOptionExtension.Options);

                    if (eventName != null)
                        await response.WriteAsync($"event: {eventName}\n", ct);

                    await response.WriteAsync($"data: {json}\n\n", ct);
                    await response.Body.FlushAsync(ct);
                }

                await Task.Delay(intervalMs, ct);
            }
        }
        catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Simplified SSE overload: sends the same <paramref name="generator"/> result on a fixed interval.
    /// The generator has no parameters — use when the data doesn't depend on cancellation token.
    /// </summary>
    public static Task WriteSseAsync(
        this HttpResponse response,
        Func<object?> generator,
        int intervalMs = 1000,
        string? eventName = null)
    {
        return response.WriteSseAsync(
            _ => Task.FromResult(generator()),
            intervalMs,
            eventName);
    }
}
