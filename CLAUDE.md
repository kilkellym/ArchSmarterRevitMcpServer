# Revit MCP Server

## Project Overview
A C# MCP (Model Context Protocol) server for Autodesk Revit 2025+ that supports
two modes: external (stdio for Claude Desktop / VS Code) and embedded (WPF panel
inside Revit with direct LLM API calls). Both modes share the same tool definitions
and command handlers.

## Solution Structure
All projects target .NET 8.

- RevitMcp.Core: Message contracts, command name constants, handler interface
  and implementations. References the Revit API but not the MCP SDK or Revit UI.
- RevitMcp.Server (.NET 8 console app): MCP server with stdio transport. Contains
  tool definitions ([McpServerToolType] classes) and the Named Pipe bridge client.
- RevitMcp.Addin (.NET 8 class library): Revit add-in with Named Pipe server,
  execution strategies (ExternalEvent/Idling), WPF chat panel, and embedded
  MCP client.
- RevitMcp.Tests: Unit and integration tests.

## Adding a New Tool
1. Add the command name to RevitMcp.Core/Commands/CommandNames.cs
2. Create an ICommandHandler implementation in RevitMcp.Core/Handlers/
3. Create or update a [McpServerToolType] class in RevitMcp.Server/Tools/
4. Register the handler in App.cs
5. Write clear tool descriptions that explain purpose, parameters, and return format

## Revit API Threading Rules
- All Revit API calls MUST run on Revit's main thread
- ExternalEvent with IExternalEventHandler is the production approach
- NEVER call Revit API from a background thread or the pipe server thread
- All model modifications require a Transaction: create, Start, work, Commit/Rollback
- Validate inputs BEFORE starting the Transaction

## Bridge Communication
- Named Pipes with length-prefixed JSON framing
- Pipe name: "revit-mcp-bridge"
- All messages use BridgeRequest/BridgeResponse records from Core
- 4 byte length prefix, then UTF-8 JSON payload
- RevitBridgeClient (server side) sends; PipeServer (add-in side) receives

## Build Commands
- Build all: dotnet build RevitMcpServer.sln
- Build server only: dotnet build src/RevitMcp.Server/RevitMcp.Server.csproj
- Run tests: dotnet test src/RevitMcp.Tests/RevitMcp.Tests.csproj
- Publish server: dotnet publish src/RevitMcp.Server -c Release -o ./publish

## Testing
- Unit test handlers by mocking Document and Element where possible
- Integration test pipe communication with a test server/client pair
- Use MCP Inspector (npx @modelcontextprotocol/inspector) to test tools manually

## Common Revit API Gotchas
- FilteredElementCollector is IDisposable; wrap in using or call .ToList()
- Element.Name can throw for some types; wrap in try/catch
- BuiltInCategory enum names don't always match user-facing category names
- Revit uses feet internally; all MCP tool inputs and outputs use decimal feet (no conversion needed)
- Parameter.Set() with wrong StorageType throws; check StorageType first
- Element.Id.Value replaces Element.Id.IntegerValue in Revit 2025+

## Serialization
- Use System.Text.Json everywhere (no Newtonsoft.Json)
- Use record types with required properties for message contracts
- Use JsonSerializer with default options; snake_case not needed

## Style
- C# 12 features: primary constructors, required properties, file-scoped types
- Nullable reference types enabled
- Async/await in server and pipe code; synchronous in Revit handlers
- XML doc comments on all public types and methods
