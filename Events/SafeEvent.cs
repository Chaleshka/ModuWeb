using System.Runtime.Loader;

namespace ModuWeb.Events
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

        internal void RemoveHandlersFromContext(AssemblyLoadContext context)
        {
            lock (_lock)
            {
                _handlers.RemoveAll(handler =>
                    ReferenceEquals(AssemblyLoadContext.GetLoadContext(handler.Method.Module.Assembly), context));
            }
        }

        public void Invoke(params object[] args)
        {
            List<T> toInvoke = new();
            lock (_lock)
            {
                toInvoke.AddRange(_handlers);
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
