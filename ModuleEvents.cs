namespace ModuWeb
{
    public class ModuleEvents<T>
    {
        private readonly Dictionary<string, Action<T>> _delegates = new();

        public void InvokeAll(T arg)
        {
            foreach (var h in _delegates.Values)
                h?.Invoke(arg);
        }

        public void Register(string name, Action<T> handler)
        {
            if (_delegates.ContainsKey(name))
                _delegates[name] += handler;
            else
                _delegates[name] = handler;
        }

        public void Unregister(string name, Action<T> handler)
        {
            if (_delegates.ContainsKey(name))
            {
                _delegates[name] -= handler;
                if (_delegates[name] == null)
                    _delegates.Remove(name);
            }
        }

        public void Dispatch(string name, T arg)
        {
            if (_delegates.TryGetValue(name, out var d))
                d?.Invoke(arg);
        }

        public event Action<T> Global
        {
            add => Register("Global", value);
            remove => Unregister("Global", value);
        }
    }
}
