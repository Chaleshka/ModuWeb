
# ModuWeb

**ModuWeb** is a .NET web application that supports dynamic runtime loading, reloading, and unloading of external modules (`.dll` files). 
Each module can expose custom HTTP routes, CORS policies, and request handlers.

---

## 🧩 Features

- 🔄 **Hot-reloadable modules** – automatically reloads a module when its `.dll` file is updated or replaced.
- 📦 **Shared dependencies** – one managed assembly generation is shared by all consuming modules; changing one reloads only its affected consumers.
- 📁 **File system watching** – monitors module and shared dependency DLLs with `FileSystemWatcher`.
- 🌐 **Per-module CORS** – modules define their own CORS rules.  
- 🔀 **Custom middleware routing** – routes HTTP requests to appropriate modules based on URL.  
- 💾 **Session support** – every module can create and/or use session storage.  
- ⚡ **Event system** – allows modules to subscribe to and react to system events.  
- 💬 **Message system** – enables modules to communicate with each other.  
- 🧾 **Built-in logger** – simple color-coded console logger for info, warnings, and errors.  
- 🖼️ **Razor view engine** – runtime Razor compilation via RazorLight for HTML pages with models.  
- 📡 **Server-Sent Events (SSE)** – built-in support for real-time server-to-client streaming with a fluent Razor helper.  


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
│   ├── RequestReceivedEventArgs.cs         # Args for event about received http request
│   └── SafeEvent.cs                        # Base and safe class for events
│
├── examples/                               # Examples modules
│
├── Cors/
│   ├── DynamicCorsPolicyProvider.cs        # CORS policy provider per module
│   ├── Headers.cs                          # CORS headers constants
│   └── ModuleCorsGuardMiddleware.cs        # Middleware for handling CORS per module
│
├── Extensions/
│   ├── ArrayExtension.cs                   # Little extension for array
│   ├── HttpRequestExtension.cs             # Extension for get request data (from query string or json body)
│   ├── HttpResponseExtension.cs            # Extensions for Razor page rendering and SSE streaming
│   ├── JsonOptionExtension.cs              # JSON serializer options (camelCase, null handling)
│   ├── SessionExtensions.cs                # Session helper extensions for HttpContext
│   ├── SseHtmlHelper.cs                    # Fluent SSE helper for Razor views (Sse.Stream(...).Bind(...))
│   └── StringExtension.cs                  # Little extension for string.Replace(old, new, count)
│
├── ModuleLoadSystem/
│   ├── ModuleLoadContext.cs                # Custom AssemblyLoadContext
│   ├── ModuleManager.cs                    # Loads/unloads modules and handles lifecycle
│   ├── SharedDependencyLoadContext.cs       # Collectible context for one shared dependency generation
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
├── ViewEngine/
│   ├── IModuleViewEngine.cs                # Interface for module view engine
│   └── ModuleViewEngine.cs                 # RazorLight-based runtime Razor compilation
│
├── appsettings.json                        # Default appsettings
├── LICENSE.txt                             # License for this project
├── Logger.cs                               # Static logger with color output
├── ModuleBase.cs                           # Base class for all modules
├── ModuleMiddleware.cs                     # Middleware for routing requests to modules
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

The expected layout is:

```text
modules/
  Orders.dll
  Reports.dll
  dependencies/
    Shared.Contracts.dll
    Shared.Database.dll
```

`modules/dependencies` is shared storage: dependency DLLs are **not copied** into each module's temporary folder. The loader reads the metadata of a module and its dependency graph, then loads one collectible assembly generation for every required shared DLL. All modules that use the same generation receive the very same CLR `Assembly` instance. Therefore public DTOs from that DLL can be passed through `ModuleMessage.Data` and cast by another consumer, while `static` fields are shared by those consumers.

When a shared DLL changes, ModuWeb rebuilds that dependency generation, every shared DLL that transitively depends on it, and only active modules whose dependency graph contains one of them. Replacement candidates are initialized before the active modules are changed; if initialization or view registration fails, the previous group remains active. A regular module-only reload reuses the current shared dependency generation.

Circular references between DLLs in `modules/dependencies` are not supported and are rejected during loading.

Only one active DLL per assembly simple name is supported in this shared directory. For example, two incompatible versions of `Shared.Contracts` cannot be kept side by side there.

The temporary `modules/temp` folder contains only short-lived copies of module DLLs used for hot reload. The previous package is deleted when the old module has been unloaded.

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
    public override string ModuleName => "hello";

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
- `ModuleName` — canonical module identifier for routes, views, events, and messages. It must be unique. If it is not overridden, the filename without `.dll` is used as a stable fallback before `OnModuleLoad()` runs. For example, `HelloWorld.dll` uses `HelloWorld`.
- `OnModuleLoad()` — optional initialization logic.
- `OnModuleUnload()` — optional cleanup logic.

Module files may have unique names:
    - a module whose `ModuleName` is `index` handles the main page (`/` or `/index`);
    - otherwise URLs use the canonical `ModuleName`, for example `/hello/hello` for the example above.

<br />

You can also see the examples in [examples](/examples).

---

## 🖼️ Razor Views

Modules can render HTML pages using Razor (`.cshtml`) templates via RazorLight. Views are embedded as resources in the module DLL.

### Setup

1. Mark `.cshtml` files as **Embedded Resource** in your `.csproj`:

