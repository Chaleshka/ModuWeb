namespace ModuWeb.Extentions
{
    public static class HttpRequestExtention
    {
        public async static Task<T?> GetRequestData<T>(this HttpRequest request, bool queryOnly = false) where T : new()
        {
            if (request.Method.ToUpper() == "GET" || queryOnly)
                return QueryParser.Parse<T?>(request.Query);
            else
                return await request.ReadFromJsonAsync<T?>();
        }
    }
}
