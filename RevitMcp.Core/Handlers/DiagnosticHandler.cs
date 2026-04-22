using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMcp.Core.Commands;
using RevitMcp.Core.Messages;

namespace RevitMcp.Core.Handlers;

/// <summary>
/// Handles the <see cref="CommandNames.PingRevit"/> command. Reports the Revit
/// version and the active document state. Implements
/// <see cref="INoDocumentCommandHandler"/> so it can still report the Revit
/// version when no document is open.
/// </summary>
public sealed class DiagnosticHandler : ICommandHandler, INoDocumentCommandHandler
{
    /// <inheritdoc />
    public string Command => CommandNames.PingRevit;

    /// <inheritdoc />
    public BridgeResponse Handle(BridgeRequest request, UIDocument uiDoc)
    {
        try
        {
            var doc = uiDoc.Document;
            var version = doc.Application.VersionNumber;
            var title = string.IsNullOrEmpty(doc.Title) ? null : doc.Title;
            var documentType = doc.IsFamilyDocument ? "Family" : "Project";

            return Respond(version, title, documentType);
        }
        catch (Exception ex)
        {
            return new BridgeResponse(Success: false, Error: ex.Message);
        }
    }

    /// <inheritdoc />
    public BridgeResponse HandleWithoutDocument(BridgeRequest request, UIApplication app)
    {
        try
        {
            var version = app.Application.VersionNumber;
            return Respond(version, activeDocument: null, documentType: "None");
        }
        catch (Exception ex)
        {
            return new BridgeResponse(Success: false, Error: ex.Message);
        }
    }

    private static BridgeResponse Respond(string? revitVersion, string? activeDocument, string? documentType)
    {
        var payload = JsonSerializer.SerializeToElement(new
        {
            revitVersion,
            activeDocument,
            documentType
        });
        return new BridgeResponse(Success: true, Data: payload);
    }
}
