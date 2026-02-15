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
}
