using ModuWeb;
using ModuWeb.ModuleMessenger;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using ModuWeb.Extensions;

namespace ModuWeb.examples
{
    /// <summary>
    /// Generated with AI for ApiModule test
    /// </summary>
    public class ApiTester : ModuleBase
    {
        public override string ModuleName => "apitester";

        private readonly HttpClient _httpClient;

        public ApiTester()
        {
            _httpClient = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
        }

        public override async Task OnModuleLoad()
        {
            Map("http_call", "POST", HttpCallHandler);
            Map("messenger_call", "POST", MessengerCallHandler);
            Map("compare", "POST", CompareHandler);
            Map("session_get", "GET", SessionGetHandler);
            Map("session_set", "POST", SessionSetHandler);
            await base.OnModuleLoad();
        }

        private async Task HttpCallHandler(HttpContext ctx)
        {
            var req = await ctx.Request.GetRequestData<Dictionary<string, object>>();
            ctx.Response.StatusCode = 400;

            if (req == null || !req.TryGetValue("action", out var actObj) || actObj is not string action)
                return;

            action = action.ToLowerInvariant();

            var basePrefix = ctx.Request.PathBase.HasValue ? ctx.Request.PathBase.Value : string.Empty;
            var url = $"{ctx.Request.Scheme}://{ctx.Request.Host}{basePrefix}/api/apimodule/{action}";

            var method = action == "set_data" ? HttpMethod.Put : HttpMethod.Post;
            var message = new HttpRequestMessage(method, url);

            if (req.TryGetValue("payload", out var payloadObj) && payloadObj is Dictionary<string, object> dict)
            {
                var payloadDict = dict.ToDictionary(kv => kv.Key, kv => kv.Value?.ToString() ?? "");
                var json = JsonSerializer.Serialize(payloadDict, JsonOptionExtension.Options);
                message.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            if (ctx.Request.Headers.TryGetValue("Cookie", out var cookieHeader))
                message.Headers.Add("Cookie", (string)cookieHeader);

            try
            {
                using var resp = await _httpClient.SendAsync(message);
                var content = resp.Content != null ? await resp.Content.ReadAsStringAsync() : string.Empty;
                ctx.Response.StatusCode = (int)resp.StatusCode;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.WriteAsync(string.IsNullOrEmpty(content)
                    ? JsonSerializer.Serialize(new { status = resp.StatusCode.ToString() }, JsonOptionExtension.Options)
                    : content);
            }
            catch (Exception ex)
            {
                ctx.Response.StatusCode = 500;
                await ctx.Response.WriteAsJsonAsync(new { error = ex.Message });
            }
        }

        private async Task MessengerCallHandler(HttpContext ctx)
        {
            var req = await ctx.Request.GetRequestData<Dictionary<string, object>>();
            ctx.Response.StatusCode = 400;

            if (req == null || !req.TryGetValue("action", out var actObj) || actObj is not string action)
                return;

            action = action.ToLowerInvariant();

            var payload = new Dictionary<string, object>();
            if (req.TryGetValue("payload", out var p) && p is Dictionary<string, object> pd)
            {
                foreach (var kv in pd)
                    payload[kv.Key] = kv.Value?.ToString() ?? "";
            }

            var sessionUser = await ctx.GetSessionAsync<string>("user");
            if (!string.IsNullOrEmpty(sessionUser))
                payload["session_user"] = sessionUser;

            var msg = new ModuleMessage($"api.{action}", ModuleName, payload);
            try
            {
                var resp = await ModuleMessenger.SendAndWaitAsync(msg, timeoutInS: 3);
                ctx.Response.StatusCode = 200;
                await ctx.Response.WriteAsJsonAsync(new { from = "messenger", data = resp.Data });
            }
            catch (TimeoutException)
            {
                ctx.Response.StatusCode = 504;
                await ctx.Response.WriteAsJsonAsync(new { error = "no response from api module" });
            }
            catch (Exception ex)
            {
                ctx.Response.StatusCode = 500;
                await ctx.Response.WriteAsJsonAsync(new { error = ex.Message });
            }
        }

        private async Task CompareHandler(HttpContext ctx)
        {
            var req = await ctx.Request.GetRequestData<Dictionary<string, object>>();
            ctx.Response.StatusCode = 400;

            if (req == null || !req.TryGetValue("action", out var actObj) || actObj is not string action || string.IsNullOrWhiteSpace(action))
                return;

            action = action.ToLowerInvariant();

            async Task<object> CallHttp()
            {
                var basePrefix = ctx.Request.PathBase.HasValue ? ctx.Request.PathBase.Value : string.Empty;
                var url = $"{ctx.Request.Scheme}://{ctx.Request.Host}{basePrefix}/api/apimodule/{action}";
                var method = action == "set_data" ? HttpMethod.Put : HttpMethod.Post;
                var message = new HttpRequestMessage(method, url);

                if (req.TryGetValue("payload", out var payloadObj) && payloadObj is Dictionary<string, object> dict)
                {
                    var payloadDict = dict.ToDictionary(kv => kv.Key, kv => kv.Value?.ToString() ?? "");
                    var json = JsonSerializer.Serialize(payloadDict, JsonOptionExtension.Options);
                    message.Content = new StringContent(json, Encoding.UTF8, "application/json");
                }

                if (ctx.Request.Headers.TryGetValue("Cookie", out var cookieHeader))
                    message.Headers.Add("Cookie", (string)cookieHeader);

                using var resp = await _httpClient.SendAsync(message);
                var content = resp.Content != null ? await resp.Content.ReadAsStringAsync() : string.Empty;
                return new { status = (int)resp.StatusCode, body = content };
            }

            async Task<object> CallMessenger()
            {
                var payload = new Dictionary<string, object>();
                if (req.TryGetValue("payload", out var p) && p is Dictionary<string, object> pd)
                {
                    foreach (var kv in pd)
                        payload[kv.Key] = kv.Value?.ToString() ?? "";
                }

                var sessionUser = await ctx.GetSessionAsync<string>("user");
                if (!string.IsNullOrEmpty(sessionUser))
                    payload["session_user"] = sessionUser;

                var msg = new ModuleMessage($"api.{action}", ModuleName, payload);
                try
                {
                    var resp = await ModuleMessenger.SendAndWaitAsync(msg, timeoutInS: 3);
                    return new { ok = true, data = resp.Data };
                }
                catch (Exception ex)
                {
                    return new { ok = false, error = ex.Message };
                }
            }

            var httpTask = CallHttp();
            var msgTask = CallMessenger();
            await Task.WhenAll(httpTask, msgTask);

            ctx.Response.StatusCode = 200;
            await ctx.Response.WriteAsJsonAsync(new { http = httpTask.Result, messenger = msgTask.Result });
        }


        private async Task SessionSetHandler(HttpContext ctx)
        {
            var req = await ctx.Request.GetRequestData<Dictionary<string, object>>();
            ctx.Response.StatusCode = 400;

            if (req == null || !req.TryGetValue("key", out var k) || k is not string key || string.IsNullOrWhiteSpace(key))
                return;

            req.TryGetValue("value", out var v);
            var val = v?.ToString() ?? "";

            await ctx.SetSessionAsync<object>(key, val);
            ctx.Response.StatusCode = 200;
            await ctx.Response.WriteAsJsonAsync(new { set = key });
        }

        private async Task SessionGetHandler(HttpContext ctx)
        {
            var key = ctx.Request.Query["key"].ToString();
            if (string.IsNullOrWhiteSpace(key))
            {
                ctx.Response.StatusCode = 400;
                return;
            }
            var val = await ctx.GetSessionAsync<object>(key);
            ctx.Response.StatusCode = 200;
            await ctx.Response.WriteAsJsonAsync(new { key, value = val });
        }
    }
}
