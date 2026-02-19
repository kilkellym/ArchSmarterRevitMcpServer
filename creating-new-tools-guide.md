# Creating New MCP Tools: A Guide for Claude Code

## How a Tool Works

Every MCP tool has three parts that work together:

1. **Tool class** in `RevitMcp.Server/Tools/` defines what the AI sees: the tool name, description, parameters, and how to send the request over the bridge.
2. **Handler class** in `RevitMcp.Core/Handlers/` contains the actual Revit API logic that runs on Revit's main thread.
3. **Command name constant** in `RevitMcp.Core/Commands/CommandNames.cs` ties the tool to its handler with a shared string identifier.

The handler also needs to be **registered** in `RevitMcp.Addin/App.cs` so the add-in knows how to route incoming requests.

---

## The Pattern

Every tool follows the same flow:

```
Claude calls tool
    → MCP Server receives call (Tool class)
    → Serializes parameters into a BridgeRequest
    → Sends over Named Pipe to Revit
    → Add-in routes to correct Handler (via CommandName)
    → Handler runs Revit API code on main thread
    → Returns BridgeResponse with JSON data
    → MCP Server returns result to Claude
```

---

## Files You Touch for Every New Tool

| File | What to add |
|------|-------------|
| `RevitMcp.Core/Commands/CommandNames.cs` | New string constant |
| `RevitMcp.Core/Handlers/YourNewHandler.cs` | New handler class (or add method to existing handler) |
| `RevitMcp.Server/Tools/YourToolClass.cs` | New tool method (or add to existing tool class) |
| `RevitMcp.Addin/App.cs` | Register the handler |

---

## Deciding Where to Put a New Tool

Group tools by domain rather than creating one class per tool:

| Domain | Tool class | Handler class | Example tools |
|--------|-----------|---------------|---------------|
| Elements | ElementTools.cs | ElementHandler.cs | get_elements, count_elements, get_element_by_id |
| Parameters | ParameterTools.cs | ParameterHandler.cs | get_element_parameters, set_parameter |
| Views | ViewTools.cs | ViewHandler.cs | get_views, get_current_view_info |
| Project | ProjectTools.cs | ProjectInfoHandler.cs | get_project_info |
| Rooms | RoomTools.cs | RoomHandler.cs | export_room_data |
| Model | ModelTools.cs | ModelStatisticsHandler.cs | analyze_model_statistics |
| Families | FamilyTools.cs | FamilyHandler.cs | get_families, get_available_family_types |
| Sheets | SheetTools.cs | SheetHandler.cs | create_sheet, place_view_on_sheet |

If a tool doesn't fit an existing group, create a new pair.

---

## Writing Good Tool Descriptions

The tool description is the single most important thing you write. It's how Claude decides which tool to call and what arguments to pass. Bad descriptions lead to Claude calling the wrong tool or passing bad parameters.

### Rules for Descriptions

**Say what the tool does and what it returns.** Not just "Gets elements" but "Get elements from the active Revit model filtered by category. Returns element IDs, names, and basic properties."

**Say when to use it versus alternatives.** "Use this after get_elements to inspect a specific element's properties" or "Faster than get_elements when you only need the count."

**Specify parameter formats.** "Revit category name, e.g., 'Walls', 'Doors', 'Floors'" is better than just "category."

**Mention units if relevant.** "Returns area in square feet" or "Elevation in decimal feet."

### Description Template

```
[verb] [what] from/in the active Revit model [filtered/scoped by what]. 
Returns [what fields/data]. 
[When to use this vs other tools]. 
[Any important caveats].
```

### Examples

Good:
```
"Get all parameters and their values for a specific Revit element. 
Returns parameter name, value, storage type (String, Integer, Double, ElementId), 
and whether it is a type or instance parameter. 
Use this after get_elements or get_element_by_id to inspect an element's properties."
```

Bad:
```
"Gets parameters for an element."
```

---

## Prompting Claude Code

### The Basic Prompt Template

Use this structure when asking Claude Code to create a new tool:

```
Add a new MCP tool called [tool_name]. Follow the same pattern as the existing 
tools (CommandNames constant, Handler in Core, Tool in Server, registration in 
App.cs).

Tool description: "[your description following the rules above]"

Parameters:
- paramName (type, required/optional): "description"
- paramName (type, optional, default value): "description"

The handler should:
[Describe the Revit API logic step by step]
[Mention specific API classes: FilteredElementCollector, Parameter, Transaction, etc.]
[Specify any error handling or edge cases]
[Specify units or conversions needed]
[Mention if a Transaction is required for write operations]
```

### Example Prompt for a Read Tool

