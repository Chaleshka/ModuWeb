using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json.Serialization;

namespace ModuWeb;

internal class Program
{
    internal static void Main(string[] args)
    {
        var builder = WebApplication.CreateSlimBuilder(args);

        builder.Services.AddSingleton<ICorsPolicyProvider, DynamicCorsPolicyProvider>();
        builder.Services.AddCors();
        if (builder.Configuration.GetValue<bool>("UseHttps"))
            builder.WebHost.UseKestrelHttpsConfiguration();

        builder.WebHost.UseUrls(builder.Configuration["ApplicationUrl"]);

        var app = builder.Build();
        app.UseCors();

        var modulesPath = Path.Combine(builder.Environment.ContentRootPath, "modules");

        ModuleManager.Instance = new(modulesPath);

        Logger.Info($"Module base path: `{builder.Configuration["BaseApiPath"]}`");
        app.UseMiddleware<ModuleMiddleware>(builder.Configuration["BaseApiPath"]);

        app.Use(async (context, next) =>
        {
            await next();
            if (context.Response.StatusCode == 404)
            {
                context.Response.ContentType = "text/plain";
                await context.Response.WriteAsync("Module not found");
            }
        });

        app.Run();
    }
}
