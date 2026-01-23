
# ModuWeb

**ModuWeb** is a .NET web application that supports dynamic runtime loading, reloading, and unloading of external modules (`.dll` files). 
Each module is self-contained and can expose custom HTTP routes, CORS policies, and request handlers. 

---

## 🧩 Features

- 🔄 **Hot-reloadable modules** – automatically reloads modules when their `.dll` files are updated or replaced.  
- 📁 **File system watching** – monitors the `modules/` folder for `.dll` changes using `FileSystemWatcher`.  
- 🌐 **Per-module CORS** – modules define their own CORS rules.  
- 🔀 **Custom middleware routing** – routes HTTP requests to appropriate modules based on URL.  
- 💾 **Session support** – every module can create and/or use session storage.  
- ⚡ **Event system** – allows modules to subscribe to and react to system events.  
- 💬 **Message system** – enables modules to communicate with each other.  
- 🧾 **Built-in logger** – simple color-coded console logger for info, warnings, and errors.  


---

## 📁 Project Structure

```
ModuWeb/
│
├── Properties/
│   └── launchSettings.json                 # Startup settings for dev mode
│
├── Events/
│   ├── Events.cs                           # Contains all events
│   ├── ModuleLoadedEventArgs.cs            # Args for event about loaded module
│   ├── ModuleMessageSentEventArgs.cs       # Args for event about sent message
│   ├── ModuleUnloadedEventArgs.cs          # Args for event about unloaded module
│   ├── RequestRecievedEventArgs.cs         # Args for event about recieved http request
│   └── SafeEvent.cs                        # Base and safe class for events
│
├── examples/                               # Examples modules
│
├── Extensions/
│   ├── ArrayExtention.cs                   # Little extention for array
│   ├── HttpRequestExtention.cs             # Extention for get request data (from query string or json body)
│   └── StringExtention.cs                  # Little extention for string.Replace(old, new, count)
│
├── ModuleLoadSystem/
│   ├── ModuleLoadContext.cs                # Custom AssemblyLoadContext
│   ├── ModuleManager.cs                    # Loads/unloads modules and handles lifecycle
│   └── ModuleWatcher.cs                    # Watches for module file changes
│
├── ModuleMessenger/
│   ├── ModuleMessage.cs                    # Module message that every moudle can create and receive
│   └── ModuleMessenger.cs                  # System handler for module messages
│
├── SessionSystem/
│   ├── ISessionService.cs                  # Interface of session service
│   ├── LiteDbSessionService.cs             # Session service for create and working with sessions
│   └── SessionData.cs                      # Data that store into database
│
├── Storage/
│   ├── IStorageService.cs                  # Interface of storage service
│   └── LiteDbStorageService.cs             # Data that store into database
│
├── appsettings.json                        # Default appsettings
├── DynamicCorsPolicyProvider.cs            # CORS policy provider per module
├── LICENSE.txt                             # License for this project
├── Logger.cs                               # Static logger with color output
├── ModuleBase.cs                           # Base class for all modules
├── ModuleCorsGuardMiddleware               # Middleware for handling CORS per module
├── Program.cs                              # Application entry point
├── QueryParser.cs                          # Tool for parse args from query
└── RouteDictionary.cs                      # Path + method → handler registry
```

---

## 🚀 Getting Started

### To run the project, make sure you have the .NET Runtime (Microsoft.AspNetCore.App) or SDK version 9.0.2 or higher installed.

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
**Important:** To use `HttpContext` and other ASP.NET Core types in your module, add a `FrameworkReference` to your `.csproj` file:

```xml
<ItemGroup>
  <!-- Add this to get HttpContext and ASP.NET Core dependencies -->
  <FrameworkReference Include="Microsoft.AspNetCore.App" />
  
  <!-- Reference to ModuWeb.dll -->
  <Reference Include="ModuWeb">
    <HintPath>path/to/ModuWeb.dll</HintPath>
  </Reference>
</ItemGroup>
```

This way you don't need to manually add NuGet packages for ASP.NET Core dependencies.

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
- `WithOriginsCors`, `WithHeadersCors`, `BlockFailedCorsRequests` — specify CORS policies.
- `ModuleName` — name of module that will used for some system tools.
- `OnModuleLoad()` — optional initialization logic.
- `OnModuleUnLoad()` — optional cleanup logic.

Module files may have unique names:
    - `index.dll` — special module name used to handle the main page (`/` or `/index`).

<br />

You can also see the examples in [examples](/examples).

---

## 📌 Notes

- Dependencies should be placed in `modules/dependencies/`. They will be copied automatically.
- Modules are loaded into memory. Dependencies only as 
- A failed module load is logged but does not crash the host.
- The middleware checks the base API path (from configuration) and maps requests accordingly.
- Empty string into path in Map will mean base url with some method.

---

## 📂 Example

After placing a sample DLL in `modules/`, you can access its route via:

```
http://localhost:5000/{ModuleName}/{Route}
```

For example, with a module named [`HelloWorld`](/examples/HelloWorldModule.cs):

```
GET http://localhost:5000/HelloWorld/hello
```

---

## 🧪 Example Folder

The [`example/`](/examples) folder includes working example modules you can compile and test.

---

## 📃 License

This project is open-source and free to use, modify, and distribute.
