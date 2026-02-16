using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMcp.Core.Commands;
using RevitMcp.Core.Messages;

namespace RevitMcp.Core.Handlers;

/// <summary>
/// Handles the <see cref="CommandNames.CreateSheet"/> command.
/// Creates a new sheet in the project with a title block.
/// </summary>
public sealed class CreateSheetHandler : ICommandHandler
{
    /// <inheritdoc />
    public string Command => CommandNames.CreateSheet;

    /// <inheritdoc />
    public BridgeResponse Handle(BridgeRequest request, UIDocument uiDoc)
    {
        try
        {
            var doc = uiDoc.Document;

            var sheetNumber = request.Payload?.TryGetProperty("sheetNumber", out var numProp) == true
                ? numProp.GetString() : null;
            var sheetName = request.Payload?.TryGetProperty("sheetName", out var nameProp) == true
                ? nameProp.GetString() : null;
            var titleBlockName = request.Payload?.TryGetProperty("titleBlockName", out var tbProp) == true
                ? tbProp.GetString() : null;

            // Find title block
            using var tbCollector = new FilteredElementCollector(doc);
            var titleBlocks = tbCollector
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                .Cast<FamilySymbol>()
                .ToList();

            if (titleBlocks.Count == 0)
                return new BridgeResponse(Success: false,
                    Error: "No title block families found in the project.");

            FamilySymbol titleBlock;
            if (!string.IsNullOrEmpty(titleBlockName))
            {
                titleBlock = titleBlocks.FirstOrDefault(tb =>
                    string.Equals(tb.Name, titleBlockName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals($"{tb.FamilyName}: {tb.Name}", titleBlockName, StringComparison.OrdinalIgnoreCase))!;

                if (titleBlock is null)
                    return new BridgeResponse(Success: false,
                        Error: $"Title block not found: '{titleBlockName}'. Available: {string.Join(", ", titleBlocks.Select(tb => $"{tb.FamilyName}: {tb.Name}"))}");
            }
            else
            {
                titleBlock = titleBlocks.First();
            }

            using var transaction = new Transaction(doc, "MCP: Create Sheet");
            transaction.Start();

            try
            {
                if (!titleBlock.IsActive)
                    titleBlock.Activate();

                var sheet = ViewSheet.Create(doc, titleBlock.Id);

                if (!string.IsNullOrEmpty(sheetNumber))
                    sheet.SheetNumber = sheetNumber;

                if (!string.IsNullOrEmpty(sheetName))
                    sheet.Name = sheetName;

                transaction.Commit();

                var result = new
                {
                    Id = sheet.Id.Value,
                    SheetNumber = sheet.SheetNumber,
                    Name = sheet.Name,
                    TitleBlock = $"{titleBlock.FamilyName}: {titleBlock.Name}",
                    ViewType = sheet.ViewType.ToString()
                };

                var data = JsonSerializer.SerializeToElement(result);
                return new BridgeResponse(Success: true, Data: data);
            }
            catch (Exception ex)
            {
                if (transaction.GetStatus() == TransactionStatus.Started)
                    transaction.RollBack();
                return new BridgeResponse(Success: false, Error: $"Failed to create sheet: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            return new BridgeResponse(Success: false, Error: ex.Message);
        }
    }
}
