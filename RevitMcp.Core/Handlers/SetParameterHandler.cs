using System.Text.Json;
using Autodesk.Revit.DB;
using RevitMcp.Core.Commands;
using RevitMcp.Core.Messages;

namespace RevitMcp.Core.Handlers;

/// <summary>
/// Handles the <see cref="CommandNames.SetParameter"/> command.
/// Sets a parameter value on a Revit element within a Transaction.
/// Parses the string value to the correct type based on the parameter's StorageType.
/// </summary>
/// <remarks>
/// Expected payload properties:
/// <list type="bullet">
///   <item><c>elementId</c> (int, required) – The Revit element ID.</item>
///   <item><c>parameterName</c> (string, required) – The exact parameter name.</item>
///   <item><c>value</c> (string, required) – The new value as a string.</item>
/// </list>
/// </remarks>
public sealed class SetParameterHandler : ICommandHandler
{
    /// <inheritdoc />
    public string Command => CommandNames.SetParameter;

    /// <inheritdoc />
    public BridgeResponse Handle(BridgeRequest request, Document doc)
    {
        try
        {
            // --- Extract and validate payload ---
            if (request.Payload?.TryGetProperty("elementId", out var idProp) != true)
                return new BridgeResponse(Success: false, Error: "Missing required parameter: elementId");

            if (request.Payload?.TryGetProperty("parameterName", out var nameProp) != true)
                return new BridgeResponse(Success: false, Error: "Missing required parameter: parameterName");

            if (request.Payload?.TryGetProperty("value", out var valueProp) != true)
                return new BridgeResponse(Success: false, Error: "Missing required parameter: value");

            var elementId = new ElementId((long)idProp.GetInt64());
            var parameterName = nameProp.GetString();
            var valueString = valueProp.GetString();

            if (string.IsNullOrEmpty(parameterName))
                return new BridgeResponse(Success: false, Error: "parameterName cannot be empty.");

            if (valueString is null)
                return new BridgeResponse(Success: false, Error: "value cannot be null.");

            // --- Look up element and parameter ---
            var element = doc.GetElement(elementId);
            if (element is null)
                return new BridgeResponse(Success: false, Error: $"Element not found: {idProp.GetInt64()}");

            var param = element.LookupParameter(parameterName);
            if (param is null)
                return new BridgeResponse(Success: false,
                    Error: $"Parameter '{parameterName}' not found on element {idProp.GetInt64()}.");

            if (param.IsReadOnly)
                return new BridgeResponse(Success: false,
                    Error: $"Parameter '{parameterName}' is read-only.");

            // --- Capture old value before modification ---
            var oldValue = GetDisplayValue(param);

            // --- Parse and set within a Transaction ---
            using var transaction = new Transaction(doc, "MCP: Set Parameter");
            transaction.Start();

            try
            {
                var setResult = SetParameterValue(doc, param, valueString);
                if (!setResult.Success)
                {
                    transaction.RollBack();
                    return new BridgeResponse(Success: false, Error: setResult.Error);
                }

                transaction.Commit();
            }
            catch (Exception ex)
            {
                if (transaction.GetStatus() == TransactionStatus.Started)
                    transaction.RollBack();

                return new BridgeResponse(Success: false,
                    Error: $"Failed to set parameter: {ex.Message}");
            }

            // --- Read back the new display value ---
            var newValue = GetDisplayValue(param);

            var result = new
            {
                ElementId = element.Id.Value,
                ParameterName = parameterName,
                StorageType = param.StorageType.ToString(),
                OldValue = oldValue,
                NewValue = newValue
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
    /// Parses the string value and calls <see cref="Parameter.Set"/> with the
    /// appropriate type based on <see cref="Parameter.StorageType"/>.
    /// </summary>
    private static (bool Success, string? Error) SetParameterValue(
        Document doc, Parameter param, string value)
    {
        switch (param.StorageType)
        {
            case StorageType.String:
                param.Set(value);
                return (true, null);

            case StorageType.Integer:
                if (!int.TryParse(value, out var intVal))
                    return (false, $"Cannot parse '{value}' as an integer for parameter '{param.Definition.Name}'.");

                param.Set(intVal);
                return (true, null);

            case StorageType.Double:
                if (!double.TryParse(value, out var doubleVal))
                    return (false, $"Cannot parse '{value}' as a number for parameter '{param.Definition.Name}'.");

                var internalVal = ConvertToInternal(doc, param, doubleVal);
                param.Set(internalVal);
                return (true, null);

            case StorageType.ElementId:
                if (!long.TryParse(value, out var eidVal))
                    return (false, $"Cannot parse '{value}' as an element ID (integer) for parameter '{param.Definition.Name}'.");

                param.Set(new ElementId(eidVal));
                return (true, null);

            default:
                return (false, $"Unsupported StorageType: {param.StorageType}");
        }
    }

    /// <summary>
    /// Converts a value from the document's display units to Revit internal units.
    /// For dimensionless parameters the value is returned as-is.
    /// </summary>
    private static double ConvertToInternal(Document doc, Parameter param, double value)
    {
        var specTypeId = param.Definition.GetDataType();

        if (!UnitUtils.IsMeasurableSpec(specTypeId))
            return value;

        var formatOptions = doc.GetUnits().GetFormatOptions(specTypeId);
        var displayUnitId = formatOptions.GetUnitTypeId();
        return UnitUtils.ConvertToInternalUnits(value, displayUnitId);
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
}
