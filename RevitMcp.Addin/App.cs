#nullable enable
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI.Events;
using RevitMcp.Addin.Bridge;
using RevitMcp.Addin.Status;
using RevitMcp.Addin.UI;
using RevitMcp.Core.Handlers;

namespace RevitMcp.Addin;

/// <summary>
/// Revit external application entry point.
/// Wires up the MCP bridge pipeline on startup, creates the ribbon UI,
/// and tears everything down on shutdown.
/// </summary>
internal class App : IExternalApplication
{
    private PushButton? _statusButton;
    private ConnectionStatus _lastIconState = ConnectionStatus.Error;
    private DateTime _lastIconCheck = DateTime.MinValue;

    /// <summary>
    /// Static reference to the pipe server so external commands can restart it.
    /// </summary>
    public static PipeServer? PipeServer { get; private set; }

    /// <summary>
    /// Static reference to the request channel so external commands can clear pending requests.
    /// </summary>
    public static RequestChannel? Channel { get; private set; }

    /// <summary>
    /// Minimum interval between icon-state checks in the Idling handler.
    /// </summary>
    private static readonly TimeSpan IconCheckInterval = TimeSpan.FromSeconds(2);

    public Result OnStartup(UIControlledApplication app)
    {
        // 1. Build the handler registry with all known command handlers.
        var registry = new HandlerRegistry(new ICommandHandler[]
        {
            new GetElementsHandler(),
            new GetElementParametersHandler(),
            new GetProjectInfoHandler(),
            new GetElementByIdHandler(),
            new AnalyzeModelStatisticsHandler(),
            new GetCurrentViewInfoHandler(),
            new ExportRoomDataHandler(),
            new SetParameterHandler(),
            new GetSelectedElementsHandler(),
            new OpenViewHandler(),
            new CreatePlanViewHandler(),
            new CreateElevationViewHandler(),
            new CreateSectionViewHandler(),
            new CreateScheduleViewHandler(),
            new CreateSheetHandler(),
            new AddViewToSheetHandler(),
            new DeleteElementsHandler(),
            new GetParameterValueHandler(),
            new CreateWallHandler(),
            new CreateTextNoteHandler(),
            new InsertFamilyInstanceByPointHandler(),
            new CreateRailingHandler(),
            new InsertGroupHandler(),
            new CreateDimensionHandler(),
            new CreateFloorHandler(),
            new CreateDetailLineHandler(),
            new GetSheetViewsHandler(),
            new GetElementsInViewHandler(),
            new FindElementsByParameterHandler(),
            new BatchSetParametersHandler(),
            new GetViewsOnSheetHandler(),
            new MoveElementsHandler(),
            new FindElementsByNameHandler(),
            new GetSheetViewMappingHandler()
        });

        // 2. Create the channel that bridges the pipe thread → Revit main thread.
        var channel = new RequestChannel();
        Channel = channel;

        // 3. Create the external-event handler that drains the channel on Revit's main thread.
        var executor = new ExternalEventExecutor(channel, registry);
        var externalEvent = ExternalEvent.Create(executor);

        // 4. Start the named-pipe server on a background thread.
        PipeServer = new PipeServer(channel, externalEvent);
        PipeServer.Start();

        // 5. Create the ribbon panel and all buttons.
        CreateRibbonUI(app);

        // 6. Subscribe to Idling for periodic icon updates.
        app.Idling += OnIdling;

        return Result.Succeeded;
    }

    public Result OnShutdown(UIControlledApplication app)
    {
        app.Idling -= OnIdling;

        PipeServer?.Dispose();
        PipeServer = null;
        Channel = null;

        return Result.Succeeded;
    }

