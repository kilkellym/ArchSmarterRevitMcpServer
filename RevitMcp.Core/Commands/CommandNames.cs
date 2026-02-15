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
}
