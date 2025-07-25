using ModuWeb.Json;
using System.Net.NetworkInformation;
using static ModuWeb.examples.WhatsYourNameModule;

namespace ModuWeb.Extentions
{
    public static class HttpRequestExtention
    {
        public async static Task<T?> GetRequestData<T>(this HttpRequest request) where T : new()
        {
            if (request.Method.ToUpper() == "GET")
                return QueryParser.Parse<T?>(request.Query);
            else
            {
                using var reader = new StreamReader(request.Body);
                string rawJson = await reader.ReadToEndAsync();
                return CustomJsonDeserializer.Deserialize<T?>(rawJson);
            }
        }
    }
}
