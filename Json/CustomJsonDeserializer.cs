using System.Reflection;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Collections.Generic;
using System.Collections;

namespace ModuWeb.Json
{
    public static class CustomJsonDeserializer
    {
        public static T? Deserialize<T>(string json)
        {
            var doc = JsonDocument.Parse(json);
            return (T?)DeserializeValue(typeof(T), doc.RootElement);
        }

        private static object? DeserializeValue(Type type, JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Null)
                return null;

            if (Nullable.GetUnderlyingType(type) is Type underlyingType)
            {
                return DeserializeValue(underlyingType, element);
            }

            if (type == typeof(string)) return element.GetString();
            if (type == typeof(int)) return element.GetInt32();
            if (type == typeof(bool)) return element.GetBoolean();
            if (type == typeof(double)) return element.GetDouble();
            if (type == typeof(float)) return element.GetSingle();
            if (type == typeof(long)) return element.GetInt64();
            if (type == typeof(short)) return element.GetInt16();

            if (type.IsArray)
            {
                var itemType = type.GetElementType()!;
                var list = new List<object?>();

                foreach (var item in element.EnumerateArray())
                    list.Add(DeserializeValue(itemType, item));

                var array = Array.CreateInstance(itemType, list.Count);
                for (int i = 0; i < list.Count; i++)
                    array.SetValue(list[i], i);

                return array;
            }

            if (typeof(IList).IsAssignableFrom(type))
            {
                var itemType = type.GetGenericArguments()[0];
                var list = (IList)Activator.CreateInstance(type)!;

                foreach (var item in element.EnumerateArray())
                    list.Add(DeserializeValue(itemType, item));

                return list;
            }

            var instance = Activator.CreateInstance(type);
            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!prop.CanWrite) continue;

                var jsonAttr = prop.GetCustomAttribute<JsonPropertyNameAttribute>();
                var jsonName = jsonAttr?.Name ?? prop.Name;

                if (!element.TryGetProperty(jsonName, out var propElement)) continue;

                var propValue = DeserializeValue(prop.PropertyType, propElement);
                prop.SetValue(instance, propValue);
            }

            return instance;
        }
    }
}
