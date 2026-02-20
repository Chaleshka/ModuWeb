using Microsoft.AspNetCore.Http;
using ModuWeb;
using ModuWeb.Extensions;
using ModuWeb.ViewEngine;

namespace testPlugin;

public class TimeServerSseModule : ModuleBase
{
    public override string ModuleName => "timeserversse";

    public override async Task OnModuleLoad()
    {
        Map("/", "GET", PageHandler);
        Map("time-stream", "GET", TimeStreamHandler);
        await base.OnModuleLoad();
    }

    public override void RegisterViews(IModuleViewEngine viewEngine)
    {
        viewEngine.RegisterModuleViews(ModuleName, GetType().Assembly);
    }

    private async Task PageHandler(HttpContext context)
    {
        var model = new
        {
            Title = "Время на сервере (SSE)",
            InitialTime = DateTime.Now.ToString("HH:mm:ss"),
            InitialDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };

        await context.Response.WriteRazorPageAsync("Views/Index.cshtml", model);
    }

    private async Task TimeStreamHandler(HttpContext context)
    {
        await context.Response.WriteSseAsync(() => new
        {
            time = DateTime.Now.ToString("HH:mm:ss"),
            date = DateTime.Now.ToString("yyyy-MM-dd"),
            datetime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            timezone = TimeZoneInfo.Local.DisplayName
        }, intervalMs: 1000);
    }
}