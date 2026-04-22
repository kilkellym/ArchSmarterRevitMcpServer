using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMcp.Core.Commands;
using RevitMcp.Core.Messages;

namespace RevitMcp.Core.Handlers;

/// <summary>
/// Shared helpers for Family Editor command handlers. Every handler in this file
/// that modifies or reads family-specific state calls <see cref="EnsureFamilyDocument"/>
/// as its first line so that calls made against a project document fail with a
/// clear, structured error instead of a confusing Revit API exception.
/// </summary>
internal static class FamilyEditorGuard
{
    /// <summary>
    /// Returns a structured error response if the given document is not a family
    /// document; otherwise returns null so the caller can proceed.
    /// </summary>
    public static BridgeResponse? EnsureFamilyDocument(Document doc)
    {
        if (doc.IsFamilyDocument)
            return null;

        return new BridgeResponse(
            Success: false,
            Error: "Active document is not a family document. This tool requires a family (.rfa) to be open in the Family Editor.");
    }
}

/// <summary>
/// Handles the <see cref="CommandNames.IsFamilyDocument"/> command.
/// Checks whether the active document is a family document and returns the
/// family category and name when true. This handler never applies the family
/// document guard because its job is to report that state to the caller.
/// </summary>
public sealed class IsFamilyDocumentHandler : ICommandHandler
{
    /// <inheritdoc />
    public string Command => CommandNames.IsFamilyDocument;

    /// <inheritdoc />
    public BridgeResponse Handle(BridgeRequest request, UIDocument uiDoc)
    {
        try
        {
            var doc = uiDoc.Document;

            if (!doc.IsFamilyDocument)
            {
                var payload = JsonSerializer.SerializeToElement(new
                {
                    isFamilyDocument = false,
                    category = (string?)null,
                    familyName = (string?)null
                });
                return new BridgeResponse(Success: true, Data: payload);
            }

            var family = doc.OwnerFamily;
            var categoryName = family?.FamilyCategory?.Name;
            var familyName = string.IsNullOrEmpty(family?.Name) ? null : family.Name;

            var data = JsonSerializer.SerializeToElement(new
            {
                isFamilyDocument = true,
                category = categoryName,
                familyName = familyName
            });
            return new BridgeResponse(Success: true, Data: data);
        }
        catch (Exception ex)
        {
            return new BridgeResponse(Success: false, Error: ex.Message);
        }
    }
}

/// <summary>
/// Handles the <see cref="CommandNames.GetFamilyInfo"/> command.
/// Returns top-level information about the active family document.
/// </summary>
public sealed class GetFamilyInfoHandler : ICommandHandler
{
    /// <inheritdoc />
    public string Command => CommandNames.GetFamilyInfo;

    /// <inheritdoc />
    public BridgeResponse Handle(BridgeRequest request, UIDocument uiDoc)
    {
        try
        {
            var doc = uiDoc.Document;
            var guard = FamilyEditorGuard.EnsureFamilyDocument(doc);
            if (guard is not null)
                return guard;

            var family = doc.OwnerFamily;
            var manager = doc.FamilyManager;

            var data = JsonSerializer.SerializeToElement(new
            {
                familyName = family.Name,
                category = family.FamilyCategory?.Name,
                placementType = family.FamilyPlacementType.ToString(),
                parameterCount = manager.Parameters.Size,
                typeCount = manager.Types.Size,
                currentTypeName = manager.CurrentType?.Name
            });
            return new BridgeResponse(Success: true, Data: data);
        }
        catch (Exception ex)
        {
            return new BridgeResponse(Success: false, Error: ex.Message);
        }
    }
}

/// <summary>
/// Handles the <see cref="CommandNames.ListFamilyElements"/> command.
/// Returns all geometric and reference elements in the active family document
/// grouped by element type.
/// </summary>
public sealed class ListFamilyElementsHandler : ICommandHandler
{
    /// <inheritdoc />
    public string Command => CommandNames.ListFamilyElements;