    /// <summary>
    /// Creates the "Revit MCP" ribbon panel with all push buttons.
    /// </summary>
    private void CreateRibbonUI(UIControlledApplication app)
    {
        var panel = GetOrCreatePanel(app, "Revit MCP");
        var assemblyPath = Assembly.GetExecutingAssembly().Location;

        // MCP Status button
        var statusData = new PushButtonData(
            "btnMcpStatus",
            "MCP\nStatus",
            assemblyPath,
            typeof(McpStatusCommand).FullName)
        {
            ToolTip = "View the status of the MCP server connection",
            LargeImage = ConvertToImageSource(Properties.Resources.Red_32),
            Image = ConvertToImageSource(Properties.Resources.Red_16)
        };
        _statusButton = panel.AddItem(statusData) as PushButton;

        // Restart Connection button
        var restartData = new PushButtonData(
            "btnRestartConnection",
            "Restart\nConnection",
            assemblyPath,
            typeof(RestartConnectionCommand).FullName)
        {
            ToolTip = "Restart the MCP pipe server. Use this if tool calls are timing out or the connection seems stuck.",
            LargeImage = ConvertToImageSource(Properties.Resources.Restart_32),
            Image = ConvertToImageSource(Properties.Resources.Restart_16)
        };
        panel.AddItem(restartData);

        // Kill Server button
        var killData = new PushButtonData(
            "btnKillServer",
            "Kill\nServer",
            assemblyPath,
            typeof(KillMcpServerCommand).FullName)
        {
            ToolTip = "Terminate the MCP server process. Use this if the server is unresponsive. Claude Desktop will start a new server automatically.",
            LargeImage = ConvertToImageSource(Properties.Resources.Stop_32),
            Image = ConvertToImageSource(Properties.Resources.Stop_16)
        };
        panel.AddItem(killData);

        // Open Claude button
        var claudeData = new PushButtonData(
            "btnOpenClaude",
            "Open\nClaude",
            assemblyPath,
            typeof(OpenClaudeCommand).FullName)
        {
            ToolTip = "Launch Claude Desktop or bring it to the foreground",
            LargeImage = ConvertToImageSource(Properties.Resources.Chat_32),
            Image = ConvertToImageSource(Properties.Resources.Chat_16)
        };
        panel.AddItem(claudeData);
    }

    /// <summary>
    /// Gets an existing ribbon panel by name, or creates a new one on the Add-Ins tab.
    /// </summary>
    private static RibbonPanel GetOrCreatePanel(UIControlledApplication app, string panelName)
    {
        foreach (var panel in app.GetRibbonPanels())
        {
            if (panel.Name == panelName)
                return panel;
        }

        return app.CreateRibbonPanel("ArchSmarter", panelName);
    }

    /// <summary>
    /// Idling event handler that checks the MCP status and swaps the ribbon icon
    /// if the state has changed. Throttled to check at most every 2 seconds.
    /// </summary>
    private void OnIdling(object? sender, IdlingEventArgs e)
    {
        var now = DateTime.Now;
        if (now - _lastIconCheck < IconCheckInterval)
            return;

        _lastIconCheck = now;

        var currentStatus = McpStatusTracker.Instance.OverallStatus;
        if (currentStatus == _lastIconState || _statusButton is null)
            return;

        _lastIconState = currentStatus;

        var (large, small) = currentStatus switch
        {
            ConnectionStatus.Connected => (Properties.Resources.Green_32, Properties.Resources.Green_16),
            ConnectionStatus.Waiting => (Properties.Resources.Yellow_32, Properties.Resources.Yellow_16),
            _ => (Properties.Resources.Red_32, Properties.Resources.Red_16)
        };

        _statusButton.LargeImage = ConvertToImageSource(large);
        _statusButton.Image = ConvertToImageSource(small);
    }

    /// <summary>
    /// Converts a byte array (embedded resource PNG) to a WPF BitmapImage.
    /// </summary>
    private static BitmapImage ConvertToImageSource(byte[] imageData)
    {
        using var stream = new MemoryStream(imageData);
        stream.Position = 0;
        var bmi = new BitmapImage();
        bmi.BeginInit();
        bmi.StreamSource = stream;
        bmi.CacheOption = BitmapCacheOption.OnLoad;
        bmi.EndInit();
        return bmi;
    }
}
