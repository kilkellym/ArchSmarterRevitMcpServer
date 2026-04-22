# Revit MCP Family Editor Tools: Batch 1

## Prerequisites

Read these first, in order:
1. `creating-new-tool-guide.md` at the repo root
2. `master-prompt.md` in this prompts folder

## Scope of Batch 1

Build the foundation: an active document guard and five read tools. After this batch, Claude can connect to a family document and describe what is in it before any write tools exist.

All five tools go into the new `FamilyEditorTools` class and `FamilyEditorHandler` class. Create both classes if they do not exist. Register the handler in `RevitMcp.Addin/App.cs`.

## Guard to Build First

In `FamilyEditorHandler`, add a private helper:

```csharp
private BridgeResponse EnsureFamilyDocument(Document doc)
```

The helper returns a structured error response if `doc.IsFamilyDocument` is false, or null if the check passes. Every command handler method in FamilyEditorHandler except the one backing `is_family_document` calls this as its first line and returns early on a non null result.

---

## Tool 1: is_family_document

**Description**: "Check whether the active Revit document is a family document (.rfa). Returns true or false, and when true also returns the family category name and family name. Call this first before attempting any other family editor tools, since those tools require a family document to be open."

**Parameters**: None

**Handler logic**:
- Do not call EnsureFamilyDocument. This tool is the check.
- Read `doc.IsFamilyDocument`
- If true, get the family category from `doc.OwnerFamily.FamilyCategory.Name` and the family name from `doc.OwnerFamily.Name`. The family name may be empty for unsaved families, which is fine.
- Return: `{ "isFamilyDocument": bool, "category": string or null, "familyName": string or null }`
- No transaction required

## Tool 2: get_family_info

**Description**: "Get top level information about the active family document, including the family category, placement type, parameter count, and type count. Use this after is_family_document returns true to understand what kind of family you are working with before reading elements or parameters in detail."

**Parameters**: None

**Handler logic**:
- Call EnsureFamilyDocument first, return early if it fails
- Get family from `doc.OwnerFamily`
- Get FamilyManager from `doc.FamilyManager`
- Return:
  - `familyName`: doc.OwnerFamily.Name
  - `category`: doc.OwnerFamily.FamilyCategory.Name
  - `placementType`: doc.OwnerFamily.FamilyPlacementType.ToString()
  - `parameterCount`: FamilyManager.Parameters.Size
  - `typeCount`: FamilyManager.Types.Size
  - `currentTypeName`: FamilyManager.CurrentType?.Name or null
- No transaction required

## Tool 3: list_family_elements

**Description**: "List all geometric and reference elements in the active family document, grouped by element type. Returns element IDs, names, and element type (Extrusion, Sweep, Blend, Revolution, ReferencePlane, Dimension). Use this to understand the current geometry of a family before adding, modifying, or flexing anything."

**Parameters**: None

**Handler logic**:
- Call EnsureFamilyDocument first, return early if it fails
- Use FilteredElementCollector on the family document
- Collect the following element classes:
  - `GenericForm` (abstract base class for Extrusion, Sweep, Blend, Revolution)
  - `ReferencePlane`
  - `Dimension`
- For GenericForm, determine the specific subtype by checking the concrete class name (Extrusion, Sweep, Blend, Revolution)
- For each element, return: `id` (int), `name` (string, may be empty), `elementType` (string), `category` (string or null)
- Element.Name can throw for some element types, wrap in try/catch and return empty string on failure
- Group results in the response by elementType
- No transaction required

## Tool 4: get_reference_planes

**Description**: "Get all reference planes in the active family document with their geometry and reference settings. Returns the plane ID, name, bubble end and free end coordinates, normal vector, and whether the plane is named. All coordinates in decimal feet. Use this when you need to dimension to or from reference planes, or before creating new geometry that must align with existing references."

**Parameters**: None

**Handler logic**:
- Call EnsureFamilyDocument first, return early if it fails
- FilteredElementCollector with `OfClass(typeof(ReferencePlane))`
- For each ReferencePlane:
  - `id`: Id.IntegerValue
  - `name`: Name
  - `bubbleEnd`: BubbleEnd as {x, y, z}
  - `freeEnd`: FreeEnd as {x, y, z}
  - `normal`: Normal as {x, y, z}
  - `isNamed`: true if Name is not empty and not a default placeholder like "Reference Plane"
- All coordinates are already in decimal feet (Revit internal units), no conversion needed
- No transaction required

## Tool 5: get_parameters

**Description**: "Get all family parameters in the active family document with their metadata and current values. Returns parameter name, storage type (String, Integer, Double, ElementId), whether the parameter is an instance or type parameter, whether it is shared, its formula if present, and its current value for the active type. Use this before creating new parameters to avoid duplicates, or before associating dimensions to parameters."

**Parameters**: None

**Handler logic**:
- Call EnsureFamilyDocument first, return early if it fails
- Get FamilyManager from `doc.FamilyManager`
- Iterate FamilyManager.Parameters
- For each FamilyParameter, return:
  - `name`: Definition.Name
  - `storageType`: StorageType.ToString()
  - `isInstance`: IsInstance
  - `isShared`: IsShared
  - `formula`: Formula (may be null)
  - `currentValue`: value from FamilyManager.CurrentType using the parameter
- Handle CurrentType being null by returning null for currentValue
- For each StorageType, use the appropriate accessor on CurrentType:
  - String: CurrentType.AsString(param)
  - Integer: CurrentType.AsInteger(param)
  - Double: CurrentType.AsDouble(param), return raw internal unit value (decimal feet for lengths, radians for angles)
  - ElementId: CurrentType.AsElementId(param), return IntegerValue
- No transaction required

---

## Deliverable Checklist

When done:

- [ ] `RevitMcp.Core/Commands/CommandNames.cs` has five new constants
- [ ] `RevitMcp.Core/Handlers/FamilyEditorHandler.cs` exists with the guard helper and five command methods
- [ ] `RevitMcp.Server/Tools/FamilyEditorTools.cs` exists with five tool methods
- [ ] `RevitMcp.Addin/App.cs` registers FamilyEditorHandler and routes the five command names
- [ ] All five tools appear in MCP Inspector with correct descriptions and parameter schemas

## Testing

Follow the testing steps in `creating-new-tool-guide.md`:

1. Build with `dotnet build RevitMcp.Server` and the addin project
2. Verify tools appear in MCP Inspector with correct names and descriptions
3. Open a simple family in Revit (a stock furniture template works fine for smoke testing)
4. Call each tool through MCP Inspector or Claude Desktop
5. Verify responses contain accurate data from the family
6. Open a project document and confirm the guard fires correctly on tools 2 through 5, while is_family_document returns false without error
