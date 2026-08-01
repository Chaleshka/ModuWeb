namespace ModuWeb.Events
{
    public class ModuleLoadedEventArgs : EventArgs
    {
        public ModuleLoadedEventArgs(string moduleName, string originalPath)
        {
            ModuleName = moduleName;
            OriginalPath = originalPath;
        }

        public string ModuleName { get; }
        public string OriginalPath { get; }
    }
}
