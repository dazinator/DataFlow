namespace DataFlow.Blazor.BlockTypes;

/// <summary>
/// Provides sensible default display labels for the library's native block types.
/// This is a simple lookup keyed on the CLR generic type definition name — no string
/// manipulation or naming-convention assumptions.
/// Unknown types fall back to <see cref="Type.Name"/> unchanged.
/// </summary>
public static class DataFlowBlockTypeNameFormatter
{
    // Keyed by the CLR generic type definition name (e.g. "EpochActorBlock`3").
    // Extended only when new native block types are added to the library.
    private static readonly Dictionary<string, string> KnownTypeNames = new()
    {
        { "EpochActorBlock`3",  "Actor"  },
        { "EpochBufferBlock`1", "Buffer" },
        { "EpochBatchBlock`1",  "Batch"  },
        { "EpochSourceBlock`2", "Source" },
    };

    /// <summary>
    /// Returns a friendly display label for <paramref name="blockType"/>.
    /// Returns the entry from the native-type table when matched, otherwise <c>Type.Name</c>.
    /// </summary>
    public static string Format(Type blockType)
    {
        var lookupName = blockType.IsGenericType
            ? blockType.GetGenericTypeDefinition().Name
            : blockType.Name;

        return KnownTypeNames.TryGetValue(lookupName, out var label) ? label : blockType.Name;
    }
}
