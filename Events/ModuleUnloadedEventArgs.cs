namespace ModuWeb.Events
{
    public class ModuleUnloadedEventArgs : EventArgs
    {
        public ModuleUnloadedEventArgs(string moduleName)
        {
            ModuleName = moduleName;
        }

        public string ModuleName { get; }
    }
}
