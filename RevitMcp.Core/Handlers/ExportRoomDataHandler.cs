using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using RevitMcp.Core.Commands;
using RevitMcp.Core.Messages;

namespace RevitMcp.Core.Handlers;

/// <summary>
/// Handles the <see cref="CommandNames.ExportRoomData"/> command.
/// Extracts all rooms from the active Revit model with detailed spatial and property data.
/// </summary>
/// <remarks>
/// Expected payload properties:
/// <list type="bullet">
///   <item><c>level</c> (string, optional) – Filter rooms to a specific level name.</item>
///   <item><c>department</c> (string, optional) – Filter rooms by department.</item>
///   <item><c>maxResults</c> (int, optional) – Maximum number of rooms to return. Defaults to 500.</item>
/// </list>
/// </remarks>
public sealed class ExportRoomDataHandler : ICommandHandler
{
    /// <inheritdoc />
    public string Command => CommandNames.ExportRoomData;

    /// <inheritdoc />
    public BridgeResponse Handle(BridgeRequest request, UIDocument uiDoc)
    {
        try
        {
            var doc = uiDoc.Document;
            var levelFilter = request.Payload?.TryGetProperty("level", out var levelProp) == true
                ? levelProp.GetString()
                : null;

            var departmentFilter = request.Payload?.TryGetProperty("department", out var deptProp) == true
                ? deptProp.GetString()
                : null;

            var maxResults = request.Payload?.TryGetProperty("maxResults", out var maxProp) == true
                ? maxProp.GetInt32()
                : 500;

            using var collector = new FilteredElementCollector(doc);
            var allRooms = collector
                .OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType()
                .Cast<Room>()
                .ToList();

            var notPlacedCount = 0;
            var notEnclosedCount = 0;
            var rooms = new List<object>();

            foreach (var room in allRooms)
            {
                if (room.Location is null)
                {
                    notPlacedCount++;
                    continue;
                }

                if (room.Area == 0)
                {
                    notEnclosedCount++;
                    continue;
                }

                var roomLevelName = room.Level?.Name;
                if (levelFilter is not null &&
                    !string.Equals(roomLevelName, levelFilter, StringComparison.OrdinalIgnoreCase))
                    continue;

                var roomDepartment = GetParameterString(room, BuiltInParameter.ROOM_DEPARTMENT);
                if (departmentFilter is not null &&
                    !string.Equals(roomDepartment, departmentFilter, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (rooms.Count >= maxResults)
                    break;

                var areaFt2 = GetParameterDouble(room, BuiltInParameter.ROOM_AREA);
                var volumeFt3 = GetParameterDouble(room, BuiltInParameter.ROOM_VOLUME);
                var perimeterFt = GetParameterDouble(room, BuiltInParameter.ROOM_PERIMETER);

                rooms.Add(new
                {
                    Id = room.Id.Value,
                    Name = room.Name,
                    Number = room.Number,
                    LevelName = roomLevelName,
                    Area = new
                    {
                        SquareFeet = areaFt2,
                        SquareMeters = areaFt2.HasValue
                            ? UnitUtils.ConvertFromInternalUnits(areaFt2.Value, UnitTypeId.SquareMeters)
                            : (double?)null
                    },
                    Volume = new
                    {
                        CubicFeet = volumeFt3,
                        CubicMeters = volumeFt3.HasValue
                            ? UnitUtils.ConvertFromInternalUnits(volumeFt3.Value, UnitTypeId.CubicMeters)
                            : (double?)null
                    },
                    Perimeter = new
                    {
                        Feet = perimeterFt,
                        Meters = perimeterFt.HasValue
                            ? UnitUtils.ConvertFromInternalUnits(perimeterFt.Value, UnitTypeId.Meters)
                            : (double?)null
                    },
                    Department = roomDepartment,
                    UpperOffset = GetParameterValueString(room, BuiltInParameter.ROOM_UPPER_OFFSET),
                    LimitOffset = GetParameterValueString(room, BuiltInParameter.ROOM_LOWER_OFFSET),
                    UnboundedHeight = GetParameterValueString(room, BuiltInParameter.ROOM_HEIGHT)
                });
            }

            var result = new
            {
                Summary = new
                {
                    TotalRooms = allRooms.Count,
                    PlacedAndEnclosed = allRooms.Count - notPlacedCount - notEnclosedCount,
                    NotPlaced = notPlacedCount,
                    NotEnclosed = notEnclosedCount,
                    Returned = rooms.Count
                },
                Rooms = rooms
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
    /// Gets a string parameter value from an element.
    /// </summary>
    private static string? GetParameterString(Element element, BuiltInParameter param)
    {
        var p = element.get_Parameter(param);
        if (p is null || !p.HasValue)
            return null;

        try
        {
            return p.AsString();
        }
        catch
        {
            return p.AsValueString();
        }
    }

    /// <summary>
    /// Gets a double parameter value (in internal units) from an element.
    /// </summary>
    private static double? GetParameterDouble(Element element, BuiltInParameter param)
    {
        var p = element.get_Parameter(param);
        if (p is null || !p.HasValue)
            return null;

        return p.AsDouble();
    }

    /// <summary>
    /// Gets the display value string from a parameter.
    /// </summary>
    private static string? GetParameterValueString(Element element, BuiltInParameter param)
    {
        var p = element.get_Parameter(param);
        if (p is null || !p.HasValue)
            return null;

        return p.AsValueString();
    }
}
