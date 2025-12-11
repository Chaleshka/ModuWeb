using System.Reflection;
using System.Runtime.Loader;

namespace ModuWeb.Events
{
    public class ModuleUnloadedEventArgs : EventArgs
    {
        public ModuleUnloadedEventArgs(string moduleName, ModuleBase module, AssemblyLoadContext assembly)
        {
            ModuleName = moduleName;
            Module = module;
            Assembly = assembly;
        }

        public string ModuleName { get; }
        public ModuleBase Module { get; }
        public AssemblyLoadContext Assembly { get; }
    }
}