    /// <inheritdoc />
    public BridgeResponse Handle(BridgeRequest request, UIDocument uiDoc)
    {
        try
        {
            var doc = uiDoc.Document;
            var guard = FamilyEditorGuard.EnsureFamilyDocument(doc);
            if (guard is not null)
                return guard;

            var results = new List<ElementSummary>();

            using (var forms = new FilteredElementCollector(doc).OfClass(typeof(GenericForm)))
            {
                foreach (var element in forms)
                    results.Add(Summarize(element));
            }

            using (var planes = new FilteredElementCollector(doc).OfClass(typeof(ReferencePlane)))
            {
                foreach (var element in planes)
                    results.Add(Summarize(element));
            }

            using (var dims = new FilteredElementCollector(doc).OfClass(typeof(Dimension)))
            {
                foreach (var element in dims)
                    results.Add(Summarize(element));
            }

            var grouped = results
                .GroupBy(r => r.ElementType)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(r => new { id = r.Id, name = r.Name, category = r.Category }).ToList());

            var data = JsonSerializer.SerializeToElement(new { elements = grouped });
            return new BridgeResponse(Success: true, Data: data);
        }
        catch (Exception ex)
        {
            return new BridgeResponse(Success: false, Error: ex.Message);
        }
    }

    private static ElementSummary Summarize(Element element)
    {
        var typeName = element.GetType().Name;
        return new ElementSummary(
            Id: element.Id.Value,
            Name: SafeGetName(element),
            ElementType: typeName,
            Category: element.Category?.Name);
    }

    private static string SafeGetName(Element element)
    {
        try
        {
            return element.Name ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private readonly record struct ElementSummary(
        long Id,
        string Name,
        string ElementType,
        string? Category);
}

/// <summary>
/// Handles the <see cref="CommandNames.GetReferencePlanes"/> command.
/// Returns every reference plane in the active family document with its geometry.
/// </summary>
public sealed class GetReferencePlanesHandler : ICommandHandler
{
    /// <inheritdoc />
    public string Command => CommandNames.GetReferencePlanes;

    /// <inheritdoc />
    public BridgeResponse Handle(BridgeRequest request, UIDocument uiDoc)
    {
        try
        {
            var doc = uiDoc.Document;
            var guard = FamilyEditorGuard.EnsureFamilyDocument(doc);
            if (guard is not null)
                return guard;

            using var collector = new FilteredElementCollector(doc).OfClass(typeof(ReferencePlane));

            var planes = collector
                .Cast<ReferencePlane>()
                .Select(p => new
                {
                    id = p.Id.Value,
                    name = p.Name ?? string.Empty,
                    bubbleEnd = XyzToObject(p.BubbleEnd),
                    freeEnd = XyzToObject(p.FreeEnd),
                    normal = XyzToObject(p.Normal),
                    isNamed = IsUserNamed(p.Name)
                })
                .ToList();

            var data = JsonSerializer.SerializeToElement(new { referencePlanes = planes });
            return new BridgeResponse(Success: true, Data: data);
        }
        catch (Exception ex)
        {
            return new BridgeResponse(Success: false, Error: ex.Message);
        }
    }

    private static object XyzToObject(XYZ xyz) => new { x = xyz.X, y = xyz.Y, z = xyz.Z };

    private static bool IsUserNamed(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        // Revit auto-names unnamed planes "Reference Plane", "Reference Plane 1", etc.
        return !name.StartsWith("Reference Plane", StringComparison.Ordinal);
    }
}

/// <summary>
/// Handles the <see cref="CommandNames.GetParameters"/> command.
/// Returns every family parameter in the active family document along with its
/// metadata and current value on the active type.
/// </summary>
public sealed class GetParametersHandler : ICommandHandler
{
    /// <inheritdoc />
    public string Command => CommandNames.GetParameters;

    /// <inheritdoc />
    public BridgeResponse Handle(BridgeRequest request, UIDocument uiDoc)
    {
        try
        {
            var doc = uiDoc.Document;
            var guard = FamilyEditorGuard.EnsureFamilyDocument(doc);
            if (guard is not null)
                return guard;

            var manager = doc.FamilyManager;
            var currentType = manager.CurrentType;

            var parameters = new List<object>();
            foreach (FamilyParameter param in manager.Parameters)
            {
                parameters.Add(new
                {
                    name = param.Definition.Name,
                    storageType = param.StorageType.ToString(),
                    isInstance = param.IsInstance,
                    isShared = param.IsShared,
                    formula = param.Formula,
                    currentValue = ReadCurrentValue(currentType, param)
                });
            }

            var data = JsonSerializer.SerializeToElement(new { parameters });
            return new BridgeResponse(Success: true, Data: data);
        }
        catch (Exception ex)
        {
            return new BridgeResponse(Success: false, Error: ex.Message);
        }
    }

    private static object? ReadCurrentValue(FamilyType? currentType, FamilyParameter param)
    {
        if (currentType is null)
            return null;

        try
        {
            return param.StorageType switch
            {
                StorageType.String => currentType.AsString(param),
                StorageType.Integer => currentType.AsInteger(param),
                StorageType.Double => currentType.AsDouble(param),
                StorageType.ElementId => currentType.AsElementId(param)?.Value,
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }
}
