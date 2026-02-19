using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMcp.Core.Commands;
using RevitMcp.Core.Messages;

namespace RevitMcp.Core.Handlers;

/// <summary>
/// Handles the <see cref="CommandNames.GetCurrentViewInfo"/> command.
/// Returns information about the currently active view in Revit including
/// view name, type, scale, detail level, associated level, and sheet placement.
/// </summary>
public sealed class GetCurrentViewInfoHandler : ICommandHandler
{
    /// <inheritdoc />
    public string Command => CommandNames.GetCurrentViewInfo;

    /// <inheritdoc />
    public BridgeResponse Handle(BridgeRequest request, UIDocument uiDoc)
    {
        try
        {
            var doc = uiDoc.Document;
            var view = doc.ActiveView;
            if (view is null)
                return new BridgeResponse(Success: false, Error: "No active view.");

            var result = new Dictionary<string, object?>
            {
                ["Id"] = view.Id.Value,
                ["Name"] = view.Name,
                ["ViewType"] = view.ViewType.ToString(),
                ["Scale"] = view.Scale,
                ["DetailLevel"] = view.DetailLevel.ToString(),
                ["IsTemplate"] = view.IsTemplate,
                ["Level"] = GetLevelInfo(view),
                ["Sheet"] = GetSheetInfo(view)
            };

            var data = JsonSerializer.SerializeToElement(result);
            return new BridgeResponse(Success: true, Data: data);
        }
        catch (Exception ex)
        {
            return new BridgeResponse(Success: false, Error: ex.Message);
        }
    }

    /// <summary>
    /// Gets the associated level name and elevation if the view has a GenLevel.
    /// </summary>
    private static object? GetLevelInfo(View view)
    {
        var level = view.GenLevel;
        if (level is null)
            return null;

        return new
        {
            Name = level.Name,
            Elevation = level.Elevation
        };
    }

    /// <summary>
    /// Gets the sheet number and name if the view is placed on a sheet.
    /// </summary>
    private static object? GetSheetInfo(View view)
    {
        var sheetNumberParam = view.get_Parameter(BuiltInParameter.VIEWPORT_SHEET_NUMBER);
        if (sheetNumberParam is null || !sheetNumberParam.HasValue)
            return null;

        var sheetNumber = sheetNumberParam.AsString();
        if (string.IsNullOrEmpty(sheetNumber))
            return null;

        var sheetNameParam = view.get_Parameter(BuiltInParameter.VIEWPORT_SHEET_NAME);
        var sheetName = sheetNameParam?.HasValue == true ? sheetNameParam.AsString() : null;

        return new
        {
            Number = sheetNumber,
            Name = sheetName
        };
    }
}
