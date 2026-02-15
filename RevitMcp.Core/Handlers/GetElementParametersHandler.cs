using System.Text.Json;
using Autodesk.Revit.DB;
using RevitMcp.Core.Commands;
using RevitMcp.Core.Messages;

namespace RevitMcp.Core.Handlers;

/// <summary>
/// Handles the <see cref="CommandNames.GetElementParameters"/> command.
/// Returns all parameters and their values for a specific Revit element.
/// </summary>
/// <remarks>
/// Expected payload properties:
/// <list type="bullet">
///   <item><c>elementId</c> (int, required) – The Revit element ID.</item>
/// </list>
/// </remarks>
public sealed class GetElementParametersHandler : ICommandHandler
{
    /// <inheritdoc />
    public string Command => CommandNames.GetElementParameters;

    /// <inheritdoc />
    public BridgeResponse Handle(BridgeRequest request, Document doc)
    {
        try
        {
            if (request.Payload?.TryGetProperty("elementId", out var idProp) != true)
                return new BridgeResponse(Success: false, Error: "Missing required parameter: elementId");

            var elementId = new ElementId((long)idProp.GetInt64());
            var element = doc.GetElement(elementId);

            if (element is null)
                return new BridgeResponse(Success: false, Error: $"Element not found: {idProp.GetInt64()}");

            var parameters = new List<object>();

            foreach (Parameter param in element.Parameters)
            {
                if (param.Definition is null)
                    continue;

                parameters.Add(new
                {
                    Name = param.Definition.Name,
                    Value = SafeGetParameterValue(param),
                    StorageType = param.StorageType.ToString(),
                    IsReadOnly = param.IsReadOnly,
                    IsInstance = !param.IsShared || element.GetTypeId() == ElementId.InvalidElementId
                        ? true
                        : IsInstanceParameter(doc, element, param)
                });
            }

            var data = JsonSerializer.SerializeToElement(parameters);
            return new BridgeResponse(Success: true, Data: data);
        }
        catch (Exception ex)
        {
            return new BridgeResponse(Success: false, Error: ex.Message);
        }
    }

    /// <summary>
    /// Determines whether a parameter is an instance parameter by checking if the
    /// element type also has it. If the type element lacks this parameter, it's instance-level.
    /// </summary>
    private static bool IsInstanceParameter(Document doc, Element element, Parameter param)
    {
        var typeId = element.GetTypeId();
        if (typeId == ElementId.InvalidElementId)
            return true;

        var typeElement = doc.GetElement(typeId);
        if (typeElement is null)
            return true;

        var typeParam = typeElement.LookupParameter(param.Definition.Name);
        return typeParam is null;
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
