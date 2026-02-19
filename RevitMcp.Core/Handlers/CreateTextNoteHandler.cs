using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMcp.Core.Commands;
using RevitMcp.Core.Messages;

namespace RevitMcp.Core.Handlers;

/// <summary>
/// Handles the <see cref="CommandNames.CreateTextNote"/> command.
/// Creates a text note annotation in a specified view.
/// </summary>
/// <remarks>
/// Expected payload properties:
/// <list type="bullet">
///   <item><c>viewId</c> (long, required) – Element ID of the view.</item>
///   <item><c>x</c> (double, required) – X coordinate in decimal feet.</item>
///   <item><c>y</c> (double, required) – Y coordinate in decimal feet.</item>
///   <item><c>text</c> (string, required) – The text content.</item>
///   <item><c>textNoteTypeName</c> (string, optional) – TextNoteType name.</item>
/// </list>
/// </remarks>
public sealed class CreateTextNoteHandler : ICommandHandler
{
    /// <inheritdoc />
    public string Command => CommandNames.CreateTextNote;

    /// <inheritdoc />
    public BridgeResponse Handle(BridgeRequest request, UIDocument uiDoc)
    {
        try
        {
            var doc = uiDoc.Document;

            // --- Extract required parameters ---
            if (request.Payload?.TryGetProperty("viewId", out var viewIdProp) != true)
                return new BridgeResponse(Success: false, Error: "Missing required parameter: viewId");
            if (request.Payload?.TryGetProperty("x", out var xProp) != true)
                return new BridgeResponse(Success: false, Error: "Missing required parameter: x");
            if (request.Payload?.TryGetProperty("y", out var yProp) != true)
                return new BridgeResponse(Success: false, Error: "Missing required parameter: y");
            if (request.Payload?.TryGetProperty("text", out var textProp) != true)
                return new BridgeResponse(Success: false, Error: "Missing required parameter: text");

            var viewId = new ElementId(viewIdProp.GetInt64());
            var x = xProp.GetDouble();
            var y = yProp.GetDouble();
            var text = textProp.GetString();

            if (string.IsNullOrEmpty(text))
                return new BridgeResponse(Success: false, Error: "text cannot be empty.");

            var textNoteTypeName = request.Payload?.TryGetProperty("textNoteTypeName", out var tntProp) == true
                ? tntProp.GetString() : null;

            // --- Verify the view exists and is a valid view ---
            var viewElement = doc.GetElement(viewId);
            if (viewElement is not View view)
                return new BridgeResponse(Success: false,
                    Error: $"View not found or element {viewIdProp.GetInt64()} is not a view.");

            if (view.IsTemplate)
                return new BridgeResponse(Success: false, Error: "Cannot place text notes in a view template.");

            // --- Find TextNoteType ---
            using var tntCollector = new FilteredElementCollector(doc);
            var textNoteTypes = tntCollector.OfClass(typeof(TextNoteType)).Cast<TextNoteType>().ToList();

            if (textNoteTypes.Count == 0)
                return new BridgeResponse(Success: false, Error: "No text note types found in the project.");

            TextNoteType textNoteType;
            if (!string.IsNullOrEmpty(textNoteTypeName))
            {
                textNoteType = textNoteTypes.FirstOrDefault(t =>
                    string.Equals(t.Name, textNoteTypeName, StringComparison.OrdinalIgnoreCase))!;

                if (textNoteType is null)
                {
                    var available = string.Join(", ", textNoteTypes.Select(t => t.Name).Take(30));
                    return new BridgeResponse(Success: false,
                        Error: $"Text note type not found: '{textNoteTypeName}'. Available: {available}");
                }
            }
            else
            {
                textNoteType = textNoteTypes.First();
            }

            // --- Create the text note ---
            var position = new XYZ(x, y, 0);

            using var transaction = new Transaction(doc, "MCP: Create Text Note");
            transaction.Start();

            try
            {
                var textNote = TextNote.Create(doc, view.Id, position, text, textNoteType.Id);

                transaction.Commit();

                var result = new
                {
                    Id = textNote.Id.Value,
                    ViewId = view.Id.Value,
                    ViewName = view.Name,
                    Text = text,
                    TextNoteTypeName = textNoteType.Name
                };

                var data = JsonSerializer.SerializeToElement(result);
                return new BridgeResponse(Success: true, Data: data);
            }
            catch (Exception ex)
            {
                if (transaction.GetStatus() == TransactionStatus.Started)
                    transaction.RollBack();
                return new BridgeResponse(Success: false, Error: $"Failed to create text note: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            return new BridgeResponse(Success: false, Error: ex.Message);
        }
    }
}
