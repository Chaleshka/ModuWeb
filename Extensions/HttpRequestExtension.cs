using System.Text.Json;

namespace ModuWeb.Extensions
{
    public static class HttpRequestExtension
    {
        /// <summary>
        /// Унифицированный метод для чтения тела запроса.
        /// Для GET-запросов парсит query, для остальных — тело JSON.
        /// Автоматически преобразует JsonElement в реальные CLR-типы.
        /// </summary>
        public async static Task<T?> GetRequestData<T>(this HttpRequest request, bool queryOnly = false) where T : new()
        {
            if (request.Method.ToUpper() == "GET" || queryOnly)
                return QueryParser.Parse<T?>(request.Query);

            var result = await request.ReadFromJsonAsync<T?>();

            if (result is Dictionary<string, object> dict)
                return (T)(object)ConvertJsonElements(dict);

            return result;
        }

        /// <summary>
        /// Рекурсивно преобразует JsonElement в реальные CLR-типы (string, long, bool, Dictionary, List).
        /// </summary>
        private static object ConvertJsonElements(object value)
        {
            if (value is JsonElement je)
            {
                switch (je.ValueKind)
                {
                    case JsonValueKind.String:
                        return je.GetString()!;
                    case JsonValueKind.Number:
                        if (je.TryGetInt64(out var l)) return l;
                        if (je.TryGetDouble(out var d)) return d;
                        return je.GetDecimal();
                    case JsonValueKind.True:
                        return true;
                    case JsonValueKind.False:
                        return false;
                    case JsonValueKind.Null:
                        return null!;
                    case JsonValueKind.Object:
                    {
                        var dict = new Dictionary<string, object>();
                        foreach (var prop in je.EnumerateObject())
                            dict[prop.Name] = ConvertJsonElements(prop.Value);
                        return dict;
                    }
                    case JsonValueKind.Array:
                    {
                        var list = new List<object>();
                        foreach (var el in je.EnumerateArray())
                            list.Add(ConvertJsonElements(el));
                        return list;
                    }
                    default:
                        return je.ToString()!;
                }
            }


            {
                if (value is Dictionary<string, object> d)
                {
                    var newDict = new Dictionary<string, object>();
                    foreach (var kv in d)
                        newDict[kv.Key] = ConvertJsonElements(kv.Value);
                    return newDict;
                }
                if (value is IEnumerable<object> list)
                {
                    var newList = new List<object>();
                    foreach (var el in list)
                        newList.Add(ConvertJsonElements(el));
                    return newList;
                }
            }
            return value;
        }
    }
}
