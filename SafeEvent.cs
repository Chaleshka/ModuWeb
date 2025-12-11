using ModuWeb.Events;
using System.Runtime.Loader;

namespace ModuWeb
{
    public class SafeEvent<T> where T : Delegate
    {
        private readonly List<T> _handlers = new();
        private readonly object _lock = new();

        public void AddHandler(T handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            lock (_lock)
            {
                _handlers.Add(handler);
            }
        }

        public static bool InspectDelegate(Delegate d)
        {
            var method = d.Method;
            var target = d.Target;

            var asm = method.Module.Assembly;
            var alc = AssemblyLoadContext.GetLoadContext(asm);
            var res = ModuleManager.Instance.modules.FirstOrDefault(f => f.Value.Context.Equals(alc));
            if (res.Key == default)
                return false;
            return true;
        }

        public void RemoveHandler(T handler)
        {
            if (handler == null) return;
            lock (_lock)
            {
                _handlers.RemoveAll(wr => 
                {
                    return wr.Equals(handler);
                });
            }
        }

        public void Invoke(params object[] args)
        {
            List<T> toInvoke = new();
            lock (_lock)
            {
                for (int i = _handlers.Count - 1; i >= 0; i--)
                {
                    var h = _handlers[i];
                    if (InspectDelegate(h))
                        toInvoke.Add(h);
                    else
                        _handlers.RemoveAt(i);
                }
            }

            foreach (var h in toInvoke)
            {
                try { h.DynamicInvoke(args); }
                catch (Exception ex)
                {
                    Logger.Error($"SafeEvent handler threw: {ex}");
                }
            }
        }

        public void Clear()
        {
            lock (_lock) { _handlers.Clear(); }
        }

        public static SafeEvent<T> operator +(SafeEvent<T> e, T handler)
        {
            e.AddHandler(handler);
            return e;
        }

        public static SafeEvent<T> operator -(SafeEvent<T> e, T handler)
        {
            e.RemoveHandler(handler);
            return e;
        }
    }
}
