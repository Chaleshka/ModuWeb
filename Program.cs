using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using ModuWeb.Cors;
using ModuWeb.Extensions;
using ModuWeb.SessionSystem;
using ModuWeb.Storage;
using ModuWeb.ViewEngine;

namespace ModuWeb;

internal class Program
{
    internal static void Main(string[] args)
    {
        var builder = WebApplication.CreateSlimBuilder(args);

        builder.Services.AddSingleton<ICorsPolicyProvider, DynamicCorsPolicyProvider>();
        builder.Services.AddSingleton<IStorageService>(provider =>
        {
            var dbPath = Path.Combine(builder.Environment.ContentRootPath, builder.Configuration["BaseDbPath"],
                "storage.db");
            return new LiteDbStorageService(dbPath);
        });
        builder.Services.AddSingleton<ISessionService, LiteDbSessionService>();
        builder.Services.AddSingleton<IModuleViewEngine, ModuleViewEngine>();
        builder.Services.AddCors();
        if (builder.Configuration.GetValue<bool>("UseHttps"))
            builder.WebHost.UseKestrelHttpsConfiguration();

        builder.Services.Configure<JsonOptions>(options => options.JsonSerializerOptions());

        builder.Services.Configure<FormOptions>(options =>
        {
            options.MultipartBodyLengthLimit = builder.Configuration.GetValue<int>("MaxRequestBodySize") * 1024 * 1024;
        });
        builder.Services.Configure<KestrelServerOptions>(options =>
        {
            options.Limits.MaxRequestBodySize = builder.Configuration.GetValue<int>("MaxRequestBodySize") * 1024 * 1024;
        });

        var app = builder.Build();
        app.UseCors(); 

        var modulesPath = Path.Combine(builder.Environment.ContentRootPath, "modules");

        ModuleManager.Instance = new(modulesPath,
            builder.Configuration.GetSection("LoadOrder").Get<string[]>() ?? Array.Empty<string>(), app.Services);

        Logger.Info($"Module base path: `{builder.Configuration["BaseApiPath"]}`");
        app.UseStaticFiles();
        app.UseMiddleware<ModuleCorsGuardMiddleware>();
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
