using Autodesk.Revit.UI;
using RevitMcp.Core.Messages;

namespace RevitMcp.Core.Handlers;

/// <summary>
/// Optional capability a command handler can implement to run when no document
/// is open in Revit. The default <see cref="ICommandHandler"/> path requires an
/// active <see cref="UIDocument"/>; handlers that implement this interface are
/// dispatched through <see cref="HandleWithoutDocument"/> instead when the
/// user has Revit running but no project or family loaded.
/// </summary>
/// <remarks>
/// Useful for diagnostic tools like <c>ping_revit</c> that need to report state
/// regardless of whether a document is open. Handlers should still implement
/// <see cref="ICommandHandler.Handle"/> for the normal path.
/// </remarks>
public interface INoDocumentCommandHandler
{
    /// <summary>
    /// Executes the command using only the <see cref="UIApplication"/>, without
    /// requiring an active document. Called on Revit's main thread.
    /// </summary>
    BridgeResponse HandleWithoutDocument(BridgeRequest request, UIApplication app);
}
