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

### Category guard for category-specific tools

Some tools only make sense for specific family categories. Host parameter operations apply only to wall hosted, face hosted, or ceiling hosted families. Door and window specific parameters apply only to those categories.

Add a companion helper `EnsureFamilyCategory(Document doc, params BuiltInCategory[] allowed)` in `FamilyEditorHandler`. Tools that require a specific category call this guard after `EnsureFamilyDocument`. Failing in the handler with a clear message is better than letting Revit throw a surprising exception deep in the API.

### Return structured state

Every tool response should include enough state for the next tool call to be smart. If a tool creates an element, return the new element ID and any warnings Revit raised. If a tool modifies a parameter, return the old and new values. Do not return bare success or failure.

### Transaction naming and grouping

Each write operation wraps in a Transaction named `MCP: [Action]`. This means each tool call is independently reversible via Ctrl Z in Revit.

For multi step operations (create three extrusions and constrain them together), document that the user will see each step as a separate undo entry. A future batch may add `begin_session` and `end_session` wrappers that group a sequence of tool calls into a single TransactionGroup for a cleaner undo experience. Out of scope for v1.

## Full Roadmap

We are building these tools in six batches. This master prompt only sets context. Each batch has its own prompt file and should be built and tested before moving to the next.

### Batch 1: Read tools and foundation

- `is_family_document`
- `get_family_info` (must include family category and host type)
- `list_family_elements`
- `get_reference_planes`
- `get_parameters`
- `list_materials`
- `get_active_view`

These are all read only. No transactions. Establishes the guard pattern and the structured response shape that later batches follow.

### Batch 2: Flex loop and parameter management

- `flex_family` (runs regen, returns any errors or constraint failures)
- `set_parameter_value` (sets a family parameter's value; what flex_family operates on)
- `create_parameter`
- `modify_parameter` (rename, change group, toggle instance or type, change formula)
- `delete_parameter`

This batch delivers the core parametric iteration loop. Create a parameter, set its value, flex, observe, adjust. Without all five tools the loop has gaps.

### Batch 3: Parametric tools

- `create_dimension`
- `lock_dimension`
- `associate_dimension_with_parameter`
- `create_formula`
- `align_geometry_to_reference_plane`
- `lock_alignment`

The alignment tools are as important as the dimension tools. Dimensions drive parameter values; alignments keep geometry attached to the parametric skeleton when those values change. A family with dimensions but no alignments will have geometry that floats away from its reference planes during flexing.

### Batch 4: Geometry

- `create_reference_plane`
- `create_reference_line` (different from reference_plane; needed for angular parametric geometry)
- `set_work_plane` (sets the active sketch plane before creating sketch based geometry)
- `create_extrusion` (takes an `is_void` flag)
- `create_sweep` (takes an `is_void` flag)
- `create_revolve` (takes an `is_void` flag)
- `create_model_line` (used as sweep paths and general reference geometry)
- `create_symbolic_line` (drives 2D representations in project plan, section, and elevation views)
- `edit_sketch` (modify the profile of an existing extrusion, sweep, or revolve)
- `delete_element` (geometry, reference planes, dimensions, anything with an ElementId)
- `set_subcategory`
- `assign_material_to_geometry`

Void support is a flag on the creation tools rather than separate tools because the downstream logic is nearly identical. The `edit_sketch` and `delete_element` tools are what make this batch usable for real iteration rather than just one shot creation.

### Batch 5: Types and catalogs

- `set_family_category_and_parameters` (changes the family category; drives which built in parameters exist)
- `create_type`
- `set_type_parameter`
- `list_types`
- `import_type_catalog`

`set_family_category_and_parameters` comes first in this batch because the category determines what built in type parameters are available.

### Batch 6: Loading, saving, validation

- `load_family_from_rfa`
- `save_family`
- `load_into_project`
- `reload_family`
- `validate_family`
- `list_warnings`

## Explicitly Out of Scope for v1

Documented here so the boundaries are clear, not because these are unimportant:

- **Shared parameters.** Required for project wide scheduling consistency. Adds complexity around the shared parameter file. Defer to v2.
- **Nested families.** Loading a hosted family into another family (a hinge inside a cabinet, a handle inside a drawer). Defer to v2.
- **Visibility settings.** Detail level toggles (coarse, medium, fine) and view type visibility (plan, elevation, section, 3D). Defer to v2.
- **Family environment undo grouping.** The `begin_session` and `end_session` wrappers described above. Defer to v2.
- **Family templates.** Starting a new family from a specific RFT template. May need to be added earlier if users want "create a new casework family from scratch" workflows.

## Principles

1. Follow the existing codebase conventions as documented in `creating-new-tool-guide.md`
2. All units are decimal feet (Revit internal units). No conversion in handlers.
3. Write operations require a Transaction named `MCP: [Action]`
4. All FamilyEditor tools guard on IsFamilyDocument first, except `is_family_document` itself
5. Category specific tools add a second guard via `EnsureFamilyCategory`
6. Return structured state, never bare success or failure
7. Every creation tool returns the new ElementId and any Revit warnings
8. Every modification tool returns both the old and new values
9. Update all four files for every new tool: CommandNames, Handler, Tool, App.cs registration
