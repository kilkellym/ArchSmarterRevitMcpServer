using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMcp.Core.Commands;
using RevitMcp.Core.Messages;

namespace RevitMcp.Core.Handlers;

/// <summary>
/// Handles the <see cref="CommandNames.DeleteElements"/> command.
/// Supports a preview mode (confirm=false) that returns element info without deleting,
/// and a delete mode (confirm=true) that actually removes the elements.
/// </summary>
public sealed class DeleteElementsHandler : ICommandHandler
{
    private const int MaxElements = 100;

    /// <inheritdoc />
    public string Command => CommandNames.DeleteElements;

    /// <inheritdoc />
    public BridgeResponse Handle(BridgeRequest request, UIDocument uiDoc)
    {
        try
        {
            var doc = uiDoc.Document;

            if (request.Payload?.TryGetProperty("elementIds", out var idsProp) != true
                || idsProp.ValueKind != JsonValueKind.Array)
                return new BridgeResponse(Success: false, Error: "Missing required parameter: elementIds (array of integers)");

            var elementIds = new List<long>();
            foreach (var idVal in idsProp.EnumerateArray())
                elementIds.Add(idVal.GetInt64());

            if (elementIds.Count == 0)
                return new BridgeResponse(Success: false, Error: "elementIds array cannot be empty.");

            if (elementIds.Count > MaxElements)
                return new BridgeResponse(Success: false,
                    Error: $"Too many elements ({elementIds.Count}). Maximum is {MaxElements} per call.");

            var confirm = request.Payload?.TryGetProperty("confirm", out var confirmProp) == true
                && confirmProp.GetBoolean();

            if (!confirm)
            {
                // Preview mode: return info about what would be deleted
                var previews = new List<object>();
                foreach (var id in elementIds)
                {
                    var elemId = new ElementId(id);
                    var elem = doc.GetElement(elemId);
                    if (elem is null)
                    {
                        previews.Add(new { Id = id, Status = "NotFound" });
                    }
                    else
                    {
                        string? name = null;
                        try { name = elem.Name; } catch { /* some elements throw on Name */ }

                        previews.Add(new
                        {
                            Id = id,
                            Name = name ?? "(unnamed)",
                            Category = elem.Category?.Name ?? "(none)",
                            Status = "WouldDelete"
                        });
                    }
                }

                var previewResult = new
                {
                    Mode = "Preview",
                    Message = "Set confirm=true to actually delete these elements.",
                    Elements = previews
                };

                var previewData = JsonSerializer.SerializeToElement(previewResult);
                return new BridgeResponse(Success: true, Data: previewData);
            }

            // Delete mode
            using var transaction = new Transaction(doc, "MCP: Delete Elements");
            transaction.Start();

            try
            {
                var deleted = new List<object>();
                var errors = new List<object>();

                foreach (var id in elementIds)
                {
                    var elemId = new ElementId(id);
                    var elem = doc.GetElement(elemId);

                    if (elem is null)
                    {
                        errors.Add(new { Id = id, Error = "Element not found" });
                        continue;
                    }

                    string? name = null;
                    try { name = elem.Name; } catch { /* some elements throw on Name */ }
                    var category = elem.Category?.Name ?? "(none)";

                    try
                    {
                        doc.Delete(elemId);
                        deleted.Add(new { Id = id, Name = name ?? "(unnamed)", Category = category });
                    }
                    catch (Exception ex)
                    {
                        errors.Add(new { Id = id, Name = name ?? "(unnamed)", Error = ex.Message });
                    }
                }

                transaction.Commit();

                var result = new
                {
                    Mode = "Delete",
                    DeletedCount = deleted.Count,
                    ErrorCount = errors.Count,
                    Deleted = deleted,
                    Errors = errors
                };

                var data = JsonSerializer.SerializeToElement(result);
                return new BridgeResponse(Success: true, Data: data);
            }
            catch (Exception ex)
            {
                if (transaction.GetStatus() == TransactionStatus.Started)
                    transaction.RollBack();
                return new BridgeResponse(Success: false, Error: $"Failed to delete elements: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            return new BridgeResponse(Success: false, Error: ex.Message);
        }
    }
}
