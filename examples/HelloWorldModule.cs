using Microsoft.AspNetCore.Http;
using ModuWeb;

namespace ModuWeb.examples
{
    public class HelloWorldModule : ModuleBase
    {
        public override async Task OnModuleLoad()
        {
            Map("hello", "GET", HelloWorldHandler);
        }

        public async Task HelloWorldHandler(HttpContext context)
        {
            context.Response.StatusCode = 200;
            await context.Response.WriteAsync("Hello World!");
        }
    }
}
