using System.Reflection;
using System.Text.Json.Serialization;

namespace ModuWeb
{
    public class QueryParser
    {
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
                        object? converted = Convert.ChangeType(value.ToString(), prop.PropertyType);
                        prop.SetValue(obj, converted);
                    }
                    catch(Exception ex)
                    {
                        Logger.Error(ex);
                    }
                }
            }

            return obj;
        }
    }
}