```xml
<ItemGroup>
  <EmbeddedResource Include="Views\**\*.cshtml" />
</ItemGroup>
```

Views are registered automatically when the module is loaded — no extra code needed.

2. Render a page from a handler:

```csharp
private async Task PageHandler(HttpContext context)
{
    var model = new { Title = "Hello", Message = "World" };
    await context.Response.WriteRazorPageAsync("Views/Index.cshtml", model);
}
```

3. Access model data in `.cshtml`:

```html
<h1>@Model.Title</h1>
<p>@Model.Message</p>
```

### `GetInitialViewData` — shared data for all views

Override `GetInitialViewData` in your module to provide common data that will be automatically available in every Razor view — without passing it manually each time. Useful for base paths, locale, user info, app settings, etc.

```csharp
protected override Dictionary<string, object> GetInitialViewData(HttpContext context) => new()
{
    int i = 0;
    ["Title"] = $"Some title #" + (++i).ToString(),
    ["Lang"] = context.Request.Headers["Accept-Language"].FirstOrDefault() ?? "en",
    ["Year"] = DateTime.Now.Year
};
```

These values are merged into the model and accessible in `.cshtml` as `@Model.BasePath`, `@Model.Lang`, etc.:

```html
<html lang="@Model.Lang">
<head>
    <title>@Model.Title</title>
</head>
<body>
    <footer>© @Model.Year</footer>
</body>
</html>
```

This is called automatically by `WriteRazorPageAsync` when no explicit `viewData` parameter is passed. If you pass `viewData` manually, `GetInitialViewData` is skipped.

---

## 📡 Server-Sent Events (SSE)

ModuWeb has built-in SSE support on both sides: a **server-side** extension for streaming data and a **client-side Razor helper** for receiving it — no jQuery or manual JavaScript needed.

### Server side — `WriteSseAsync`

In your module handler, use `WriteSseAsync` to push data to the client on a fixed interval:

```csharp
using ModuWeb.Extensions;

// Simple (synchronous generator)
private async Task StreamHandler(HttpContext context)
{
    await context.Response.WriteSseAsync(() => new
    {
        time = DateTime.Now.ToString("HH:mm:ss"),
        date = DateTime.Now.ToString("yyyy-MM-dd")
    }, intervalMs: 5000);
}

// Async generator (for DB queries, HTTP calls, etc.)
private async Task StreamHandler(HttpContext context)
{
    await context.Response.WriteSseAsync(async ct =>
    {
        var data = await GetSensorDataAsync(ct);
        return new { temperature = data.Temp, humidity = data.Hum };
    }, intervalMs: 2000);
}
```

The extension handles `Content-Type`, `Cache-Control`, flushing, JSON serialization, and client disconnect automatically.

You can also send named events:

```csharp
await context.Response.WriteSseAsync(() => payload, intervalMs: 1000, eventName: "sensor-update");
```

### Client side — `Sse.Stream()` Razor helper

Instead of writing JavaScript manually, use the fluent `Sse` helper directly in `.cshtml`:

```html
@using ModuWeb.Extensions

<p id="serverTime">Loading...</p>
<p id="lastUpdate"></p>

@(Sse.Stream("time-stream")
    .Bind("#serverTime", "time")
    .Bind("#lastUpdate", "date", "Updated: {0}")
    .Render())
```

This generates all the `EventSource` JavaScript automatically. No jQuery, no `<script>` blocks.

#### Available methods

| Method | Description |
|--------|-------------|
| `.Bind("#id", "field")` | Sets element's `textContent` to the JSON field value |
| `.Bind("#id", "field", "Format: {0}")` | Same, with a format string |
| `.OnMessage("js code")` | Raw JS executed on each message (has access to `data`) |
| `.OnOpen("js code")` | Raw JS executed when connection opens |
| `.OnError("js code")` | Raw JS executed on connection error |
| `.On("eventName", e => e.Bind(...))` | Bindings for a named SSE event |

#### Full example

```html
@using ModuWeb.Extensions

@(Sse.Stream("time-stream")
    .Bind("#serverTime", "time")
    .Bind("#lastUpdate", "datetime", "Updated: {0}")
    .OnOpen("document.getElementById('status').textContent='Connected'")
    .OnError("document.getElementById('status').textContent='Reconnecting...'")
    .Render())
```

> **Note:** Always wrap in `@(...)` for multi-line expressions. `Render()` returns raw HTML that won't be escaped by Razor.

### Using jQuery instead of SSE

SSE is optional. ModuWeb ships with jQuery (`/jquery-4.0.0.js`) available for all modules. You can use classic polling with `$.get` / `$.ajax` if you prefer — or combine both approaches in the same project:

```html
<script src="/jquery-4.0.0.js"></script>
<script>
    function updateTime() {
        $.get('time').done(function (data) {
            $('#serverTime').text(data.time);
            $('#lastUpdate').text('Updated: ' + data.datetime);
        });
    }
    setInterval(updateTime, 1000);
    updateTime();
</script>
```

---

## 📌 Notes

- Dependencies belong in `modules/dependencies/`; they are loaded from there and are not copied into temporary module packages.
- A shared DLL has one active generation per assembly simple name. Keep DTOs and shared `static` state only in such dependencies, not in module DLLs.
- Replacing a shared dependency creates a new generation for its affected consumers. Objects from the previous generation must not be retained and cast by modules that were reloaded to the new generation.
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
