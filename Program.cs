using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http.Json;
using ModuWeb.SessionSystem;
using ModuWeb.Storage;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

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
        builder.Services.AddCors();
        if (builder.Configuration.GetValue<bool>("UseHttps"))
            builder.WebHost.UseKestrelHttpsConfiguration();

        builder.Services.Configure<JsonOptions>(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;

            options.SerializerOptions.TypeInfoResolver = JsonTypeInfoResolver.Combine(
                options.SerializerOptions.TypeInfoResolver,
                new DefaultJsonTypeInfoResolver()
            );
        });

        var app = builder.Build();
        app.UseCors(); 

        var modulesPath = Path.Combine(builder.Environment.ContentRootPath, "modules");

        ModuleManager.Instance = new(modulesPath, 
            builder.Configuration.GetSection("LoadOrder").Get<string[]>() ?? Array.Empty<string>());

        Logger.Info($"Module base path: `{builder.Configuration["BaseApiPath"]}`");
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
