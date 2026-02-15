using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using RevitMcp.Server.Bridge;

// Redirect any stray stdout writes to stderr so they don't
// interfere with MCP JSON-RPC messages on stdout.
Console.SetOut(Console.Error);

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddDebug();

builder.Services.AddSingleton<RevitBridgeClient>();

builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new()
        {
            Name = "revit-mcp",
            Version = "0.1.0"
        };
    })
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

var app = builder.Build();

// Connect the bridge client to the Revit add-in before accepting MCP calls.
//var bridge = app.Services.GetRequiredService<RevitBridgeClient>();
//await bridge.ConnectAsync();

await app.RunAsync();
