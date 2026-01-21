namespace ModuWeb.Cors
{
    /// <summary>
    /// Common HTTP headers for modules and middleware.
    /// </summary>
    public static class Headers
    {
        public const string Accept = "Accept";
        public const string AcceptEncoding = "Accept-Encoding";
        public const string Authorization = "Authorization";
        public const string ContentLength = "Content-Length";
        public const string ContentType = "Content-Type";
        public const string Cookie = "Cookie";
        public const string Host = "Host";
        public const string UserAgent = "User-Agent";
        public const string Origin = "Origin";
        public const string Referer = "Referer";

        // Response headers
        public const string SetCookie = "Set-Cookie";
        public const string Location = "Location";
        public const string Server = "Server";
        public const string ContentEncoding = "Content-Encoding";
        public const string ContentLanguage = "Content-Language";
        public const string ContentDisposition = "Content-Disposition";

        // CORS headers
        public const string AccessControlAllowOrigin = "Access-Control-Allow-Origin";
        public const string AccessControlAllowMethods = "Access-Control-Allow-Methods";
        public const string AccessControlAllowHeaders = "Access-Control-Allow-Headers";
        public const string AccessControlExposeHeaders = "Access-Control-Expose-Headers";
        public const string AccessControlRequestMethod = "Access-Control-Request-Method";
        public const string AccessControlRequestHeaders = "Access-Control-Request-Headers";
    }
}
