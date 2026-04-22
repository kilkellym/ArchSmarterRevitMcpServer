# Revit MCP Family Editor Tools: Master Context

## Goal

We are adding a new set of tools to the existing ArchSmarterRevitMcpServer that operate on the Revit Family Editor. The goal is to let Claude create new families from scratch and add features to existing families through natural language, with particular focus on parametric work (dimensions, parameters, formulas, flexing). The JSON plus executor pipeline handles family creation from drawings and photos well, but struggles with parametric behavior. MCP is the better fit for that work because parametric constraints are inherently stateful and need a feedback loop.

## First Step

Before doing anything else, read the file `creating-new-tool-guide.md` at the root of this repo. That guide is the canonical reference for how every tool in this server is structured. All new tools must follow the same pattern it describes: CommandNames constant, Handler in Core, Tool in Server, registration in App.cs.

## Architectural Decisions

### New tool class and handler

The existing `FamilyTools` and `FamilyHandler` are scoped to families within a project (querying loaded families, getting available types). Do not add to those. All new tools go into a new pair:

- `RevitMcp.Server/Tools/FamilyEditorTools.cs`
- `RevitMcp.Core/Handlers/FamilyEditorHandler.cs`

This keeps project operations and family editor operations cleanly split.

### Active document guard

Every tool in FamilyEditorTools only makes sense when the active document is a family document (.rfa). Calling these tools on a project document will produce confusing Revit API errors.

Build a shared guard in `FamilyEditorHandler`. The pattern:

1. Add a private helper method `EnsureFamilyDocument(Document doc)` in FamilyEditorHandler
2. The helper checks `doc.IsFamilyDocument`
3. If false, it returns a structured error response with a clear message: "Active document is not a family document. This tool requires a family (.rfa) to be open in the Family Editor."
4. If true, it returns null
5. Every command handler method in FamilyEditorHandler calls this guard as its first line after getting the document, and returns early on a non null result

The one exception is the `is_family_document` tool itself, which should never fail. It returns true or false so Claude can check context before attempting other operations.

### Return structured state

Every tool response should include enough state for the next tool call to be smart. If a tool creates an element, return the new element ID and any warnings Revit raised. If a tool modifies a parameter, return the old and new values. Do not return bare success or failure.

## Full Roadmap

We are building these tools in six batches. This master prompt only sets context. Each batch has its own prompt file and should be built and tested before moving to the next.

### Batch 1: Read tools and foundation
is_family_document, get_family_info, list_family_elements, get_reference_planes, get_parameters

### Batch 2: Flex loop
flex_family, create_parameter

### Batch 3: Parametric tools
create_dimension, lock_dimension, associate_dimension_with_parameter, create_formula

### Batch 4: Geometry
create_extrusion, create_sweep, create_revolve, create_reference_plane, set_subcategory

### Batch 5: Types and catalogs
create_type, set_type_parameter, list_types, import_type_catalog

### Batch 6: Loading, saving, validation
load_family_from_rfa, save_family, load_into_project, reload_family, validate_family, list_warnings

## Principles

1. Follow the existing codebase conventions as documented in `creating-new-tool-guide.md`
2. All units are decimal feet (Revit internal units). No conversion in handlers.
3. Write operations require a Transaction named `MCP: [Action]`
4. All FamilyEditor tools guard on IsFamilyDocument first, except is_family_document itself
5. Return structured state, never bare success or failure
6. Update all four files for every new tool: CommandNames, Handler, Tool, App.cs registration
