namespace DataFlow.POC.Registry;

using System.Collections.Concurrent;
using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Implementation of block type metadata.
/// </summary>
public sealed class BlockTypeMetadata : IBlockTypeMetadata
{
    public BlockTypeMetadata(Type inputType, Type outputType)
    {
        InputType = inputType ?? throw new ArgumentNullException(nameof(inputType));
        OutputType = outputType ?? throw new ArgumentNullException(nameof(outputType));
    }

    public Type InputType { get; }
    public Type OutputType { get; }
}

/// <summary>
/// Thread-safe implementation of the block type registry.
/// Uses ConcurrentDictionary for safe concurrent access.
/// </summary>
public sealed class BlockTypeRegistry : IBlockTypeRegistry
{
    private readonly ConcurrentDictionary<string, IBlockTypeMetadata> _metadata = new();

    /// <inheritdoc />
    public void RegisterBlock(string key, IBlockTypeMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(metadata);

        if (!_metadata.TryAdd(key, metadata))
        {
            throw new InvalidOperationException(
                $"Block '{key}' is already registered in the registry. " +
                $"Each block must have a unique key.");
        }
    }

    /// <inheritdoc />
    public bool TryRegisterBlock(string key, IBlockTypeMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(metadata);

        return _metadata.TryAdd(key, metadata);
    }

    /// <inheritdoc />
    public IBlock GetBlock(IServiceProvider serviceProvider, string key)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(key);

        if (!TryGetBlock(serviceProvider, key, out var block))
        {
            throw new InvalidOperationException(
                $"Block '{key}' not found. " +
                $"Ensure the block is registered using services.AddDataFlows() before building the graph. " +
                $"Available blocks: {string.Join(", ", GetAllBlockKeys())}");
        }

        return block!;
    }

    /// <inheritdoc />
    public bool TryGetBlock(IServiceProvider serviceProvider, string key, out IBlock? block)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(key);

        block = null;

        // Check if block is registered in the registry
        if (!_metadata.ContainsKey(key))
        {
            return false;
        }

        // Resolve from DI using keyed service
        block = serviceProvider.GetKeyedService<IBlock>(key);
        return block != null;
    }

    /// <inheritdoc />
    public IBlockTypeMetadata GetMetadata(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (!_metadata.TryGetValue(key, out var metadata))
        {
            throw new KeyNotFoundException(
                $"Block '{key}' is not registered in the registry. " +
                $"Available blocks: {string.Join(", ", GetAllBlockKeys())}");
        }

        return metadata;
    }

    /// <inheritdoc />
    public bool TryGetMetadata(string key, out IBlockTypeMetadata? metadata)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _metadata.TryGetValue(key, out metadata);
    }

    /// <inheritdoc />
    public bool IsBlockRegistered(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _metadata.ContainsKey(key);
    }

    /// <inheritdoc />
    public IEnumerable<string> GetAllBlockKeys()
    {
        return _metadata.Keys.ToList();
    }

    /// <inheritdoc />
    public IEnumerable<KeyValuePair<string, IBlockTypeMetadata>> GetAllBlockMetadata()
    {
        return _metadata.ToList();
    }

    /// <inheritdoc />
    public IEnumerable<string> GetBlockKeys(string namespacePrefix)
    {
        ArgumentNullException.ThrowIfNull(namespacePrefix);

        var prefix = namespacePrefix.EndsWith(':') ? namespacePrefix : $"{namespacePrefix}:";
        return _metadata.Keys.Where(k => k.StartsWith(prefix)).ToList();
    }
}
