using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMcp.Core.Commands;
using RevitMcp.Core.Messages;

namespace RevitMcp.Core.Handlers;

/// <summary>
/// Handles the <see cref="CommandNames.CreateScheduleView"/> command.
/// Creates a new schedule view for a specified category, optionally adding fields.
/// </summary>
public sealed class CreateScheduleViewHandler : ICommandHandler
{
    /// <inheritdoc />
    public string Command => CommandNames.CreateScheduleView;

    /// <inheritdoc />
    public BridgeResponse Handle(BridgeRequest request, UIDocument uiDoc)
    {
        try
        {
            var doc = uiDoc.Document;

            if (request.Payload?.TryGetProperty("category", out var catProp) != true)
                return new BridgeResponse(Success: false, Error: "Missing required parameter: category");

            var categoryName = catProp.GetString();
            if (string.IsNullOrEmpty(categoryName))
                return new BridgeResponse(Success: false, Error: "category cannot be empty.");

            var viewName = request.Payload?.TryGetProperty("viewName", out var nameProp) == true
                ? nameProp.GetString() : null;

            // Parse field names if provided
            var fieldNames = new List<string>();
            if (request.Payload?.TryGetProperty("fields", out var fieldsProp) == true
                && fieldsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var f in fieldsProp.EnumerateArray())
                {
                    var name = f.GetString();
                    if (!string.IsNullOrEmpty(name))
                        fieldNames.Add(name);
                }
            }

            // Map category name to BuiltInCategory
            if (!Enum.TryParse<BuiltInCategory>($"OST_{categoryName}", out var builtInCategory))
                return new BridgeResponse(Success: false,
                    Error: $"Unknown category: '{categoryName}'. Use names like 'Walls', 'Doors', 'Rooms', etc.");

            var categoryId = new ElementId(builtInCategory);

            using var transaction = new Transaction(doc, "MCP: Create Schedule View");
            transaction.Start();

            try
            {
                var schedule = ViewSchedule.CreateSchedule(doc, categoryId);

                if (!string.IsNullOrEmpty(viewName))
                    schedule.Name = viewName;

                // Add requested fields
                var addedFields = new List<string>();
                var skippedFields = new List<string>();

                if (fieldNames.Count > 0)
                {
                    var schedulableFields = schedule.Definition.GetSchedulableFields();

                    foreach (var fieldName in fieldNames)
                    {
                        var match = schedulableFields
                            .FirstOrDefault(sf =>
                                string.Equals(sf.GetName(doc), fieldName, StringComparison.OrdinalIgnoreCase));

                        if (match != null)
                        {
                            schedule.Definition.AddField(match);
                            addedFields.Add(fieldName);
                        }
                        else
                        {
                            skippedFields.Add(fieldName);
                        }
                    }
                }

                transaction.Commit();

                var result = new
                {
                    Id = schedule.Id.Value,
                    Name = schedule.Name,
                    Category = categoryName,
                    ViewType = schedule.ViewType.ToString(),
                    AddedFields = addedFields,
                    SkippedFields = skippedFields
                };

                var data = JsonSerializer.SerializeToElement(result);
                return new BridgeResponse(Success: true, Data: data);
            }
            catch (Exception ex)
            {
                if (transaction.GetStatus() == TransactionStatus.Started)
                    transaction.RollBack();
                return new BridgeResponse(Success: false, Error: $"Failed to create schedule: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            return new BridgeResponse(Success: false, Error: ex.Message);
        }
    }
}
