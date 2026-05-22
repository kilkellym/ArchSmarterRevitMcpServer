# Build Your Own Revit MCP Server

A hands-on guide to forking, extending, and making this MCP server your own using Claude Code.

## What This Is

This repo is an MCP (Model Context Protocol) server that connects Autodesk Revit to AI assistants like Claude. MCP is a standard that lets AI tools call into desktop applications through a structured set of "tools" -- think of them as API endpoints that Claude can call on your behalf.

The server already ships with 30+ tools for querying elements, creating views, managing sheets, modifying parameters, and more. But the real point of this tutorial is not the tools that exist. It is the pattern they follow, and how you can use Claude Code to generate new tools in minutes instead of hours.

Here is the core idea: you describe what you want a tool to do in plain English, Claude Code writes the C# across four files, you build, and you test. The architecture is designed to make that loop fast.

## How the Architecture Works

You do not need to understand every line of the server code to add tools. But you do need a mental model of how a request flows from Claude to Revit and back. Here is the short version:

```
Claude Desktop / VS Code
        |
        | stdio (JSON-RPC)
        v
  RevitMcp.Server          Named Pipe           RevitMcp.Addin
  (Console App)    <-- "revit-mcp-bridge" -->   (Revit Add-in)
                     length-prefixed JSON
                                                      |
                                                 Revit API calls
                                                 (main thread only)
```

**RevitMcp.Server** is a standalone console app. Claude Desktop launches it automatically. It defines the tools that Claude sees and sends requests over a named pipe to Revit.

**RevitMcp.Addin** loads inside Revit. It listens on the named pipe, receives requests, and runs Revit API code on Revit's main thread using an `ExternalEvent`.

**RevitMcp.Core** holds the shared contracts: command name constants, message types, and handler implementations. Both the server and the add-in reference this project.

The named pipe bridge, the threading model, the message framing -- all of that is already built. You will not touch any of it when adding tools. Your work stays in four specific files, every time.

## Prerequisites

Before starting, make sure you have:

