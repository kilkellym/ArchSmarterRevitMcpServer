namespace RevitMcp.Core.Handlers;

/// <summary>
/// Writes a C# script file to the Launchpad watched folder so it opens
/// automatically in the Launchpad editor inside Revit.
/// This handler is a simple static method — it does not implement
/// <see cref="ICommandHandler"/> because it requires no Revit API access
/// and does not run through the bridge.
/// </summary>
public static class ScriptPushHandler
{
    /// <summary>
    /// The folder where Launchpad watches for MCP-generated scripts.
    /// </summary>
    private static readonly string OutputFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ArchSmarter",
        "Launchpad",
        "MCP");

    /// <summary>
    /// Writes a script file to the Launchpad MCP folder.
    /// </summary>
    /// <param name="code">The C# script code.</param>
    /// <param name="description">Brief description prepended as a comment block.</param>
    /// <param name="fileName">
    /// Optional file name without extension. When <c>null</c> or empty a
    /// timestamp-based name is generated (e.g. <c>mcp-script-20260221-143052</c>).
    /// </param>
    /// <returns>A result containing the file path and file name.</returns>
    public static ScriptPushResult Execute(string code, string description, string? fileName = null)
    {
        Directory.CreateDirectory(OutputFolder);

        if (string.IsNullOrWhiteSpace(fileName))
            fileName = $"mcp-script-{DateTime.Now:yyyyMMdd-HHmmss}";

        var fullFileName = fileName.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
            ? fileName
            : $"{fileName}.cs";

        var filePath = Path.Combine(OutputFolder, fullFileName);

        var content =
            $"// MCP Script: {description}" + Environment.NewLine +
            $"// Generated: {DateTime.Now}" + Environment.NewLine +
            Environment.NewLine +
            code;

        File.WriteAllText(filePath, content);

        return new ScriptPushResult(filePath, fullFileName);
    }

    /// <summary>
    /// Result of a successful script push operation.
    /// </summary>
    /// <param name="FilePath">Full path to the written script file.</param>
    /// <param name="FileName">The script file name including extension.</param>
    public sealed record ScriptPushResult(string FilePath, string FileName);
}
