using System.Reflection;
using System.Text.Json.Serialization;
using System.Text;
using System.Collections.Generic;
using System.Collections;

namespace ModuWeb.Json
{
    public static class CustomJsonSerializer
    {
        public static string Serialize(object? obj)
        {
            return SerializeValue(obj);
        }

        private static string SerializeValue(object? value)
        {
            if (value == null)
                return "null";

            var type = value.GetType();

            if (value is string str)
                return $"\"{EscapeString(str)}\"";

            if (value is bool b)
                return b.ToString().ToLower();

            if (value is char c)
                return $"\"{c}\"";

            if (value is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal)
                return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)!;

            if (value is IEnumerable enumerable)
                return SerializeArray(enumerable);

            return SerializeObject(value, type);
        }

        private static string SerializeArray(IEnumerable array)
        {
            var sb = new StringBuilder();
            sb.Append("[");
            bool first = true;
            foreach (var item in array)
            {
                if (!first) sb.Append(",");
                sb.Append(SerializeValue(item));
                first = false;
            }
            sb.Append("]");
            return sb.ToString();
        }

        private static string SerializeObject(object obj, Type type)
        {
            var sb = new StringBuilder();
            sb.Append("{");

            var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            bool first = true;

            foreach (var prop in props)
            {
                if (!prop.CanRead) continue;

                var jsonPropAttr = prop.GetCustomAttribute<JsonPropertyNameAttribute>();
                var name = jsonPropAttr?.Name ?? prop.Name;

                var value = prop.GetValue(obj);

                if (!first) sb.Append(",");
                sb.Append($"\"{EscapeString(name)}\":{SerializeValue(value)}");
                first = false;
            }

            sb.Append("}");
            return sb.ToString();
        }

        private static string EscapeString(string str)
        {
            return str.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
        }
    }
}
