using System.Reflection;
using System.Text.Json.Serialization;

namespace ModuWeb
{
    public class QueryParser
    {
        /// <summary>
        /// Parsring query into T object;
        /// </summary>
        /// <param name="query">Query of the request</param>
        public static T? Parse<T>(IQueryCollection query) where T : new()
        {
            var obj = new T();
            var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var prop in props)
            {
                string name = prop.Name;
                var attr = prop.GetCustomAttribute<JsonPropertyNameAttribute>();
                if (attr != null)
                    name = attr.Name;

                if (query.TryGetValue(name, out var value))
                {
                    try
                    {
                        object? converted = null;
                        var type = prop.PropertyType;

                        var underlyingType = Nullable.GetUnderlyingType(type);

                        if (underlyingType != null)
                        {
                            if (string.IsNullOrWhiteSpace(value.ToString()))
                                converted = null;
                            else
                                converted = Convert.ChangeType(value.ToString(), underlyingType);
                        }
                        else
                        {
                            converted = Convert.ChangeType(value.ToString(), type);
                        }

                        prop.SetValue(obj, converted);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex);
                    }
                }
            }

            return obj;
        }
    }
}
