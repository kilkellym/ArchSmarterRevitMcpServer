using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMcp.Core.Commands;
using RevitMcp.Core.Messages;

namespace RevitMcp.Core.Handlers;

/// <summary>
/// Handles the <see cref="CommandNames.BatchSetParameters"/> command.
/// Sets a parameter value on multiple elements in a single transaction.
/// Supports preview mode (confirm=false) and execute mode (confirm=true).
/// </summary>
/// <remarks>
/// Expected payload properties:
/// <list type="bullet">
///   <item><c>elementIds</c> (long[], required) – Array of element IDs.</item>
///   <item><c>parameterName</c> (string, required) – The parameter name to set.</item>
///   <item><c>value</c> (string, required) – The new value as a string.</item>
///   <item><c>confirm</c> (bool, optional) – If true, applies changes. Defaults to false (preview).</item>
/// </list>
/// </remarks>
public sealed class BatchSetParametersHandler : ICommandHandler
{
    private const int MaxElements = 500;

    /// <inheritdoc />
    public string Command => CommandNames.BatchSetParameters;

    /// <inheritdoc />
    public BridgeResponse Handle(BridgeRequest request, UIDocument uiDoc)
    {
        try
        {
            var doc = uiDoc.Document;

            if (request.Payload?.TryGetProperty("elementIds", out var idsProp) != true
                || idsProp.ValueKind != JsonValueKind.Array)
                return new BridgeResponse(Success: false, Error: "Missing required parameter: elementIds (array of integers)");

            if (request.Payload?.TryGetProperty("parameterName", out var nameProp) != true)
                return new BridgeResponse(Success: false, Error: "Missing required parameter: parameterName");

            if (request.Payload?.TryGetProperty("value", out var valueProp) != true)
                return new BridgeResponse(Success: false, Error: "Missing required parameter: value");

            var parameterName = nameProp.GetString();
            var valueString = valueProp.GetString();

            if (string.IsNullOrEmpty(parameterName))
                return new BridgeResponse(Success: false, Error: "parameterName cannot be empty.");
            if (valueString is null)
                return new BridgeResponse(Success: false, Error: "value cannot be null.");

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
                return HandlePreview(doc, elementIds, parameterName);

            return HandleExecute(doc, elementIds, parameterName, valueString);
        }
        catch (Exception ex)
        {
            return new BridgeResponse(Success: false, Error: ex.Message);
        }
    }

    /// <summary>
    /// Preview mode: shows current values for each element without modifying anything.
    /// </summary>
    private static BridgeResponse HandlePreview(Document doc, List<long> elementIds, string parameterName)
    {
        var previews = new List<object>();

        foreach (var id in elementIds)
        {
            var elemId = new ElementId(id);
            var element = doc.GetElement(elemId);

            if (element is null)
            {
                previews.Add(new { Id = id, Status = "NotFound" });
                continue;
            }

            var param = element.LookupParameter(parameterName);
            if (param is null)
            {
                previews.Add(new
                {
                    Id = id,
                    Name = SafeGetName(element),
                    Status = "ParameterNotFound"
                });
                continue;
            }

            if (param.IsReadOnly)
            {
                previews.Add(new
                {
                    Id = id,
                    Name = SafeGetName(element),
                    CurrentValue = GetDisplayValue(param),
                    Status = "ReadOnly"
                });
                continue;
            }

            previews.Add(new
            {
                Id = id,
                Name = SafeGetName(element),
                CurrentValue = GetDisplayValue(param),
                StorageType = param.StorageType.ToString(),
                Status = "WouldUpdate"
            });
        }

        var result = new
        {
            Mode = "Preview",
            ParameterName = parameterName,
            Message = "Set confirm=true to apply these changes.",
            Elements = previews
        };

        var data = JsonSerializer.SerializeToElement(result);
        return new BridgeResponse(Success: true, Data: data);
    }

