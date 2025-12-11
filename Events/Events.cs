namespace ModuWeb.Events
{
    public class Events
    {
        internal static SafeEvent<Action<ModuleLoadedEventArgs>> ModuleLoadedSafeEvent = new();
        internal static SafeEvent<Action<ModuleUnloadedEventArgs>> ModuleUnloadedSafeEvent = new();
        internal static SafeEvent<Action<RequestRecievedEventArgs>> RequestReceivedSafeEvent = new();
        internal static SafeEvent<Action<ModuleMessageSentEventArgs>> ModuleMessageSentSafeEvent = new();


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

        public static event Action<RequestRecievedEventArgs> RequestReceived
        {
            add => RequestReceivedSafeEvent.AddHandler(value);
            remove => RequestReceivedSafeEvent.RemoveHandler(value);
        }

        public static event Action<ModuleMessageSentEventArgs> ModuleMessageSent
        {
            add => ModuleMessageSentSafeEvent.AddHandler(value);
            remove => ModuleMessageSentSafeEvent.RemoveHandler(value);
        }
    }
}
