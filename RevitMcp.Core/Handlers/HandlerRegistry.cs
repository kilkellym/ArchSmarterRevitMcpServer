using System.Collections.Frozen;

namespace RevitMcp.Core.Handlers;

/// <summary>
/// Registry that maps command names to their <see cref="ICommandHandler"/> implementations.
/// Built once at startup and treated as immutable thereafter.
/// </summary>
public sealed class HandlerRegistry
{
    private readonly FrozenDictionary<string, ICommandHandler> _handlers;

    /// <summary>
    /// Creates a new registry from the given handlers.
    /// </summary>
    /// <param name="handlers">The handlers to register. Duplicate command names will throw.</param>
    /// <exception cref="ArgumentException">Thrown when two handlers share the same command name.</exception>
    public HandlerRegistry(IEnumerable<ICommandHandler> handlers)
    {
        _handlers = handlers.ToFrozenDictionary(h => h.Command, StringComparer.Ordinal);
    }

    /// <summary>
    /// Attempts to find a handler for the given command name.
    /// </summary>
    /// <param name="command">The command name to look up.</param>
    /// <param name="handler">The matching handler, or null if not found.</param>
    /// <returns>True if a handler was found; otherwise false.</returns>
    public bool TryGetHandler(string command, out ICommandHandler? handler)
    {
        return _handlers.TryGetValue(command, out handler);
    }

    /// <summary>
    /// Gets all registered command names.
    /// </summary>
    public IEnumerable<string> RegisteredCommands => _handlers.Keys;
}
