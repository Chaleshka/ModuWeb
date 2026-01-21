using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.Http.Json;

namespace ModuWeb.Extensions
{
    public static class JsonOptionExtension
    {
        public static JsonOptions JsonSerializerOptions(this JsonOptions options)
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;

            options.SerializerOptions.TypeInfoResolver = JsonTypeInfoResolver.Combine(
                options.SerializerOptions.TypeInfoResolver,
                new DefaultJsonTypeInfoResolver()
            );
            return options;
        }
        public static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        };
    }
}