```
Add a new MCP tool called get_linked_models. Follow the same pattern as the 
existing tools (CommandNames constant, Handler in Core, Tool in Server, 
registration in App.cs).

Tool description: "Get all linked Revit models in the active project. Returns 
the link name, file path, link status (Loaded, Unloaded, NotFound), and the 
transform (position offset) for each instance. Use this to understand what 
external references exist in the model."

Parameters:
- statusFilter (string, optional): "Filter by link status: 'Loaded', 'Unloaded', 
  or 'NotFound'. Omit to return all."

The handler should:
- Use FilteredElementCollector with OfClass(typeof(RevitLinkInstance))
- For each RevitLinkInstance, get the link type via GetTypeId()
- Cast the type to RevitLinkType to get the file path from 
  ExternalFileReference
- Get the link status from ExternalFileReference.GetLinkedFileStatus()
- Get the transform from RevitLinkInstance.GetTransform() and return the 
  origin as X, Y, Z in decimal feet
- Skip any null references gracefully
- Apply statusFilter if provided
```

### Example Prompt for a Write Tool

```
Add a new MCP tool called set_parameter. Follow the same pattern as the 
existing tools.

Tool description: "Set a parameter value on a Revit element. Requires the 
element ID, the parameter name, and the new value. The value will be converted 
to the appropriate type based on the parameter's StorageType. Returns the 
previous value and the new value for confirmation. This modifies the model 
and requires a Transaction."

Parameters:
- elementId (int, required): "The Revit element ID"
- parameterName (string, required): "The exact parameter name as shown in 
  Revit properties"
- value (string, required): "The new value as a string. Will be parsed to 
  the correct type automatically."

The handler should:
- Get the element by ID, return error if not found
- Find the parameter by name using element.LookupParameter(parameterName)
- Return error if parameter not found or is read-only
- Check the parameter's StorageType and parse the value accordingly:
  - StorageType.String: use value directly
  - StorageType.Integer: parse to int
  - StorageType.Double: parse to double, then convert from user units to 
    internal units using UnitUtils.ConvertToInternalUnits
  - StorageType.ElementId: parse to int, create ElementId
- Wrap the Set call in a Transaction named "MCP: Set Parameter"
- Return the old value and new value in the response
- If parsing fails, return a clear error message explaining the expected format
```

### Tips for Better Claude Code Results

**Be specific about the Revit API classes.** Don't say "get all the rooms." Say "Use FilteredElementCollector with OfCategory(BuiltInCategory.OST_Rooms) and cast to Autodesk.Revit.DB.Architecture.Room." Claude Code knows the Revit API but giving it the specific classes reduces hallucination.

**Mention edge cases explicitly.** "Element.Name can throw for some element types, wrap in try/catch." or "Skip rooms where Location is null (unplaced rooms)." Claude Code won't always anticipate Revit quirks.

**All MCP tool inputs and outputs use decimal feet (Revit internal units).** No unit conversion is needed in handlers. The LLM handles any user-facing unit conversion before calling tools.

**State whether a Transaction is needed.** Any tool that modifies the model needs a Transaction. Tell Claude Code the transaction name pattern: "MCP: [Action Description]".

**Tell it to update all four files.** Claude Code sometimes creates the handler but forgets to register it in App.cs, or creates the constant but not the tool class. Listing all four files in your prompt prevents this.

**Review the generated tool description.** After Claude Code creates the tool, read the description it put in the [Description] attribute. If it's vague, rewrite it. This is the one piece that directly affects how well the AI uses the tool.

---

## Testing a New Tool

### With MCP Inspector

1. Rebuild the server: `dotnet build RevitMcp.Server`
2. Open MCP Inspector: `npx @modelcontextprotocol/inspector`
3. Point it at the server exe
4. Connect, go to Tools, click List Tools
5. Verify the new tool appears with correct name, description, and parameter schema
6. Fill in parameters and click Run Tool
7. Check the response (will timeout if Revit isn't running, which is fine for verifying the MCP layer)

### With Revit Running

1. Rebuild both the server and the add-in
2. Start Revit (add-in loads automatically)
3. Open MCP Inspector or Claude Desktop
4. Call the tool with real parameters
5. Verify the response contains correct data from the model

### Common Issues

| Symptom | Likely cause |
|---------|-------------|
| Tool doesn't appear in List Tools | Missing [McpServerToolType] or [McpServerTool] attribute, or tool class is in wrong assembly |
| Tool appears but returns "Unknown command" | Handler not registered in App.cs, or CommandNames constant doesn't match |
| Tool hangs indefinitely | Pipe connection timeout missing, or handler is deadlocking Revit's main thread |
| Tool returns empty data | FilteredElementCollector query is too restrictive, or wrong BuiltInCategory |
| Tool returns error about thread | Revit API called from background thread instead of through ExternalEvent/Idling |
| Parameter values are wrong magnitude | Tool inputs/outputs should be in decimal feet (Revit internal units) — no conversion needed |

---

## Quick Reference: Prompt Checklist

Before sending your prompt to Claude Code, make sure you've included:

- [ ] Tool name (snake_case, matches MCP convention)
- [ ] Full tool description (what it does, what it returns, when to use it)
- [ ] All parameters with types, required/optional, defaults, and descriptions
- [ ] Specific Revit API classes and methods to use
- [ ] Unit conversions needed
- [ ] Whether a Transaction is required
- [ ] Edge cases and error handling
- [ ] Instruction to update all four files (CommandNames, Handler, Tool, App.cs)
- [ ] Instruction to follow existing patterns in the codebase
