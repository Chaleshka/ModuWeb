using Microsoft.AspNetCore.Http;
using ModuWeb;
using ModuWeb.Events;

namespace ModuWeb.examples
{
    public class ModuleEventLogger : ModuleBase
    {
        private List<string> log = new();
        private object locker = new();
        public override Task OnModuleLoad()
        {
            Events.ModuleLoaded += OnModuleLoadedEvent;
            Events.ModuleUnloaded += OnModuleUnloadedEvent;
            Events.ModuleMessageSent += OnModuleMessageSentEvent;
            Events.RequestReceived += OnRequestReceivedEvent;

            Map("/", "GET", ShowLogs);

            return base.OnModuleLoad();
        }

        private async Task ShowLogs(HttpContext ctx)
        {
            ctx.Response.ContentType = "text/plain";
            await ctx.Response.WriteAsync(string.Join("\n", log.TakeLast(100)));
        }

        private void OnModuleLoadedEvent(ModuleLoadedEventArgs ev)
        {
            lock (locker)
                log.Add($"[{DateTime.Now:dd-MM-yyyy HH-mm-ss}] Module '{ev.ModuleName}' loaded.");
        }
        private void OnModuleUnloadedEvent(ModuleUnloadedEventArgs ev)
        {
            lock (locker)
                log.Add($"[{DateTime.Now:dd-MM-yyyy HH-mm-ss}] Module '{ev.ModuleName}' unloaded.");
        }
        private void OnModuleMessageSentEvent(ModuleMessageSentEventArgs ev)
        {
            lock (locker)
                log.Add($"[{DateTime.Now:dd-MM-yyyy HH-mm-ss}] Module '{ev.From}' sent message({ev.MessageId}" +
                        $"{(ev.RespondTo == null ? "" : $"-> {ev.RespondTo}")}) to '{ev.To}'.");
        }
        private void OnRequestReceivedEvent(RequestRecievedEventArgs ev)
        {
            lock (locker)
            {
                var s = $"[{DateTime.Now:dd-MM-yyyy HH-mm-ss}] Request to '{ev.Context.Request.Path}' path that: ";
                var s1 = s;
                if (ev.HeaderPassed & ev.CorsPassed)
                    s += "passed all checks";
                else if (ev.HeaderPassed & ev.OriginPassed)
                    s += "passed header and origin checks";
                else if (ev.HeaderPassed)
                    s += "passed header check only";
                else if (ev.OriginPassed)
                    s += "passed origin check only";

                if (!ev.CorsPassed)
                    s += ", but request has been blocked by module";
                s += ".";
                if (s1 + '.' == s)
                    log.Add(s.Replace(" that: ", ""));
            }
        }
    }
}
