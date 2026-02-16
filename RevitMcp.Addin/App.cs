#nullable enable
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI.Events;
using RevitMcp.Addin.Bridge;
using RevitMcp.Addin.Status;
using RevitMcp.Core.Handlers;

namespace RevitMcp.Addin;

/// <summary>
/// Revit external application entry point.
/// Wires up the MCP bridge pipeline on startup, creates the ribbon UI,
/// and tears everything down on shutdown.
/// </summary>
internal class App : IExternalApplication
{
    private PipeServer? _pipeServer;
    private PushButton? _statusButton;
    private ConnectionStatus _lastIconState = ConnectionStatus.Error;
    private DateTime _lastIconCheck = DateTime.MinValue;

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
            new GetSelectedElementsHandler()
        });

        // 2. Create the channel that bridges the pipe thread → Revit main thread.
        var channel = new RequestChannel();

        // 3. Create the external-event handler that drains the channel on Revit's main thread.
        var executor = new ExternalEventExecutor(channel, registry);
        var externalEvent = ExternalEvent.Create(executor);

        // 4. Start the named-pipe server on a background thread.
        _pipeServer = new PipeServer(channel, externalEvent);
        _pipeServer.Start();

        // 5. Create the ribbon panel and MCP Status button.
        CreateRibbonUI(app);

        // 6. Subscribe to Idling for periodic icon updates.
        app.Idling += OnIdling;

        return Result.Succeeded;
    }

    public Result OnShutdown(UIControlledApplication app)
    {
        app.Idling -= OnIdling;

        _pipeServer?.Dispose();
        _pipeServer = null;

        return Result.Succeeded;
    }

    /// <summary>
    /// Creates the "Revit MCP" ribbon panel with the MCP Status push button.
    /// </summary>
    private void CreateRibbonUI(UIControlledApplication app)
    {
        var panel = GetOrCreatePanel(app, "Revit MCP");

        var buttonData = new PushButtonData(
            "btnMcpStatus",
            "MCP\nStatus",
            Assembly.GetExecutingAssembly().Location,
            typeof(McpStatusCommand).FullName)
        {
            ToolTip = "View the status of the MCP server connection"
        };

        // Start with red icon (server not yet confirmed listening).
        buttonData.LargeImage = ConvertToImageSource(Properties.Resources.Red_32);
        buttonData.Image = ConvertToImageSource(Properties.Resources.Red_16);

        _statusButton = panel.AddItem(buttonData) as PushButton;
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
