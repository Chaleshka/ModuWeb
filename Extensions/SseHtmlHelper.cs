using System.Text;
using RazorLight.Text;

namespace ModuWeb.Extensions;

/// <summary>
/// Fluent builder for generating SSE (Server-Sent Events) client-side JavaScript in Razor views.<br/>
/// Usage in .cshtml: <c>@(Sse.Stream("time-stream").Bind("#serverTime", "time").Render())</c>
/// </summary>
public static class Sse
{
    /// <summary>
    /// Creates an SSE stream builder for the given endpoint URL.
    /// </summary>
    /// <param name="url">Relative or absolute URL of the SSE endpoint.</param>
    public static SseBuilder Stream(string url) => new(url);
}

public class SseBuilder
{
    private readonly string _url;
    private readonly List<SseBinding> _bindings = new();
    private readonly List<SseEventGroup> _namedEvents = new();
    private string? _onError;
    private string? _onOpen;
    private string? _rawOnMessage;

    internal SseBuilder(string url)
    {
        _url = url;
    }

    /// <summary>
    /// Binds an SSE JSON field to a DOM element's text content (for default <c>onmessage</c> events).
    /// </summary>
    /// <param name="elementId">CSS selector: element ID with or without '#' (e.g. "serverTime" or "#serverTime").</param>
    /// <param name="jsonField">JSON field name from the SSE data (e.g. "time").</param>
    /// <param name="format">Optional format string. Use <c>{0}</c> as placeholder (e.g. "Updated: {0}").</param>
    public SseBuilder Bind(string elementId, string jsonField, string? format = null)
    {
        _bindings.Add(new SseBinding(NormalizeId(elementId), jsonField, format));
        return this;
    }

    /// <summary>
    /// Adds bindings for a named SSE event (server sends <c>event: name</c>).
    /// </summary>
    /// <param name="eventName">The SSE event name.</param>
    /// <param name="configure">Action to add bindings for this event.</param>
    public SseBuilder On(string eventName, Action<SseEventBindingBuilder> configure)
    {
        var builder = new SseEventBindingBuilder();
        configure(builder);
        _namedEvents.Add(new SseEventGroup(eventName, builder.Bindings));
        return this;
    }

    /// <summary>
    /// Sets a raw JavaScript expression to execute on each message (receives parsed <c>data</c> object).
    /// </summary>
    /// <param name="jsExpression">JS code with access to <c>data</c> variable, e.g. <c>"console.log(data)"</c>.</param>
    public SseBuilder OnMessage(string jsExpression)
    {
        _rawOnMessage = jsExpression;
        return this;
    }

    /// <summary>
    /// Sets a raw JavaScript expression to execute on connection error.
    /// </summary>
    public SseBuilder OnError(string jsExpression)
    {
        _onError = jsExpression;
        return this;
    }

    /// <summary>
    /// Sets a raw JavaScript expression to execute when the connection opens.
    /// </summary>
    public SseBuilder OnOpen(string jsExpression)
    {
        _onOpen = jsExpression;
        return this;
    }

    /// <summary>
    /// Generates the <c>&lt;script&gt;</c> tag with EventSource setup as raw (unescaped) HTML.<br/>
    /// Use in .cshtml: <c>@(Sse.Stream(...).Bind(...).Render())</c>
    /// </summary>
    public IRawString Render()
    {
        var sb = new StringBuilder();
        sb.AppendLine("<script>");
        sb.AppendLine("(function(){");
        sb.AppendLine($"var _sse=new EventSource('{EscapeJs(_url)}');");

        if (_bindings.Count > 0 || _rawOnMessage != null)
        {
            sb.AppendLine("_sse.onmessage=function(e){var data=JSON.parse(e.data);");
            AppendBindings(sb, _bindings);
            if (_rawOnMessage != null)
                sb.AppendLine(_rawOnMessage + ";");
            sb.AppendLine("};");
        }

        foreach (var group in _namedEvents)
        {
            sb.AppendLine($"_sse.addEventListener('{EscapeJs(group.EventName)}',function(e){{var data=JSON.parse(e.data);");
            AppendBindings(sb, group.Bindings);
            sb.AppendLine("});");
        }

        if (_onOpen != null)
            sb.AppendLine($"_sse.onopen=function(){{{_onOpen};}};");

        if (_onError != null)
            sb.AppendLine($"_sse.onerror=function(){{{_onError};}};");

        sb.AppendLine("})();");
        sb.AppendLine("</script>");
        return new RawString(sb.ToString());
    }

    public override string ToString() => Render().ToString()!;

    private static void AppendBindings(StringBuilder sb, List<SseBinding> bindings)
    {
        foreach (var b in bindings)
        {
            var value = b.Format != null
                ? $"'{EscapeJs(b.Format)}'.replace('{{0}}',data.{b.JsonField})"
                : $"data.{b.JsonField}";
            sb.AppendLine($"document.getElementById('{b.ElementId}').textContent={value};");
        }
    }

    private static string NormalizeId(string id) => id.TrimStart('#');

    private static string EscapeJs(string s) => s.Replace("\\", "\\\\").Replace("'", "\\'");

    internal record SseBinding(string ElementId, string JsonField, string? Format);
    internal record SseEventGroup(string EventName, List<SseBinding> Bindings);
}

public class SseEventBindingBuilder
{
    internal readonly List<SseBuilder.SseBinding> Bindings = new();

    /// <summary>
    /// Binds an SSE JSON field to a DOM element for this named event.
    /// </summary>
    public SseEventBindingBuilder Bind(string elementId, string jsonField, string? format = null)
    {
        Bindings.Add(new SseBuilder.SseBinding(elementId.TrimStart('#'), jsonField, format));
        return this;
    }
}
