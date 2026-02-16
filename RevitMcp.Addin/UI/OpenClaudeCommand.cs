#nullable enable
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RevitMcp.Addin.UI;

/// <summary>
/// External command that launches Claude Desktop or brings it to the foreground
/// if it is already running.
/// </summary>
[Transaction(TransactionMode.Manual)]
public sealed class OpenClaudeCommand : IExternalCommand
{
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_RESTORE = 9;

    public Result Execute(
        ExternalCommandData commandData,
        ref string message,
        ElementSet elements)
    {
        try
        {
            // Check if Claude Desktop is already running
            var existing = FindClaudeProcess();

            if (existing is not null)
            {
                // Try to bring it to the foreground
                var hwnd = existing.MainWindowHandle;
                if (hwnd != IntPtr.Zero)
                {
                    ShowWindow(hwnd, SW_RESTORE);
                    SetForegroundWindow(hwnd);
                }
                else
                {
                    TaskDialog.Show("Open Claude",
                        "Claude Desktop is already running. Check your taskbar.");
                }

                existing.Dispose();
                return Result.Succeeded;
            }

            // Try known install locations
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            var candidatePaths = new[]
            {
                Path.Combine(localAppData, "Programs", "claude", "Claude.exe"),
                Path.Combine(localAppData, "AnthropicClaude", "claude.exe")
            };

            foreach (var path in candidatePaths)
            {
                if (File.Exists(path))
                {
                    Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                    return Result.Succeeded;
                }
            }

            // Try launching by name (if on PATH)
            try
            {
                Process.Start(new ProcessStartInfo("claude") { UseShellExecute = true });
                return Result.Succeeded;
            }
            catch
            {
                // Not on PATH, try protocol handler
            }

            // Try the claude:// protocol
            try
            {
                Process.Start(new ProcessStartInfo("claude://") { UseShellExecute = true });
                return Result.Succeeded;
            }
            catch
            {
                // Protocol not registered
            }

            TaskDialog.Show("Open Claude",
                "Could not find Claude Desktop. Make sure it is installed.");
            return Result.Failed;
        }
        catch (Exception ex)
        {
            TaskDialog.Show("Open Claude",
                $"Error launching Claude Desktop: {ex.Message}");
            return Result.Failed;
        }
    }

    /// <summary>
    /// Searches for a running Claude Desktop process by common process names.
    /// </summary>
    private static Process? FindClaudeProcess()
    {
        foreach (var name in new[] { "Claude", "claude", "Claude Desktop" })
        {
            var processes = Process.GetProcessesByName(name);
            if (processes.Length > 0)
            {
                // Dispose extras, return the first
                for (var i = 1; i < processes.Length; i++)
                    processes[i].Dispose();
                return processes[0];
            }
        }

        return null;
    }
}
