namespace DataFlow.DynamicFlows.Prototype;

using DataFlow.POC.Builder;
using DataFlow.POC.Core;
using DataFlow.POC.Registry;

// ---------------------------------------------------------------------------
// DynamicFlowBuilder
// Translates a FlowDefinition (JSON model) into a DataFlowGraph ready for
// execution. Uses the existing IBlockTypeRegistry and DataFlowGraphBuilder.
// ---------------------------------------------------------------------------

/// <summary>
/// Builds a <see cref="DataFlowGraph"/> from a <see cref="FlowDefinition"/> at runtime.
/// This is the core engine behind the dynamic flows feature.
///
/// <para>
/// It works by translating each block reference in the definition to a
/// <c>UseBlock()</c> call on <see cref="DataFlowGraphBuilder"/>, and each connection
/// to a <c>Connect()</c> call — the same calls a hand-written graph would make.
/// Type compatibility is validated up-front using the registry metadata so that
/// errors surface before execution begins.
/// </para>
/// </summary>
public sealed class DynamicFlowBuilder
{
    private readonly IBlockTypeRegistry _registry;

    public DynamicFlowBuilder(IBlockTypeRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    /// <summary>
    /// Validates a <see cref="FlowDefinition"/> without building a graph.
    /// Returns a list of validation errors (empty = valid).
    /// </summary>
    public IReadOnlyList<string> Validate(FlowDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var errors = new List<string>();

        // Index blocks by instanceId
        var blockIndex = new Dictionary<string, FlowBlockReference>(StringComparer.OrdinalIgnoreCase);
        foreach (var block in definition.Blocks)
        {
            if (string.IsNullOrWhiteSpace(block.InstanceId))
            {
                errors.Add("A block has an empty instanceId.");
                continue;
            }

            if (!blockIndex.TryAdd(block.InstanceId, block))
            {
                errors.Add($"Duplicate block instanceId: '{block.InstanceId}'.");
            }
        }

        // Validate each block key exists in the registry
        foreach (var block in definition.Blocks)
        {
            if (!_registry.IsBlockRegistered(block.BlockKey))
            {
                errors.Add($"Block '{block.InstanceId}' references unregistered key '{block.BlockKey}'. " +
                           $"Available keys: {string.Join(", ", _registry.GetAllBlockKeys())}");
            }
        }

        // Validate connections
        foreach (var conn in definition.Connections)
        {
            if (!blockIndex.TryGetValue(conn.From, out var fromRef))
            {
                errors.Add($"Connection references unknown source instanceId: '{conn.From}'.");
                continue;
            }

            if (!blockIndex.TryGetValue(conn.To, out var toRef))
            {
                errors.Add($"Connection references unknown target instanceId: '{conn.To}'.");
                continue;
            }

            // Type compatibility check
            if (!_registry.TryGetMetadata(fromRef.BlockKey, out var fromMeta) ||
                !_registry.TryGetMetadata(toRef.BlockKey, out var toMeta))
            {
                // Already reported above — skip compatibility check
                continue;
            }

            if (!toMeta!.InputType.IsAssignableFrom(fromMeta!.OutputType))
            {
                errors.Add(
                    $"Type mismatch on connection '{conn.From}' → '{conn.To}': " +
                    $"source outputs '{fromMeta.OutputType.Name}' but target expects '{toMeta.InputType.Name}'.");
            }
        }

        return errors;
    }

    /// <summary>
    /// Builds a <see cref="DataFlowGraph"/> from the given definition.
    /// </summary>
    /// <param name="definition">The flow definition to build from.</param>
    /// <param name="serviceProvider">DI scope for resolving block instances.</param>
    /// <returns>A ready-to-execute <see cref="DataFlowGraph"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the definition is invalid.</exception>
    public DataFlowGraph Build(FlowDefinition definition, IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var errors = Validate(definition);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                $"Flow definition '{definition.FlowId}' is invalid:\n" +
                string.Join("\n", errors.Select(e => $"  • {e}")));
        }

        // Build index: instanceId → blockKey
        var instanceToKey = definition.Blocks.ToDictionary(
            b => b.InstanceId,
            b => b.BlockKey,
            StringComparer.OrdinalIgnoreCase);

        // Use DataFlowGraphBuilder to resolve blocks from DI and wire connections
        var builder = new DataFlowGraphBuilder(definition.Name);

        foreach (var block in definition.Blocks)
        {
            builder.UseBlock(block.BlockKey);
        }

        foreach (var conn in definition.Connections)
        {
            var fromKey = instanceToKey[conn.From];
            var toKey = instanceToKey[conn.To];
            builder.Connect(fromKey, toKey, conn.BufferCapacity);
        }

        return builder.Build(serviceProvider, _registry);
    }
}
