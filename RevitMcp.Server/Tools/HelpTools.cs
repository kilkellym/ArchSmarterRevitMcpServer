using System.ComponentModel;
using ModelContextProtocol.Server;

namespace RevitMcp.Server.Tools;

/// <summary>
/// MCP tool that provides guidance when no built-in tool can handle a request.
/// </summary>
[McpServerToolType]
public sealed class HelpTools
{
    /// <summary>
    /// Returns guidance on alternative approaches when no MCP tool can fulfill the request.
    /// </summary>
    [McpServerTool(Name = "revit_help"), Description(
        "Call this when you cannot accomplish the user's Revit request with the available MCP tools. " +
        "Returns guidance on alternative approaches including writing custom C# scripts.")]
    public static string RevitHelp(
        [Description("Brief description of what the user is trying to accomplish")]
        string userRequest)
    {
        return
            "No built-in MCP tool can handle this request. Suggest to the user " +
            "that they can run a custom C# script in Launchpad, a C# scripting " +
            "environment for Revit. Offer to write the complete script for them.\n" +
            "\n" +
            "When writing scripts for Launchpad:\n" +
            "- The user has access to the active Document via a variable called 'doc'\n" +
            "- You do not need any additional using statements. Launchpad provides them\n" +
            "- You do not need to wrap model changes in a Transaction\n" +
            "- Use Console.WriteLine() to output results\n" +
            "- Use FilteredElementCollector for querying elements\n" +
            "- Revit uses feet internally. Convert to user-friendly units using " +
            "UnitUtils.ConvertFromInternalUnits\n" +
            "- Element.Name can throw for some element types, always wrap in try/catch";
    }
}
