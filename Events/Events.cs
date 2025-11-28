namespace ModuWeb.Events
{
    public class Events
    {
        internal static SafeEvent<Action<ModuleLoadedEventArgs>> ModuleLoadedSafeEvent = new();
        internal static SafeEvent<Action<ModuleUnloadedEventArgs>> ModuleUnloadedSafeEvent = new();
        internal static SafeEvent<Action<ModuleReloadedEventArgs>> ModuleReloadedSafeEvent = new();
        internal static SafeEvent<Action<RequestRecievedEventArgs>> RequestRecievedSafeEvent = new();


        public static event Action<ModuleLoadedEventArgs> ModuleLoaded
        {
            add => ModuleLoadedSafeEvent.AddHandler(value);
            remove => ModuleLoadedSafeEvent.RemoveHandler(value);
        }

        public static event Action<ModuleUnloadedEventArgs> ModuleUnloaded
        {
            add => ModuleUnloadedSafeEvent.AddHandler(value);
            remove => ModuleUnloadedSafeEvent.RemoveHandler(value);
        }

        public static event Action<ModuleReloadedEventArgs> ModuleReloaded
        {
            add => ModuleReloadedSafeEvent.AddHandler(value);
            remove => ModuleReloadedSafeEvent.RemoveHandler(value);
        }

        public static event Action<RequestRecievedEventArgs> RequestRecieved
        {
            add => RequestRecievedSafeEvent.AddHandler(value);
            remove => RequestRecievedSafeEvent.RemoveHandler(value);
        }

        public static event Action<ModuleLoadedEventArgs> ModuleInvoked;
        public static event Action<ModuleLoadedEventArgs> ModuleMessageSent;
    }
}
