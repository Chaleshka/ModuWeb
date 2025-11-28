namespace ModuWeb.Events
{
    public class RequestRecievedEventArgs : EventArgs
    {
        public RequestRecievedEventArgs(HttpContext context, ModuleBase? targetModule, bool corsPassed, bool originsPassed = true, bool headersPassed = true)
        {
            Context = context;
            TargetModule = targetModule;
            CorsPassed = corsPassed;
        }

        public HttpContext Context { get; }
        public ModuleBase? TargetModule { get; }
        public bool CorsPassed { get; }
        public bool OriginPassed { get; }
        public bool HeaderPassed { get; }
    }
}
