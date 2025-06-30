using System.Runtime.Loader;

namespace ModuWeb
{
    public class ModuleLoadedEventArgs : EventArgs
    {
        public readonly string ModuleName;
        public readonly ModuleBase Module;
        public readonly AssemblyLoadContext ModuleContext;

        public ModuleLoadedEventArgs(string moduleName, ModuleBase module, AssemblyLoadContext moduleContext)
        {
            ModuleName = moduleName;
            Module = module;
            ModuleContext = moduleContext;
        }
    }
}
