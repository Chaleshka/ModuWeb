namespace ModuWeb.Events
{
    public class RequestReceivedEventArgs : EventArgs
    {
        public RequestReceivedEventArgs(HttpContext context, ModuleBase? targetModule, bool corsPassed, bool originsPassed = true, bool headersPassed = true)
        {
            Context = context;
            TargetModule = targetModule;
            CorsPassed = corsPassed;
            OriginPassed = originsPassed;
            HeaderPassed = headersPassed;
        }

        public HttpContext Context { get; }
        public ModuleBase? TargetModule { get; }
        public bool CorsPassed { get; }
        public bool OriginPassed { get; }
        public bool HeaderPassed { get; }
    }
}
