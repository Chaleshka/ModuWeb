using Microsoft.AspNetCore.Http;
using ModuWeb;

namespace ModuWeb.examples
{
    /// <summary>
    /// Generated with AI for CorsMiddleware test
    /// </summary>
    public class CorsGuardDemoModule : ModuleBase
    {
        public override string ModuleName => "corsguard";
        public override string[] WithOriginsCors { get; } = new[] { "http://allowed.example", "http://localhost:3000" };
        public override string[] WithHeadersCors { get; } = new[] { "X-Test-Header" };
        public override bool BlockFailedCorsRequests { get; } = true;

        public override async Task OnModuleLoad()
        {
            Map("restricted", "GET", RestrictedHandler);
            Map("echo-headers", "GET", EchoHeaders);
            Map("info", "GET", Info);
            await base.OnModuleLoad();
        }

        private async Task Info(HttpContext ctx)
        {
            ctx.Response.StatusCode = 200;
            await ctx.Response.WriteAsJsonAsync(new { module = ModuleName, cors = new { origins = WithOriginsCors, headers = WithHeadersCors, block = BlockFailedCorsRequests } });
        }

        private async Task RestrictedHandler(HttpContext ctx)
        {
            ctx.Response.StatusCode = 200;
            await ctx.Response.WriteAsJsonAsync(new { ok = true, note = "Request reached handler — CORS passed" });
        }

        private async Task EchoHeaders(HttpContext ctx)
        {
            var headers = ctx.Request.Headers.ToDictionary(k => k.Key, v => v.Value.ToString());
            ctx.Response.StatusCode = 200;
            await ctx.Response.WriteAsJsonAsync(new { headers });
        }
    }
}
