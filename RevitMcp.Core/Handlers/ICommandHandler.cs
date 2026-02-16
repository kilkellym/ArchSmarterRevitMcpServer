using Autodesk.Revit.UI;
using RevitMcp.Core.Messages;

namespace RevitMcp.Core.Handlers;

/// <summary>
/// Interface for handlers that execute Revit commands on the add-in side.
/// Each implementation handles a single command identified by <see cref="Command"/>.
/// Handlers run synchronously on Revit's main thread.
/// </summary>
public interface ICommandHandler
{
    /// <summary>
    /// The command name this handler responds to.
    /// Must match a constant in <see cref="Commands.CommandNames"/>.
    /// </summary>
    string Command { get; }

    /// <summary>
    /// Executes the command and returns a bridge response.
    /// This method is called on Revit's main thread.
    /// </summary>
    /// <param name="request">The incoming bridge request with command parameters.</param>
    /// <param name="uiDoc">The active Revit <see cref="UIDocument"/>, providing access
    /// to both <see cref="UIDocument.Document"/> and <see cref="UIDocument.Selection"/>.</param>
    /// <returns>A response indicating success or failure with optional data.</returns>
    BridgeResponse Handle(BridgeRequest request, UIDocument uiDoc);
}
