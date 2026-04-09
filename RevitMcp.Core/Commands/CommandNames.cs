namespace RevitMcp.Core.Commands;

/// <summary>
/// Constants for all supported command names exchanged over the bridge.
/// Add new entries here when introducing new tools.
/// </summary>
public static class CommandNames
{
    /// <summary>
    /// Retrieves elements from the active Revit document, optionally filtered by category.
    /// </summary>
    public const string GetElements = "get_elements";

    /// <summary>
    /// Gets all parameters and their values for a specific Revit element.
    /// </summary>
    public const string GetElementParameters = "get_element_parameters";

    /// <summary>
    /// Gets metadata about the active Revit project.
    /// </summary>
    public const string GetProjectInfo = "get_project_info";

    /// <summary>
    /// Gets detailed information about a single Revit element by its ID.
    /// </summary>
    public const string GetElementById = "get_element_by_id";

    /// <summary>
    /// Analyzes the overall complexity and composition of the active Revit model.
    /// </summary>
    public const string AnalyzeModelStatistics = "analyze_model_statistics";

    /// <summary>
    /// Gets information about the currently active view in Revit.
    /// </summary>
    public const string GetCurrentViewInfo = "get_current_view_info";

    /// <summary>
    /// Extracts all rooms from the active Revit model with detailed spatial data.
    /// </summary>
    public const string ExportRoomData = "export_room_data";

    /// <summary>
    /// Sets a parameter value on a Revit element within a Transaction.
    /// </summary>
    public const string SetParameter = "set_parameter";

    /// <summary>
    /// Gets the currently selected elements in the active Revit view.
    /// </summary>
    public const string GetSelectedElements = "get_selected_elements";

    /// <summary>Opens a view and makes it the active view.</summary>
    public const string OpenView = "open_view";

    /// <summary>Creates a new floor plan or ceiling plan view.</summary>
    public const string CreatePlanView = "create_plan_view";

    /// <summary>Creates a new elevation view at a specified location.</summary>
    public const string CreateElevationView = "create_elevation_view";

    /// <summary>Creates a new section view defined by a bounding box.</summary>
    public const string CreateSectionView = "create_section_view";

    /// <summary>Creates a new schedule view for a category.</summary>
    public const string CreateScheduleView = "create_schedule_view";

    /// <summary>Creates a new sheet in the project.</summary>
    public const string CreateSheet = "create_sheet";

    /// <summary>Places a view on a sheet by creating a viewport.</summary>
    public const string AddViewToSheet = "add_view_to_sheet";

    /// <summary>Deletes one or more elements from the model.</summary>
    public const string DeleteElements = "delete_elements";

    /// <summary>Gets a single parameter value from an element by parameter name.</summary>
    public const string GetParameterValue = "get_parameter_value";

    /// <summary>Creates a straight wall along a line between two points.</summary>
    public const string CreateWall = "create_wall";

    /// <summary>Creates a text note annotation in a view.</summary>
    public const string CreateTextNote = "create_text_note";

    /// <summary>Inserts a component family instance at a point.</summary>
    public const string InsertFamilyInstanceByPoint = "insert_family_instance_by_point";

    /// <summary>Creates a railing along a path of connected line segments.</summary>
    public const string CreateRailing = "create_railing";

    /// <summary>Inserts a group instance at a point.</summary>
    public const string InsertGroup = "insert_group";

    /// <summary>Creates a dimension between two or more elements in a view.</summary>
    public const string CreateDimension = "create_dimension";

    /// <summary>Creates a floor from a closed boundary polygon.</summary>
    public const string CreateFloor = "create_floor";

    /// <summary>Creates a detail line annotation in a view.</summary>
    public const string CreateDetailLine = "create_detail_line";

    /// <summary>Gets all views placed on a sheet.</summary>
    public const string GetSheetViews = "get_sheet_views";

    /// <summary>Gets elements visible in a specific view, optionally filtered by category and region.</summary>
    public const string GetElementsInView = "get_elements_in_view";

    /// <summary>Finds elements by matching a parameter value.</summary>
    public const string FindElementsByParameter = "find_elements_by_parameter";

    /// <summary>Sets parameter values on multiple elements in a single transaction.</summary>
    public const string BatchSetParameters = "batch_set_parameters";

    /// <summary>Sends a C# script to the Launchpad scripting environment inside Revit.</summary>
    public const string PushScript = "push_script";

    /// <summary>Gets all views placed on a sheet with viewport bounds.</summary>
    public const string GetViewsOnSheet = "get_views_on_sheet";

    /// <summary>Moves one or more elements by a translation vector.</summary>
    public const string MoveElements = "move_elements";

    /// <summary>Searches for elements by name, family name, type name, or mark value.</summary>
    public const string FindElementsByName = "find_elements_by_name";

    /// <summary>Gets a complete mapping of all sheets to their views.</summary>
    public const string GetSheetViewMapping = "get_sheet_view_mapping";

    /// <summary>Gets all warnings (failure messages) in the active Revit document.</summary>
    public const string GetWarnings = "get_warnings";
}
