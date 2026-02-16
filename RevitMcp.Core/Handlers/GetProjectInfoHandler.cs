using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMcp.Core.Commands;
using RevitMcp.Core.Messages;

namespace RevitMcp.Core.Handlers;

/// <summary>
/// Handles the <see cref="CommandNames.GetProjectInfo"/> command.
/// Returns metadata about the active Revit project.
/// </summary>
public sealed class GetProjectInfoHandler : ICommandHandler
{
    /// <inheritdoc />
    public string Command => CommandNames.GetProjectInfo;

    /// <inheritdoc />
    public BridgeResponse Handle(BridgeRequest request, UIDocument uiDoc)
    {
        try
        {
            var doc = uiDoc.Document;
            var info = doc.ProjectInformation;

            var result = new
            {
                Name = info.Name,
                Number = info.Number,
                ClientName = info.ClientName,
                Address = info.Address,
                BuildingName = info.BuildingName,
                Author = info.Author,
                OrganizationName = info.OrganizationName,
                OrganizationDescription = info.OrganizationDescription,
                Status = info.Status,
                IssueDate = info.IssueDate
            };

            var data = JsonSerializer.SerializeToElement(result);
            return new BridgeResponse(Success: true, Data: data);
        }
        catch (Exception ex)
        {
            return new BridgeResponse(Success: false, Error: ex.Message);
        }
    }
}
