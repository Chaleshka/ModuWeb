using ModuWeb.SessionSystem;

namespace ModuWeb.Extentions
{
    public static class SessionExtensions
    {
        public static async Task<T> GetSessionAsync<T>(this HttpContext context, string key)
        {
            var sessionId = GetSessionId(context);
            var sessionService = context.RequestServices.GetRequiredService<ISessionService>();
            return await sessionService.GetAsync<T>(sessionId, key);
        }

        public static async Task SetSessionAsync<T>(this HttpContext context, string key, T value)
        {
            var sessionId = GetSessionId(context);
            var sessionService = context.RequestServices.GetRequiredService<ISessionService>();
            await sessionService.SetAsync(sessionId, key, value);
        }

        public static async Task RemoveSessionAsync(this HttpContext context, string key)
        {
            var sessionId = GetSessionId(context);
            var sessionService = context.RequestServices.GetRequiredService<ISessionService>();
            await sessionService.RemoveAsync(sessionId, key);
        }

        private static string GetSessionId(HttpContext context)
        {
            if (context.Request.Cookies.TryGetValue("Session", out var sessionId))
                return sessionId;

            sessionId = Guid.NewGuid().ToString();
            context.Response.Cookies.Append("Session", sessionId, new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Strict,
                Secure = context.Request.IsHttps
            });
            return sessionId;
        }
    }

}
