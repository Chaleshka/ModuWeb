using Microsoft.AspNetCore.Http;
using ModuWeb;
using ModuWeb.Extensions;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace ModuWeb.examples
{
    public class NameStorageModule : ModuleBase
    {
        public class NameData
        {
            [JsonPropertyName("name")]
            public string? Name { get; set; }
        }
        public class NamesData
        {
            public string[] names { get; set; }
        }
        public record NameIdData
        {
            public int? name_id { get; set; }
        }
        static Dictionary<int, string> storage = new();
        public override async Task OnModuleLoad()
        {
            Map("name", "PUT", PutNameHandler);
            Map("name", "GET", GetNameHandler);
            Map("name", "DELETE", DeleteNameHandler);
            Map("names", "CHECK", CheckNameHandler); //Custom method
        }

        private async Task PutNameHandler(HttpContext context)
        {
            var data = await context.Request.GetRequestData<NameData>();

            context.Response.StatusCode = 400;
            if (data == null || string.IsNullOrEmpty(data?.Name))
                return;

            context.Response.StatusCode = 200;
            var id = Random.Shared.Next();
            while (storage.ContainsKey(id))
                id = Random.Shared.Next();
            storage[id] = data.Name;

            await context.Response.WriteAsJsonAsync(
                new NameIdData() { name_id = id });
        }

        private async Task GetNameHandler(HttpContext context)
        {
            var data = await context.Request.GetRequestData<NameIdData>();

            context.Response.StatusCode = 400;
            if (data == null || data.name_id == null || !storage.ContainsKey(data.name_id.Value))
                return;

            context.Response.StatusCode = 200;
            await context.Response.WriteAsJsonAsync(
                new NameData() { Name = storage[data.name_id.Value] });
        }

        private async Task DeleteNameHandler(HttpContext context)
        {
            var data = await context.Request.GetRequestData<NameIdData>();

            context.Response.StatusCode = 400;
            if (data == null || data.name_id == null || !storage.ContainsKey(data.name_id.Value))
                return;

            context.Response.StatusCode = 200;
            storage.Remove(data.name_id.Value);
        }

        private async Task CheckNameHandler(HttpContext context)
        {
            await context.Response.WriteAsJsonAsync(
                new NamesData() { names = storage.Values.ToArray() });
        }

    }
}
