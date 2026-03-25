namespace DataFlow.Blazor.BlockTypes;

/// <summary>
/// Fluent builder for configuring block visualization metadata within a single
/// <c>AddDataFlows()</c> call. Used internally by <c>DataFlowBuilder</c>.
/// </summary>
public sealed class DataFlowBlockMetadataStoreBuilder
{
    private readonly Dictionary<string, DataFlowBlockMetadata> _entries = new();

    /// <summary>Begins configuring metadata for the block registered under <paramref name="blockName"/>.</summary>
    public IBlockMetadataBuilder ForBlock(string blockName) => new BlockMetadataBuilder(this, blockName);

    /// <summary>Returns a snapshot of all entries collected so far.</summary>
    public IReadOnlyDictionary<string, DataFlowBlockMetadata> GetEntries() =>
        new Dictionary<string, DataFlowBlockMetadata>(_entries);

    internal void Apply(string blockName, DataFlowBlockMetadata metadata) =>
        _entries[blockName] = metadata;

    // ── Builder returned by ForBlock() ─────────────────────────────────────

    /// <summary>
    /// Fluent API for setting visualization metadata on a single block.
    /// </summary>
    public interface IBlockMetadataBuilder
    {
        /// <summary>Sets the human-readable display name shown as the block title.</summary>
        IBlockMetadataBuilder DisplayName(string displayName);

        /// <summary>
        /// Overrides the block type label shown beneath the title.
        /// When not set, <see cref="DataFlowBlockTypeNameFormatter.Format"/> provides the default.
        /// </summary>
        IBlockMetadataBuilder TypeLabel(string typeLabel);
    }

    private sealed class BlockMetadataBuilder : IBlockMetadataBuilder
    {
        private readonly DataFlowBlockMetadataStoreBuilder _parent;
        private readonly string _blockName;
        private string? _displayName;
        private string? _typeLabel;

        internal BlockMetadataBuilder(DataFlowBlockMetadataStoreBuilder parent, string blockName)
        {
            _parent    = parent;
            _blockName = blockName;
        }

        public IBlockMetadataBuilder DisplayName(string displayName)
        {
            _displayName = displayName;
            Commit();
            return this;
        }

        public IBlockMetadataBuilder TypeLabel(string typeLabel)
        {
            _typeLabel = typeLabel;
            Commit();
            return this;
        }

        private void Commit() =>
            _parent.Apply(_blockName, new DataFlowBlockMetadata
            {
                DisplayName = _displayName,
                TypeLabel   = _typeLabel
            });
    }
}
