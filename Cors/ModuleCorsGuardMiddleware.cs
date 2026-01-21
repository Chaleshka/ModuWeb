using ModuWeb.Events;
using ModuWeb.Extensions;

namespace ModuWeb.Cors
{
    public class ModuleCorsGuardMiddleware
    {
        private readonly RequestDelegate _next;

        public ModuleCorsGuardMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var origin = context.Request.Headers["Origin"].FirstOrDefault();
            var requestHeaders = context.Request.Headers["Access-Control-Request-Headers"]
                .ToString()
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var module = ModuleMiddleware.Instance.GetModuleFromUrl(context.Request.Path);
            bool passed = true;
            bool originsPassed = true;
            bool headersPassed = true;

            if (module != null && module.BlockFailedCorsRequests)
            {
                if (module.WithOriginsCors.Any())
                {
                    if (string.IsNullOrEmpty(origin) || !module.WithOriginsCors.Contains(origin, StringComparer.OrdinalIgnoreCase))
                    {
                        context.Response.StatusCode = 403;
                        await context.Response.WriteAsync("Origin not allowed.");
                        passed = false;
                        originsPassed = false;
                    }
                }

                if (module.WithHeadersCors.Any() && requestHeaders.Length > 0)
                {
                    foreach (var h in requestHeaders)
                    {
                        if (!module.WithHeadersCors.Contains(h, StringComparer.OrdinalIgnoreCase))
                        {
                            if(context.Response.StatusCode != 403)
                                context.Response.StatusCode = 403;

                            await context.Response.WriteAsync($"Header '{h}' not allowed.");
                            passed = false;
                            headersPassed = false;
                        }
                    }
                }
            }

            Events.Events.RequestReceivedSafeEvent.Invoke(new RequestReceivedEventArgs(context, module, passed, originsPassed, headersPassed));
            if (passed)
                await _next(context);
        }
    }
}
