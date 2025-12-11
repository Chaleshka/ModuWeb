using System.Reflection;
using System.Runtime.Loader;

namespace ModuWeb.Events
{
    public class ModuleLoadedEventArgs : EventArgs
    {
        public ModuleLoadedEventArgs(string moduleName, ModuleBase module, Assembly assembly, AssemblyLoadContext context, string originalPath)
        {
            ModuleName = moduleName;
            Module = module;
            Assembly = assembly;
            Context = context;
            OriginalPath = originalPath;
        }

        public string ModuleName { get; }
        public ModuleBase Module { get; }
        public Assembly Assembly { get; }
        public AssemblyLoadContext Context { get; }
        public string OriginalPath { get; }
    }
}
