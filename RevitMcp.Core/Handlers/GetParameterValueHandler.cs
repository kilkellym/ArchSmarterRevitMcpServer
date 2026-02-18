using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMcp.Core.Commands;
using RevitMcp.Core.Messages;

namespace RevitMcp.Core.Handlers;

/// <summary>
/// Handles the <see cref="CommandNames.GetParameterValue"/> command.
/// Returns a single parameter value from an element, checking both instance and type parameters.
/// </summary>
/// <remarks>
/// Expected payload properties:
/// <list type="bullet">
///   <item><c>elementId</c> (long, required) – The Revit element ID.</item>
///   <item><c>parameterName</c> (string, required) – The exact parameter name.</item>
/// </list>
/// </remarks>
public sealed class GetParameterValueHandler : ICommandHandler
{
    /// <inheritdoc />
    public string Command => CommandNames.GetParameterValue;

    /// <inheritdoc />
    public BridgeResponse Handle(BridgeRequest request, UIDocument uiDoc)
    {
        try
        {
            var doc = uiDoc.Document;

            if (request.Payload?.TryGetProperty("elementId", out var idProp) != true)
                return new BridgeResponse(Success: false, Error: "Missing required parameter: elementId");
            if (request.Payload?.TryGetProperty("parameterName", out var nameProp) != true)
                return new BridgeResponse(Success: false, Error: "Missing required parameter: parameterName");

            var elementId = new ElementId(idProp.GetInt64());
            var parameterName = nameProp.GetString();

            if (string.IsNullOrEmpty(parameterName))
                return new BridgeResponse(Success: false, Error: "parameterName cannot be empty.");

            var element = doc.GetElement(elementId);
            if (element is null)
                return new BridgeResponse(Success: false, Error: $"Element not found: {idProp.GetInt64()}");

            // Check instance parameter first
            var param = element.LookupParameter(parameterName);
            var isTypeParameter = false;

            // If not found on instance, check the type element
            if (param is null)
            {
                var typeId = element.GetTypeId();
                if (typeId != ElementId.InvalidElementId)
                {
                    var typeElement = doc.GetElement(typeId);
                    param = typeElement?.LookupParameter(parameterName);
                    if (param is not null)
                        isTypeParameter = true;
                }
            }

            if (param is null)
            {
                // List available parameter names to help the user
                var available = new List<string>();
                foreach (Parameter p in element.Parameters)
                {
                    if (p.Definition is not null)
                        available.Add(p.Definition.Name);
                }

                available.Sort();
                var preview = available.Count > 30
                    ? string.Join(", ", available.Take(30)) + $"... ({available.Count} total)"
                    : string.Join(", ", available);

                return new BridgeResponse(Success: false,
                    Error: $"Parameter '{parameterName}' not found on element {idProp.GetInt64()}. " +
                           $"Available parameters: {preview}");
            }

            var result = new
            {
                ElementId = element.Id.Value,
                ParameterName = param.Definition.Name,
                Value = SafeGetParameterValue(param),
                StorageType = param.StorageType.ToString(),
                IsReadOnly = param.IsReadOnly,
                IsTypeParameter = isTypeParameter,
                HasValue = param.HasValue
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
    /// Safely reads a parameter value based on its <see cref="StorageType"/>.
    /// </summary>
    private static string? SafeGetParameterValue(Parameter param)
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
