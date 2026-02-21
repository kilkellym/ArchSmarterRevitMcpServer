using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMcp.Core.Commands;
using RevitMcp.Core.Messages;

namespace RevitMcp.Core.Handlers;

/// <summary>
/// Handles the <see cref="CommandNames.FindElementsByParameter"/> command.
/// Finds elements whose parameter value matches a search string using
/// exact match, contains, starts-with, or ends-with comparison.
/// </summary>
/// <remarks>
/// Expected payload properties:
/// <list type="bullet">
///   <item><c>parameterName</c> (string, required) – The parameter name to search.</item>
///   <item><c>value</c> (string, required) – The value to match against.</item>
///   <item><c>matchType</c> (string, optional) – One of "exact", "contains", "startsWith", "endsWith". Defaults to "exact".</item>
///   <item><c>category</c> (string, optional) – Built-in category name to filter by.</item>
///   <item><c>limit</c> (int, optional) – Maximum results. Defaults to 200.</item>
/// </list>
/// </remarks>
public sealed class FindElementsByParameterHandler : ICommandHandler
{
    private const int DefaultLimit = 200;
    private const int MaxLimit = 1000;

    /// <inheritdoc />
    public string Command => CommandNames.FindElementsByParameter;

    /// <inheritdoc />
    public BridgeResponse Handle(BridgeRequest request, UIDocument uiDoc)
    {
        try
        {
            var doc = uiDoc.Document;

            if (request.Payload?.TryGetProperty("parameterName", out var nameProp) != true)
                return new BridgeResponse(Success: false, Error: "Missing required parameter: parameterName");
            if (request.Payload?.TryGetProperty("value", out var valueProp) != true)
                return new BridgeResponse(Success: false, Error: "Missing required parameter: value");

            var parameterName = nameProp.GetString();
            var searchValue = valueProp.GetString();

            if (string.IsNullOrEmpty(parameterName))
                return new BridgeResponse(Success: false, Error: "parameterName cannot be empty.");
            if (searchValue is null)
                return new BridgeResponse(Success: false, Error: "value cannot be null.");

            var matchType = request.Payload?.TryGetProperty("matchType", out var matchProp) == true
                ? matchProp.GetString() ?? "exact" : "exact";

            var category = request.Payload?.TryGetProperty("category", out var catProp) == true
                ? catProp.GetString() : null;

            var limit = request.Payload?.TryGetProperty("limit", out var limitProp) == true
                ? Math.Min(limitProp.GetInt32(), MaxLimit) : DefaultLimit;

            // Build the collector
            using var collector = new FilteredElementCollector(doc);
            collector.WhereElementIsNotElementType();

            if (category is not null)
            {
                if (!Enum.TryParse<BuiltInCategory>($"OST_{category}", out var bic))
                    return new BridgeResponse(Success: false, Error: $"Unknown category: {category}");
                collector.OfCategory(bic);
            }

            var matchedElements = new List<object>();
            var totalScanned = 0;

            foreach (var element in collector)
            {
                totalScanned++;
                var param = element.LookupParameter(parameterName);
                if (param is null || !param.HasValue)
                    continue;

                var paramValue = GetParameterDisplayValue(param);
                if (paramValue is null)
                    continue;

                if (!IsMatch(paramValue, searchValue, matchType))
                    continue;

                string? name = null;
                try { name = element.Name; } catch { /* some elements throw on Name */ }

                matchedElements.Add(new
                {
                    Id = element.Id.Value,
                    Name = name,
                    Category = element.Category?.Name,
                    ParameterValue = paramValue
                });

                if (matchedElements.Count >= limit)
                    break;
            }

            var result = new
            {
                ParameterName = parameterName,
                SearchValue = searchValue,
                MatchType = matchType,
                MatchCount = matchedElements.Count,
                TotalScanned = totalScanned,
                Truncated = matchedElements.Count >= limit,
                Elements = matchedElements
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
    /// Compares a parameter's display value against a search value using the specified match type.
    /// </summary>
    private static bool IsMatch(string paramValue, string searchValue, string matchType)
    {
        return matchType.ToLowerInvariant() switch
        {
            "contains" => paramValue.Contains(searchValue, StringComparison.OrdinalIgnoreCase),
            "startswith" => paramValue.StartsWith(searchValue, StringComparison.OrdinalIgnoreCase),
            "endswith" => paramValue.EndsWith(searchValue, StringComparison.OrdinalIgnoreCase),
            _ => string.Equals(paramValue, searchValue, StringComparison.OrdinalIgnoreCase) // exact
        };
    }

    /// <summary>
    /// Gets a parameter value as a display-friendly string.
    /// </summary>
    private static string? GetParameterDisplayValue(Parameter param)
    {
        try
        {
            return param.AsValueString() ?? param.AsString();
        }
        catch
        {
            try { return param.AsString(); }
            catch { return null; }
        }
    }
}
