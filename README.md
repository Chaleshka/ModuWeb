
# ModuWeb

**ModuWeb** is a .NET web application that supports dynamic runtime loading, reloading, and unloading of external modules (`.dll` files). 
Each module is self-contained and can expose custom HTTP routes, CORS policies, and request handlers. 

---

## 🧩 Features

- 🔄 **Hot-reloadable modules** – automatically reloads modules when their `.dll` files are updated or replaced.
- 📁 **File system watching** – monitors the `modules/` folder for `.dll` changes using `FileSystemWatcher`.
- 🌐 **Per-module CORS** – modules define their own CORS rules.
- 🔀 **Custom middleware routing** – routes HTTP requests to appropriate modules based on URL.
- 🧾 **Built-in logger** – simple color-coded console logger for info, warnings, and errors.

---

## 📁 Project Structure

```
ModuWeb/
│
├── Json/
│   ├── CustomJsonSerializer.cs
│   └── CustomJsonDeserializer.cs
├── Properties/
│   └── launchSettings.json      # Startup settings for dev mode
├── examples/                    # Examples modules
├── Extentions/
│   ├── ArrayExtention.cs        # Little extention for array
│   ├── HttpRequestExtention.cs  # Extention for get request data (from query string or json body)
│   └── StringExtention.cs       # Little extention for string.Replace(old, new, count)
├── DynamicCorsPolicy.cs         # CORS policy provider per module
├── LICENSE.txt                  # License for this project
├── Logger.cs                    # Static logger with color output
├── ModuleBase.cs                # Base class for all modules
├── ModuleLoadContext.cs         # Custom AssemblyLoadContext
├── ModuleManager.cs             # Loads/unloads modules and handles lifecycle
├── ModuleMiddleware.cs          # Dispatches requests to the correct module
├── ModuleWatcher.cs             # Watches for module file changes
├── Program.cs                   # Application entry point
├── RouteDictionary.cs           # Path + method → handler registry
└── appsettings.json             # Default appsettings
```

---

## 🚀 Getting Started

### To run the project, you need to make sure that you have it installed .NET Runtime (Microsoft.AspNetCore.App) or SDK v9.0.2+.

#### How can you check if SDK is installed?

```bash
dotnet --list-sdks
```
If it's installed, you must see something like that:
```
9.0.200 [C:\Program Files\dotnet\sdk]
```
If it's not installed, you need to [install it there](https://dotnet.microsoft.com/en-us/download).

<br />

#### How can you check if Runtime is installed?

```bash
dotnet --list-runtimes
```
If it's installed, you must see something like that:
```
Microsoft.AspNetCore.App 9.0.2 [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App]
```
If it's not installed, you need to [install it there](https://dotnet.microsoft.com/en-us/download/dotnet/9.0/runtime). Choose `Run server apps`.

<br />
<br />

### 🚦 Running the Application

#### Option 1: Build from Source

1. **Clone the repository:**
```bash
git clone https://github.com/Chaleshka/ModuWeb.git
cd ModuWeb
```
2. **Build the solution** using .NET SDK 9.0.2+.
```bash
dotnet build
```
3. Run the app:
```bash
dotnet run
```

#### Option 2: Run from Release

1. **Download the latest release** from the [Releases page](https://github.com/Chaleshka/ModuWeb/releases)
2. **Extract the archive** to your preferred directory
3. **Launch the app:**
```bash
# Windows
ModuWeb.exe

# Linux/macOS:
dotnet ModuWeb.dll
```

### How to load modules?

After launching the program, the modules folder will be created. You need to put all the modules you need in it. <br />
Also, if dependencies are required, drop them in the modules/dependencies folder. <br />
If everything is fine with the modules, they will be loaded automatically.

---

## 🔧 Module Development

Firstly create project:
```bash
dotnet new classlib -n ModuleName
cd ModuleName
```
Then you need to add to dependencies ModuWeb.dll. <br />
After that you can create your modules.

<br />

A module must inherit from [`ModuleBase`](ModuleBase.cs) and override methods such as:

```csharp
public class HelloWorldModule : ModuleBase
{
    public override async Task OnModuleLoad()
    {
        Map("hello", "GET", HelloWorldHandler);
    }

    public async Task HelloWorldHandler(HttpContext context)
    {
        context.Response.StatusCode = 200;
        await context.Response.WriteAsync("Hello World!");
    }
}
```

- `Map(string path, string method, Func<HttpContext, Task> handler)` — maps a route.
- `Handle(...)` — receives and routes the request.
- `WithOriginsCors`, `WithHeadersCors` — specify CORS policies.
- `OnModuleLoad()` — optional initialization logic.
- `OnModuleUnLoad()` — optional cleanup logic.

<br />
<br />

You can also see the examples in [examples](https://github.com/Chaleshka/ModuWeb/tree/main/examples).

---

## 📌 Notes

- Dependencies should be placed in `modules/dependencies/`. They will be copied automatically.
- Modules are loads into memory. Dependencies only as 
- A failed module load is logged but does not crash the host.
- The middleware checks the base API path (from configuration) and maps requests accordingly.
- Empty string into path in Map will mean base url with some method.

---

## 📂 Example

After placing a sample DLL in `modules/`, you can access its route via:

```
http://localhost:5000/{ModuleName}/{Route}
```

For example, with a module named [`HelloWorld`](/examples/HelloWorldModule):

```
GET http://localhost:5000/HelloWorld/hello
```

---

## 🧪 Example Folder

The [`example/`](/examples) folder includes working example modules you can compile and test.

---

## 📃 License

This project is open-source and free to use, modify, and distribute.