    /// <summary>
    /// Execute mode: sets the parameter value on all elements within a single transaction.
    /// </summary>
    private static BridgeResponse HandleExecute(
        Document doc, List<long> elementIds, string parameterName, string valueString)
    {
        using var transaction = new Transaction(doc, "MCP: Batch Set Parameters");
        transaction.Start();

        try
        {
            var updated = new List<object>();
            var errors = new List<object>();

            foreach (var id in elementIds)
            {
                var elemId = new ElementId(id);
                var element = doc.GetElement(elemId);

                if (element is null)
                {
                    errors.Add(new { Id = id, Error = "Element not found" });
                    continue;
                }

                var param = element.LookupParameter(parameterName);
                if (param is null)
                {
                    errors.Add(new { Id = id, Name = SafeGetName(element), Error = "Parameter not found" });
                    continue;
                }

                if (param.IsReadOnly)
                {
                    errors.Add(new { Id = id, Name = SafeGetName(element), Error = "Parameter is read-only" });
                    continue;
                }

                var oldValue = GetDisplayValue(param);

                try
                {
                    var setResult = SetParameterValue(param, valueString);
                    if (!setResult.Success)
                    {
                        errors.Add(new { Id = id, Name = SafeGetName(element), Error = setResult.Error });
                        continue;
                    }

                    updated.Add(new
                    {
                        Id = id,
                        Name = SafeGetName(element),
                        OldValue = oldValue,
                        NewValue = GetDisplayValue(param)
                    });
                }
                catch (Exception ex)
                {
                    errors.Add(new { Id = id, Name = SafeGetName(element), Error = ex.Message });
                }
            }

            transaction.Commit();

            var result = new
            {
                Mode = "Execute",
                ParameterName = parameterName,
                UpdatedCount = updated.Count,
                ErrorCount = errors.Count,
                Updated = updated,
                Errors = errors
            };

            var data = JsonSerializer.SerializeToElement(result);
            return new BridgeResponse(Success: true, Data: data);
        }
        catch (Exception ex)
        {
            if (transaction.GetStatus() == TransactionStatus.Started)
                transaction.RollBack();
            return new BridgeResponse(Success: false, Error: $"Failed to set parameters: {ex.Message}");
        }
    }

    /// <summary>
    /// Parses the string value and calls <see cref="Parameter.Set"/> with the
    /// appropriate type based on <see cref="Parameter.StorageType"/>.
    /// </summary>
    private static (bool Success, string? Error) SetParameterValue(Parameter param, string value)
    {
        switch (param.StorageType)
        {
            case StorageType.String:
                param.Set(value);
                return (true, null);

            case StorageType.Integer:
                if (!int.TryParse(value, out var intVal))
                    return (false, $"Cannot parse '{value}' as an integer.");
                param.Set(intVal);
                return (true, null);

            case StorageType.Double:
                if (!double.TryParse(value, out var doubleVal))
                    return (false, $"Cannot parse '{value}' as a number.");
                param.Set(doubleVal);
                return (true, null);

            case StorageType.ElementId:
                if (!long.TryParse(value, out var eidVal))
                    return (false, $"Cannot parse '{value}' as an element ID.");
                param.Set(new ElementId(eidVal));
                return (true, null);

            default:
                return (false, $"Unsupported StorageType: {param.StorageType}");
        }
    }

    /// <summary>
    /// Gets the current parameter value as a display-friendly string.
    /// </summary>
    private static string? GetDisplayValue(Parameter param)
    {
        if (!param.HasValue)
            return null;

        try
        {
            return param.StorageType switch
            {
                StorageType.String => param.AsString(),
                StorageType.Integer => param.AsInteger().ToString(),
                StorageType.Double => param.AsValueString() ?? param.AsDouble().ToString("G"),
                StorageType.ElementId => param.AsValueString() ?? param.AsElementId().Value.ToString(),
                _ => param.AsValueString()
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Safely reads <see cref="Element.Name"/>.
    /// </summary>
    private static string? SafeGetName(Element element)
    {
        try { return element.Name; }
        catch { return null; }
    }
}