- **Autodesk Revit 2025** (or newer) installed
- **.NET 8 SDK** -- [download here](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Visual Studio 2022** (17.8 or newer, for .NET 8 support)
- **Claude Desktop** -- [download here](https://claude.ai/download)
- **Claude Code** -- install via `npm install -g @anthropic-ai/claude-code`
- **Git** installed and configured

You should be comfortable with C#, Visual Studio, and the basics of Revit add-in development. If you have taken the Revit Add-in Academy, you have more than enough background.

You do **not** need to understand MCP, named pipes, JSON-RPC, or the stdio transport. The architecture handles all of that.

## Step 1: Fork the Repository

Forking creates your own copy of the repo on GitHub. You will make all your changes in your fork, which keeps your work separate from the original repo while still letting you pull in updates later.

1. Go to [https://github.com/ArchSmarter/ArchSmarterRevitMcpServer](https://github.com/ArchSmarter/ArchSmarterRevitMcpServer)
2. Click the **Fork** button in the upper right corner of the page
3. GitHub will ask where to create the fork. Select your personal account.
4. Wait a few seconds. GitHub creates a copy of the repo under your account at `https://github.com/YOUR-USERNAME/ArchSmarterRevitMcpServer`

You now have your own version of the repo. Any changes you push go to your fork, not to the original.

## Step 2: Clone Your Fork Locally

Cloning downloads your forked repo to your local machine so you can build and modify it.

Open a terminal (Command Prompt, PowerShell, or Git Bash) and navigate to the folder where you keep your projects. Then run:

```bash
git clone https://github.com/YOUR-USERNAME/ArchSmarterRevitMcpServer.git
```

Replace `YOUR-USERNAME` with your actual GitHub username. This creates a folder called `ArchSmarterRevitMcpServer` with the full repo contents.

Navigate into the folder:

```bash
cd ArchSmarterRevitMcpServer
```

You can also clone using Visual Studio if you prefer a GUI. Go to **File > Clone Repository**, paste your fork's URL, choose a local path, and click **Clone**.

At this point, your local copy is linked to your fork on GitHub. When you commit and push changes, they go to your fork. You can verify this by running:

```bash
git remote -v
```

You should see your fork's URL listed as `origin`.

## Step 3: Build and Verify

Open the solution in Visual Studio or build from the command line:

```bash
dotnet build ArchSmarterRevitMcpServer.sln -c "Debug R25"
```

This builds three projects:

- **RevitMcp.Core** -- shared library
- **RevitMcp.Server** -- the MCP server console app
- **RevitMcp.Addin** -- the Revit add-in

The add-in's post-build step copies itself to Revit's add-in folder automatically:

```
%APPDATA%\Autodesk\REVIT\Addins\2025\RevitMcp.Addin\
```

## Step 4: Configure Claude Desktop

Add the server to your Claude Desktop config file. On Windows, this lives at:

```
%APPDATA%\Claude\claude_desktop_config.json
```

Add this entry (adjust the path to match where you cloned the repo):

```json
{
  "mcpServers": {
    "revit-mcp": {
      "command": "dotnet",
      "args": ["run", "--project", "C:\\path\\to\\RevitMcp.Server\\RevitMcp.Server.csproj"]
    }
  }
}
```

## Step 5: Test the Connection

1. Open Revit. The add-in loads automatically and starts listening on the named pipe. You should see a **Revit MCP** panel on the ribbon with a status button.
2. Open Claude Desktop. It spawns the MCP server process when you first use a tool.
3. In Claude Desktop, try: "What project do I have open in Revit?"

If Claude responds with your project's name, number, and metadata, the full pipeline is working. The ribbon status button should be green.

If it does not work, click the **MCP Status** button on the ribbon to see what is happening. Common issues:

| Symptom | Fix |
|---------|-----|
| Status button stays red | The pipe server did not start. Check the Output window in VS for errors during add-in load. |
| Claude says "no tools available" | The server is not configured in `claude_desktop_config.json`, or the path is wrong. |
| Tool calls time out | Revit is busy (modal dialog open, loading a model). Close any dialogs and try again. |

## The Four-File Pattern

Every tool in this server touches exactly four files. No exceptions. Understanding this pattern is the key to everything that follows.

### File 1: Command Name Constant

`RevitMcp.Core/Commands/CommandNames.cs`

This file holds a string constant for every command the server can send to Revit. It is the shared identifier that ties the MCP tool to its handler.

```csharp
/// <summary>Gets metadata about the active Revit project.</summary>
public const string GetProjectInfo = "get_project_info";
```

### File 2: Command Handler

`RevitMcp.Core/Handlers/YourHandler.cs`

This is where the Revit API logic lives. A handler class implements `ICommandHandler` and has a `Handle` method that receives a `BridgeRequest` and returns a `BridgeResponse`. It runs on Revit's main thread, so it has full access to the Revit API.

Here is a simplified version of the project info handler:

```csharp
public sealed class GetProjectInfoHandler : ICommandHandler
{
    public string Command => CommandNames.GetProjectInfo;

    public BridgeResponse Handle(BridgeRequest request, UIDocument uiDoc)
    {
        var doc = uiDoc.Document;
        var info = doc.ProjectInformation;

        var result = new
        {
            Name = info.Name,
            Number = info.Number,
            ClientName = info.ClientName,
            Address = info.Address
        };

        var data = JsonSerializer.SerializeToElement(result);
        return new BridgeResponse(Success: true, Data: data);
    }
}
```

A few things to notice:

- The `Command` property returns the constant from `CommandNames.cs`. This is how the registry routes requests to the right handler.
- The `Handle` method receives a `UIDocument`, giving you access to `Document`, `Selection`, and everything else in the Revit API.
- Parameters from Claude arrive in `request.Payload` as a `JsonElement`. You extract them with `TryGetProperty`.
- Results go back as serialized JSON in a `BridgeResponse`.
- If something goes wrong, return `new BridgeResponse(Success: false, Error: "your message")`.

### File 3: MCP Tool Definition

`RevitMcp.Server/Tools/YourTools.cs`

This is the MCP-facing side. It defines what Claude sees: the tool name, the description, the parameters, and the call to send the request over the bridge.

```csharp
[McpServerToolType]
public sealed class ProjectTools
{
    [McpServerTool(Name = "get_project_info"), Description(
        "Get metadata about the active Revit project including project name, number, " +
        "client, address, building name, author, organization, and status. " +
        "Use this to orient yourself about the current model.")]
    public static async Task<string> GetProjectInfo(
        RevitBridgeClient bridgeClient,
        CancellationToken cancellationToken = default)
    {
        var request = new BridgeRequest(Command: CommandNames.GetProjectInfo);
        var response = await bridgeClient.SendAsync(request, cancellationToken);

        if (!response.Success)
            return $"Error: {response.Error}";

        return response.Data?.GetRawText() ?? "No data returned.";
    }
}
```

The `[Description]` attribute is the most important thing here. It is how Claude decides whether to call this tool and what arguments to pass. A vague description leads to Claude calling the wrong tool or skipping it entirely. Be specific about what the tool does, what it returns, and when to use it versus alternatives.

For tools with parameters, each parameter gets its own `[Description]` attribute:

```csharp
[Description("Built-in category name to filter by (e.g. 'Walls', 'Doors'). Omit to return all categories.")]
string? category = null,
```

### File 4: Handler Registration

`RevitMcp.Addin/App.cs`

In the `OnStartup` method, every handler is registered in the `HandlerRegistry`. Add your new handler to the array:

```csharp
var registry = new HandlerRegistry(new ICommandHandler[]
{
    new GetElementsHandler(),
    new GetProjectInfoHandler(),
    new YourNewHandler(),  // add it here
    // ... other handlers
});
```

That is it. Four files, every time. The pattern does not change whether you are building a simple read tool or a complex write tool with transactions.

## Adding Your First Tool with Claude Code

Now for the part that makes this practical. Instead of writing all four files by hand, you describe what you want and let Claude Code generate the code.

### Set Up Claude Code

Navigate to your cloned repo in a terminal and start Claude Code:

```bash
cd C:\path\to\ArchSmarterRevitMcpServer
claude
```

Claude Code reads the `CLAUDE.md` file at the repo root automatically. That file contains the project structure, build commands, coding conventions, and Revit API gotchas. It also picks up the `creating-new-tools-guide.md` file, which has the detailed pattern documentation. Together, these files give Claude Code enough context to generate correct tools without you having to explain the architecture every time.

### Your First Prompt

Let's add a tool that returns all levels in the active Revit model. This is a simple read tool with no parameters and no transactions, which makes it a good first exercise.

Paste this into Claude Code:

```
Add a new MCP tool called get_levels. Follow the same pattern as the existing
tools (CommandNames constant, Handler in Core, Tool in Server, registration
in App.cs).

Tool description: "Get all levels in the active Revit model with their names,
elevations in decimal feet, and element IDs. Returns levels sorted by
elevation from lowest to highest. Use this to discover available levels
before creating views, placing elements, or setting level-based parameters."

Parameters: None

The handler should:
- Use FilteredElementCollector with OfClass(typeof(Level))
- Cast results to Level
- For each level, return: Id (long), Name (string), Elevation (double in
  decimal feet, rounded to 4 decimal places)
- Sort results by elevation ascending
- No transaction required

Put the handler in a new file: RevitMcp.Core/Handlers/GetLevelsHandler.cs
Add the tool method to the existing ModelTools.cs file in RevitMcp.Server/Tools/
Update all four files.
```

### What Claude Code Produces

Claude Code will create or modify the four files:

1. Add `public const string GetLevels = "get_levels";` to `CommandNames.cs`
2. Create `GetLevelsHandler.cs` with the collector, sorting, and response serialization
3. Add a `GetLevels` method to `ModelTools.cs` with the `[McpServerTool]` and `[Description]` attributes
4. Add `new GetLevelsHandler()` to the registry in `App.cs`

### What to Check Before You Trust the Output

Claude Code is good at this pattern, but review the output before building:

**Check the handler.** Make sure it wraps `FilteredElementCollector` in a `using` statement or calls `.ToList()`. Collectors are `IDisposable` and can cause issues if left open. Also verify that `Element.Name` accesses are wrapped in try/catch, since some element types throw on that property.

**Check the tool description.** Read it as if you were Claude deciding whether to call this tool. Does it say what the tool returns? Does it say when to use it? If the description is vague, rewrite it. This is the one thing that directly affects how well Claude uses the tool.

**Check the command name.** Make sure the constant in `CommandNames.cs`, the `Command` property in the handler, and the `Command` in the `BridgeRequest` all use the exact same string.

**Check the registration.** Open `App.cs` and confirm your handler is in the array. Claude Code sometimes creates the handler but forgets to register it.

### Build and Test

Build the solution:

```bash
dotnet build ArchSmarterRevitMcpServer.sln -c "Debug R25"
```

Fix any build errors. Then test:

1. Start Revit (or restart it if it was already running, so it picks up the new add-in DLL)
2. Open a model
3. In Claude Desktop, ask: "What levels are in this model?"

Claude should call your new `get_levels` tool and return the level names and elevations.

### Testing with MCP Inspector

For more precise testing, use the MCP Inspector:

```bash
npx @modelcontextprotocol/inspector
```

Point it at your server executable, connect, and go to the Tools tab. Click "List Tools" to verify your new tool appears with the correct name, description, and parameter schema. You can fill in parameters manually and click "Run Tool" to see the raw response.

The Inspector is especially useful when a tool is not showing up or returning unexpected data, since it shows you exactly what the MCP layer is doing without Claude's interpretation in the way.

## Writing Good Prompts for Tool Generation

The quality of what Claude Code produces depends entirely on the quality of your prompt. Here is what makes the difference.

### The Prompt Template

```
Add a new MCP tool called [tool_name]. Follow the same pattern as the existing
tools (CommandNames constant, Handler in Core, Tool in Server, registration
in App.cs).

Tool description: "[full description following the rules below]"

Parameters:
- paramName (type, required/optional): "description"
- paramName (type, optional, default value): "description"

The handler should:
[Specific Revit API classes and methods to use]
[What to return in the response]
[Whether a Transaction is needed]
[Edge cases and error handling]

Put the handler in [specific file path].
Add the tool method to [specific tool class].
Update all four files.
```

### Be Specific About Revit API Classes

Do not say "get all the walls." Say "Use `FilteredElementCollector` with `OfClass(typeof(Wall))` and cast to `Wall`." Claude Code knows the Revit API, but giving it the specific classes reduces the chance of it hallucinating method names or using deprecated APIs.

### Specify Units

All tool inputs and outputs use decimal feet (Revit internal units). No conversion happens in handlers. State this explicitly when relevant: "Return the wall length from `line.Length` in decimal feet, rounded to 4 decimal places."

### State Whether a Transaction Is Needed

Any tool that modifies the model needs a `Transaction`. Tell Claude Code the transaction name pattern: `"MCP: [Action Description]"`. Read-only tools do not need transactions.

### Mention Edge Cases

The Revit API has quirks that Claude Code will not always anticipate:

- `Element.Name` throws for some element types. Always wrap in try/catch.
- `FilteredElementCollector` is `IDisposable`. Wrap in `using` or call `.ToList()`.
- `Element.LevelId` can be null or `InvalidElementId`. Check before using.
- `Parameter.Set()` throws if you pass the wrong `StorageType`. Check `StorageType` first.
- `Element.Id.Value` replaces `Element.Id.IntegerValue` in Revit 2025+.

Include the relevant warnings in your prompt when they apply.

### Description Writing Rules

The tool description in the `[Description]` attribute is how Claude decides what to call. Follow this template:

```
[verb] [what] from/in the active Revit model [filtered/scoped by what].
Returns [what fields/data].
[When to use this vs other tools].
[Any important caveats].
```

Good: "Get all parameters and their values for a specific Revit element. Returns parameter name, value, storage type (String, Integer, Double, ElementId), and whether it is a type or instance parameter. Use this after get_elements or get_element_by_id to inspect an element's properties."

Bad: "Gets parameters for an element."

## Example: A Write Tool

Read tools are straightforward. Write tools add a Transaction and usually a `confirm` parameter for safety. Here is a prompt for a tool that renames views:

```
Add a new MCP tool called rename_view. Follow the same pattern as the existing
tools (CommandNames constant, Handler in Core, Tool in Server, registration
in App.cs).

Tool description: "Rename a Revit view by its element ID. Requires the view ID
and the new name. Returns the old name and new name for confirmation. If the
new name is already taken by another view, returns an error with the conflicting
view's ID. This modifies the model and requires a Transaction."

Parameters:
- viewId (long, required): "The Revit element ID of the view to rename."
- newName (string, required): "The new name for the view."

The handler should:
- Get the element by viewId, verify it is a View (not null, cast to View)
- Return error if the element is not a View
- Return error if the view is a template (view.IsTemplate)
- Check for name conflicts: use FilteredElementCollector with OfClass(typeof(View)),
  filter to views where Name equals newName (case insensitive), exclude the
  current view ID from the check
- If a conflict exists, return error with the conflicting view's ID
- Capture the old name before modifying
- Wrap the rename in a Transaction named "MCP: Rename View"
- Set view.Name = newName inside the transaction
- Return: ViewId (long), OldName (string), NewName (string)

Put the handler in a new file: RevitMcp.Core/Handlers/RenameViewHandler.cs
Add the tool method to the existing ViewTools.cs file.
Update all four files.
```

Notice the prompt covers the happy path, the error cases, and the transaction. The more specific you are, the less you have to fix after Claude Code generates the code.

## Organizing Your Tools

The existing tools are grouped by domain:

| Domain | Tool Class | Examples |
|--------|-----------|----------|
| Elements | ElementTools.cs | get_elements, set_parameter, delete_elements |
| Views | ViewTools.cs | open_view, create_plan_view |
| Sheets | SheetTools.cs | create_sheet, add_view_to_sheet |
| Model | ModelTools.cs | analyze_model_statistics, get_warnings |
| Rooms | RoomTools.cs | export_room_data |
| Creation | CreationTools.cs | create_wall, create_floor |
| Parameters | ParameterTools.cs | get_parameter_value |
| Families | FamilyTools.cs | insert_family_instance_by_point |
| Family Editor | FamilyEditorTools.cs | is_family_document, get_family_info |
| Diagnostics | DiagnosticTools.cs | ping_revit |
| Launchpad | LaunchpadTools.cs | push_script |

When adding tools, put them in the class that fits their domain. If your tool does not fit an existing group, create a new tool class and a corresponding handler class. Tell Claude Code which file to use in your prompt so it does not guess.

## The CLAUDE.md and creating-new-tools-guide.md Files

These two files at the repo root are not just documentation. They are context files that Claude Code reads automatically when you start a session in the repo.

**CLAUDE.md** contains the project structure, build commands, coding conventions, threading rules, and serialization patterns. It keeps Claude Code from making basic mistakes like using Newtonsoft.Json (the project uses System.Text.Json) or forgetting to wrap Revit API calls in the main thread pattern.

**creating-new-tools-guide.md** has the detailed four-file pattern, the prompt template, a checklist, and common issues. It is essentially a more detailed version of this tutorial's pattern section, written specifically for Claude Code to reference.

When you fork the repo, these files come with it. As you add your own conventions or project-specific patterns, update them. The better these context files are, the better Claude Code's output will be.

## The Escape Hatch: push_script

Not everything needs to be an MCP tool. The `push_script` tool lets Claude write a C# script and send it to Launchpad, the scripting environment inside Revit. The script opens automatically in the Launchpad editor for you to review and run.

This is useful for one-off tasks that do not justify a permanent tool: renaming 200 elements based on a rule, exporting data in a specific format, fixing a batch of parameter values. If you find yourself using `push_script` for the same task repeatedly, that is a signal to turn it into a proper MCP tool.

## Ideas for Tools to Build

Here are some practical tools to try after your first one. They are ordered roughly by complexity:

**Read tools (no Transaction)**
- Get all worksets and their open/closed status
- Get all linked models with their file paths and load status
- List all view templates with their applied settings
- Export a schedule's data as structured JSON
- Get all line styles in the document

**Write tools (Transaction required)**
- Set a view's scale
- Set a view's detail level
- Apply a view template to a view
- Create a new workset
- Copy parameter values from one element to another

**Advanced tools**
- Export a view to an image file
- Create a filter and apply it to a view
- Generate a room finish schedule with custom fields
- Bulk rename sheets by pattern

Pick one that solves a real problem in your workflow. That is always more motivating than a tutorial exercise.

## Troubleshooting

| Problem | What to check |
|---------|--------------|
| Tool does not appear in Claude Desktop | Rebuild the server project. Check that the class has `[McpServerToolType]` and the method has `[McpServerTool]`. |
| Tool appears but returns "Unknown command" | The handler is not registered in `App.cs`, or the command name string does not match between `CommandNames.cs` and the handler's `Command` property. |
| Tool hangs and never returns | Revit has a modal dialog open (save prompt, warning, etc). Close it. Or the pipe connection dropped. Click "Restart Connection" on the ribbon. |
| Tool returns empty data | The `FilteredElementCollector` query is too restrictive. Try removing filters to see if elements exist, then narrow down. |
| Build error about missing Revit API types | Make sure you are building with the `Debug R25` or `Release R25` configuration, not plain `Debug`. |
| Claude calls the wrong tool | The tool description is too vague or overlaps with another tool's description. Rewrite it to be more specific. |

## What to Read Next

- **README.md** in this repo covers the full architecture, all available tools, and setup details.
- **creating-new-tools-guide.md** has the detailed pattern documentation, the prompt checklist, and testing procedures. Read this when you want to go deeper on writing effective prompts for Claude Code.
- **CLAUDE.md** has the coding conventions and Revit API rules that Claude Code follows. Update this file as you add your own patterns.
- The [MCP specification](https://modelcontextprotocol.io/) if you want to understand the protocol itself (optional, not required for adding tools).
