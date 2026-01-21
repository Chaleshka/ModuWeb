using Microsoft.AspNetCore.Http;
using ModuWeb;
using ModuWeb.Extensions;
using ModuWeb.ModuleMessenger;
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace ModuWeb.examples
{
    public class ApiModule : ModuleBase
    {
        public override string ModuleName => "api";
        private static readonly ConcurrentDictionary<string, object> SimpleDataBase = new();
        private const string SecretString = ")(87yhGt56&Yhji(87^YTghIo9y*T&^rfyUKvyitiufr7";
        private readonly string _letters = "";


        public ApiModule()
        {
            for (char c = 'A'; c <= 'Z'; c++)
                _letters += c;
            for (char c = 'a'; c <= 'z'; c++)
                _letters += c;
            _letters += "1234567890_-$#&";
        }

        public override Task OnModuleLoad()
        {
            ModuleMessenger.Subscribe(MessageHandler);

            Map("login", "POST", LoginHandler);
            Map("register", "POST", RegisterHandler);
            Map("get_data", "HEAD", GetDataHandler);
            Map("set_data", "PUT", SetDataHandler);
            return base.OnModuleLoad();
        }

        private async Task LoginHandler(HttpContext ctx)
        {
            Dictionary<string, string> res = new();
            var data = await ctx.Request.GetRequestData<Dictionary<string, object>>();

            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            if (!(data.TryGetValue("login", out var login) && login is string))
            {
                res["error"] = "In data must be string key 'login'";
                await ctx.Response.WriteAsJsonAsync(res);
                return;
            }

            if (!(data.TryGetValue("password", out var password) && password is string))
            {
                res["error"] = "In data must be string key 'password'";
                await ctx.Response.WriteAsJsonAsync(res);
                return;
            }

            ctx.Response.StatusCode = StatusCodes.Status200OK;
            await ctx.Response.WriteAsJsonAsync(Login((string)login, (string)password));
        }

        private async Task RegisterHandler(HttpContext ctx)
        {
            Dictionary<string, string> res = new();
            var data = await ctx.Request.GetRequestData<Dictionary<string, object>>();

            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            if (!(data.TryGetValue("login", out var login) && login is string))
            {
                res["error"] = "In data must be string key 'login'";
                await ctx.Response.WriteAsJsonAsync(res);
                return;
            }

            if (!(data.TryGetValue("password", out var password) && password is string))
            {
                res["error"] = "In data must be string key 'password'";
                await ctx.Response.WriteAsJsonAsync(res);
                return;
            }

            ctx.Response.StatusCode = StatusCodes.Status200OK;
            await ctx.Response.WriteAsJsonAsync(Register((string)login, (string)password));
        }

        private async Task GetDataHandler(HttpContext ctx)
        {
            Dictionary<string, string> res = new();
            var data = await ctx.Request.GetRequestData<Dictionary<string, object>>();

            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            if (data.TryGetValue("token", out var token))
            {
                if (token is not string || !(data.TryGetValue("data", out var dataRequest) && dataRequest is string))
                {
                    res["error"] = "In data must be string key 'token' or 'login' + 'password' and string key 'data'";
                    await ctx.Response.WriteAsJsonAsync(res);
                    return;
                }

                ctx.Response.StatusCode = StatusCodes.Status200OK;
                await ctx.Response.WriteAsJsonAsync(GetData((string)token, (string)dataRequest));
                return;
            }

            if (!(data.TryGetValue("login", out var login) && login is string))
            {
                res["error"] = "In data must be string key 'token' or 'login' + 'password' and string key 'data'";
                await ctx.Response.WriteAsJsonAsync(res);
                return;
            }

            if (!(data.TryGetValue("password", out var password) && password is string))
            {
                res["error"] = "In data must be string key 'token' or 'login' + 'password' and string key 'data'";
                await ctx.Response.WriteAsJsonAsync(res);
                return;
            }

            {
                if (!(data.TryGetValue("data", out var dataRequest) && dataRequest is string))
                {
                    res["error"] = "In data must be string key 'token' or 'login' + 'password' and string key 'data'";
                    await ctx.Response.WriteAsJsonAsync(res);
                    return;
                }

                ctx.Response.StatusCode = StatusCodes.Status200OK;
                await ctx.Response.WriteAsJsonAsync(GetData((string)login, (string)password, (string)dataRequest));
            }
        }

        private async Task SetDataHandler(HttpContext ctx)
        {
            Dictionary<string, string> res = new();
            var data = await ctx.Request.GetRequestData<Dictionary<string, object>>();

            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            if (data.TryGetValue("token", out var token))
            {
                if (token is not string)
                {
                    res["error"] = "In data must be string key 'token' or 'login' + 'password' " +
                                   "and string key 'data_key' + object key 'data_value'";
                    await ctx.Response.WriteAsJsonAsync(res);
                    return;
                }

                if (!(data.TryGetValue("data_key", out var data_key) && data_key is string))
                {
                    res["error"] = "In data must be string key 'token' or 'login' + 'password' " +
                                   "and string key 'data_key' + object key 'data_value'";
                    await ctx.Response.WriteAsJsonAsync(res);
                    return;
                }
                if (!(data.TryGetValue("data_value", out var data_value)))
                {
                    res["error"] = "In data must be string key 'token' or 'login' + 'password' " +
                                   "and string key 'data_key' + object key 'data_value'";
                    await ctx.Response.WriteAsJsonAsync(res);
                    return;
                }

                ctx.Response.StatusCode = StatusCodes.Status200OK;
                await ctx.Response.WriteAsJsonAsync(SetData((string)token, new((string)data_key, data_value)));
                return;
            }

            if (!(data.TryGetValue("login", out var login) && login is string))
            {
                res["error"] = "In data must be string key 'token' or 'login' + 'password' " +
                               "and string key 'data_key' + object key 'data_value'";
                await ctx.Response.WriteAsJsonAsync(res);
                return;
            }

            if (!(data.TryGetValue("password", out var password) && password is string))
            {
                res["error"] = "In data must be string key 'token' or 'login' + 'password' " +
                               "and string key 'data_key' + object key 'data_value'";
                await ctx.Response.WriteAsJsonAsync(res);
                return;
            }

            {
                if (!(data.TryGetValue("data_key", out var data_key) && data_key is string))
                {
                    res["error"] = "In data must be string key 'token' or 'login' + 'password' " +
                                   "and string key 'data_key' + object key 'data_value'";
                    await ctx.Response.WriteAsJsonAsync(res);
                    return;
                }
                if (!(data.TryGetValue("data_value", out var data_value)))
                {
                    res["error"] = "In data must be string key 'token' or 'login' + 'password' " +
                                   "and string key 'data_key' + object key 'data_value'";
                    await ctx.Response.WriteAsJsonAsync(res);
                    return;
                }

                ctx.Response.StatusCode = StatusCodes.Status200OK;
                await ctx.Response.WriteAsJsonAsync(SetData((string)login, (string)password,
                    new((string)data_key, data_value)));
            }
        }




        private void MessageHandler(ModuleMessage msg)
        {
            var apiPath = msg.To.Split('.').ToList();
            apiPath.RemoveAt(0);

            switch (apiPath[0])
            {
                case "register":
                    {
                        if (!(msg.Data.TryGetValue("login", out var login) && login is string))
                        {
                            msg.Reply(new() { { "error", "In data must be string key 'login'" } });
                            return;
                        }

                        if (!(msg.Data.TryGetValue("password", out var password) && password is string))
                        {
                            msg.Reply(new() { { "error", "In data must be string key 'password'" } });
                            return;
                        }

                        msg.Reply(Register((string)login, (string)password));
                        return;
                    }
                case "login":
                    {
                        if (!(msg.Data.TryGetValue("login", out var login) && login is string))
                        {
                            msg.Reply(new() { { "error", "In data must be string key 'login'" } });
                            return;
                        }

                        if (!(msg.Data.TryGetValue("password", out var password) && password is string))
                        {
                            msg.Reply(new() { { "error", "In data must be string key 'password'" } });
                            return;
                        }

                        msg.Reply(Login((string)login, (string)password));
                        return;
                    }
                case "get_data":
                    {
                        if (msg.Data.TryGetValue("token", out var token))
                        {
                            if (token is not string || !(msg.Data.TryGetValue("data", out var dataRequest) && dataRequest is string))
                            {
                                msg.Reply(new() { { "error", "In data must be string key 'token' or 'login' " +
                                                         "+ 'password' and string key 'data'" } });
                                return;
                            }

                            msg.Reply(GetData((string)token, (string)dataRequest));
                            return;
                        }

                        if (!(msg.Data.TryGetValue("login", out var login) && login is string))
                        {
                            msg.Reply(new() { { "error", "In data must be string key 'token' or 'login' " +
                                                     "+ 'password' and string key 'data'" } });
                            return;
                        }

                        if (!(msg.Data.TryGetValue("password", out var password) && password is string))
                        {
                            msg.Reply(new() { { "error", "In data must be string key 'token' or 'login' " +
                                                     "+ 'password' and string key 'data'" } });
                            return;
                        }

                        {
                            if (!(msg.Data.TryGetValue("data", out var dataRequest) && dataRequest is string))
                            {
                                msg.Reply(new() { { "error", "In data must be string key 'token' or 'login' " +
                                                         "+ 'password' and string key 'data'" } });
                                return;
                            }

                            msg.Reply(GetData((string)login, (string)password, (string)dataRequest));
                        }
                        return;
                    }
                case "set_data":
                    {
                        if (msg.Data.TryGetValue("token", out var token))
                        {
                            if (token is not string)
                            {
                                msg.Reply(new() { { "error", "In data must be string key 'token' or 'login' + 'password' " +
                                                         "and string key 'data_key' + object key 'data_value'" } });
                                return;
                            }

                            if (!(msg.Data.TryGetValue("data_key", out var data_key) && data_key is string))
                            {
                                msg.Reply(new() { { "error", "In data must be string key 'token' or 'login' + 'password' " +
                                                         "and string key 'data_key' + object key 'data_value'" } });
                                return;
                            }
                            if (!(msg.Data.TryGetValue("data_value", out var data_value)))
                            {
                                msg.Reply(new() { { "error", "In data must be string key 'token' or 'login' + 'password' " +
                                                         "and string key 'data_key' + object key 'data_value'" } });
                                return;
                            }

                            msg.Reply(SetData((string)token, new((string)data_key, data_value)));
                            return;
                        }

                        if (!(msg.Data.TryGetValue("login", out var login) && login is string))
                        {
                            msg.Reply(new() { { "error", "In data must be string key 'token' or 'login' + 'password' " +
                                                     "and string key 'data_key' + object key 'data_value'" } });
                            return;
                        }

                        if (!(msg.Data.TryGetValue("password", out var password) && password is string))
                        {
                            msg.Reply(new() { { "error", "In data must be string key 'token' or 'login' + 'password' " +
                                                     "and string key 'data_key' + object key 'data_value'" } });
                            return;
                        }

                        {
                            if (!(msg.Data.TryGetValue("data_key", out var data_key) && data_key is string))
                            {
                                msg.Reply(new() { { "error", "In data must be string key 'token' or 'login' + 'password' " +
                                                         "and string key 'data_key' + object key 'data_value'" } });
                                return;
                            }
                            if (!(msg.Data.TryGetValue("data_value", out var data_value)))
                            {
                                msg.Reply(new() { { "error", "In data must be string key 'token' or 'login' + 'password' " +
                                                         "and string key 'data_key' + object key 'data_value'" } });
                                return;
                            }

                            msg.Reply(SetData((string)login, (string)password, new((string)data_key, data_value)));
                            return;
                        }
                    }
            }
            msg.Reply(new() { { "error", "api doesn't know how to handle this message" } });
        }

        private Dictionary<string, object> Register(string login, string password)
        {
            Dictionary<string, object> res = new();
            var hash = SHA512.HashData(Array.ConvertAll((login + SecretString + password).ToArray(), Convert.ToByte));
            var hashString = BitConverter.ToString(hash);


            if (SimpleDataBase.ContainsKey(login + "_data"))
            {
                res["error"] = "Current user already exist";
                return res;
            }

            SimpleDataBase[login + "_data"] = new Dictionary<string, object>() { { "_hash", hashString } };
            res["success"] = "User registered successfully.";
            return res;
        }

        private Dictionary<string, object> Login(string login, string password)
        {
            Dictionary<string, object> res = new();
            var hash = SHA512.HashData(Array.ConvertAll((login + SecretString + password).ToArray(), Convert.ToByte));
            var hashString = BitConverter.ToString(hash);

            if (!SimpleDataBase.ContainsKey(login + "_data"))
            {
                res["error"] = "Current user are not exist";
                return res;
            }

            if ((string)((Dictionary<string, object>)(SimpleDataBase[login + "_data"]))["_hash"] != hashString)
            {
                res["error"] = "Password is not correct";
                return res;
            }

            var token = string.Join("", RandomNumberGenerator.GetItems<char>(_letters.ToCharArray(), 200));
            SimpleDataBase[token] = login;
            res["success"] = "User logged successfully.";
            res["token"] = token;
            return res;
        }

        private Dictionary<string, object> GetData(string login, string password, string data)
        {
            Dictionary<string, object> res = new();
            var hash = SHA512.HashData(Array.ConvertAll((login + SecretString + password).ToArray(), Convert.ToByte));
            var hashString = BitConverter.ToString(hash);

            if (!SimpleDataBase.ContainsKey(login + "_data"))
            {
                res["error"] = "Current user are not exist";
                return res;
            }

            if ((string)((Dictionary<string, object>)(SimpleDataBase[login + "_data"]))["_hash"] != hashString)
            {
                res["error"] = "Password is not correct";
                return res;
            }

            if (data == "_hash")
                data = "hash";

            var userData = (Dictionary<string, object>)SimpleDataBase[login + "_data"];
            res["result"] = userData.GetValueOrDefault(data, null);

            return res;
        }
        private Dictionary<string, object> GetData(string token, string data)
        {
            Dictionary<string, object> res = new();
            if (!SimpleDataBase.TryGetValue(token, out var login))
            {
                res["error"] = "access denied";
            }

            if (data == "_hash")
                data = "hash";

            var userData = (Dictionary<string, object>)SimpleDataBase[login + "_data"];
            res["result"] = userData.GetValueOrDefault(data, null);


            return res;
        }

        private Dictionary<string, object> SetData(string login, string password, KeyValuePair<string, object> data)
        {
            Dictionary<string, object> res = new();
            var hash = SHA512.HashData(Array.ConvertAll((login + SecretString + password).ToArray(), Convert.ToByte));
            var hashString = BitConverter.ToString(hash);

            if (!SimpleDataBase.ContainsKey(login + "_data"))
            {
                res["error"] = "Current user are not exist";
                return res;
            }

            if ((string)((Dictionary<string, object>)(SimpleDataBase[login + "_data"]))["_hash"] != hashString)
            {
                res["error"] = "Password is not correct";
                return res;
            }

            if (data.Key == "_hash")
                data = new KeyValuePair<string, object>("hash", data.Value);

            var userData = (Dictionary<string, object>)SimpleDataBase[login + "_data"];
            userData.Add(data.Key, data.Value);

            res["success"] = "data saved";
            return res;
        }
        private Dictionary<string, object> SetData(string token, KeyValuePair<string, object> data)
        {
            Dictionary<string, object> res = new();
            if (!SimpleDataBase.TryGetValue(token, out var login))
            {
                res["error"] = "access denied";
            }

            if (data.Key == "_hash")
                data = new KeyValuePair<string, object>("hash", data.Value);

            var userData = (Dictionary<string, object>)SimpleDataBase[login + "_data"];
            userData.Add(data.Key, data.Value);

            res["success"] = "data saved";
            return res;
        }
    }
}
