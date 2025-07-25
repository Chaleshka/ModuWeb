using Microsoft.AspNetCore.Cors.Infrastructure;

namespace ModuWeb;

/// <summary>
/// Provides a dynamic CORS policy based on the current module associated with the request URL.
/// </summary>
internal class DynamicCorsPolicyProvider : ICorsPolicyProvider
{
    /// <summary>
    /// Gets the CORS policy for the given HTTP context and optional policy name.
    /// </summary>
    /// <param name="context">The HTTP context of the current request.</param>
    /// <param name="policyName">The name of the CORS policy (not used in this implementation).</param>
    /// <returns>A task that returns the <see cref="CorsPolicy"/> for the request, or null if no policy is found.</returns>
    public Task<CorsPolicy?> GetPolicyAsync(HttpContext context, string? policyName)
    {
        var module = ModuleMiddleware.Instance.GetModuleFromUrl(context.Request.Path);

        var policyBuilder = new CorsPolicyBuilder().AllowAnyMethod();

        if (module == null)
            policyBuilder
                .AllowAnyHeader()
                .AllowAnyOrigin();
        else
        {
            if (module.WithOriginsCors.Any())
                policyBuilder.WithOrigins(module.WithOriginsCors);
            else
                policyBuilder.AllowAnyOrigin();

            if (module.WithHeadersCors.Any())
                policyBuilder.WithHeaders(module.WithHeadersCors);
            else
                policyBuilder.AllowAnyHeader();
        }

        return Task.FromResult(policyBuilder.Build());
    }
}
