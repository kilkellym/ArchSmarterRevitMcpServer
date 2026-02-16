using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMcp.Core.Commands;
using RevitMcp.Core.Messages;

namespace RevitMcp.Core.Handlers;

/// <summary>
/// Handles the <see cref="CommandNames.AnalyzeModelStatistics"/> command.
/// Analyzes the overall complexity and composition of the active Revit model
/// by returning element counts broken down by category, family, type, and level.
/// </summary>
public sealed class AnalyzeModelStatisticsHandler : ICommandHandler
{
    /// <inheritdoc />
    public string Command => CommandNames.AnalyzeModelStatistics;

    /// <inheritdoc />
    public BridgeResponse Handle(BridgeRequest request, UIDocument uiDoc)
    {
        try
        {
            var doc = uiDoc.Document;
            using var collector = new FilteredElementCollector(doc);
            var elements = collector
                .WhereElementIsNotElementType()
                .ToList();

            var totalCount = elements.Count;

            var byCategory = elements
                .GroupBy(e => e.Category?.Name ?? "Uncategorized")
                .Select(g => new { Name = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToList();

            var byFamily = elements
                .Select(e => new
                {
                    Element = e,
                    FamilyName = GetFamilyName(doc, e)
                })
                .GroupBy(x => x.FamilyName)
                .Select(g => new { Name = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToList();

            var byType = elements
                .Select(e => new
                {
                    Element = e,
                    TypeName = GetTypeName(doc, e)
                })
                .GroupBy(x => x.TypeName)
                .Select(g => new { Name = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToList();

            var byLevel = elements
                .GroupBy(e => GetLevelName(doc, e))
                .Select(g => new { Name = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToList();

            var result = new
            {
                TotalElements = totalCount,
                ByCategory = byCategory,
                ByFamily = byFamily,
                ByType = byType,
                ByLevel = byLevel
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
    /// Gets the family name for an element via its type.
    /// </summary>
    private static string GetFamilyName(Document doc, Element element)
    {
        var typeId = element.GetTypeId();
        if (typeId == ElementId.InvalidElementId)
            return "No Family";

        var elementType = doc.GetElement(typeId) as ElementType;
        return elementType?.FamilyName ?? "No Family";
    }

    /// <summary>
    /// Gets the type name for an element.
    /// </summary>
    private static string GetTypeName(Document doc, Element element)
    {
        var typeId = element.GetTypeId();
        if (typeId == ElementId.InvalidElementId)
            return "No Type";

        var elementType = doc.GetElement(typeId) as ElementType;
        return elementType?.Name ?? "No Type";
    }

    /// <summary>
    /// Gets the level name for an element, or "No Level" if not applicable.
    /// </summary>
    private static string GetLevelName(Document doc, Element element)
    {
        if (element.LevelId is null || element.LevelId == ElementId.InvalidElementId)
            return "No Level";

        return doc.GetElement(element.LevelId) is Level level ? level.Name : "No Level";
    }
}
