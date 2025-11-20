namespace DataFlow.POC.Registry;

using DataFlow.POC.Core;

/// <summary>
/// Centralized registry for tracking registered blocks.
/// Provides block lookup, validation, and introspection capabilities.
/// </summary>
public interface IBlockTypeRegistry
{
    /// <summary>
    /// Register a block with its metadata.
    /// </summary>
    /// <param name="key">The unique key for the block (e.g., "global:producer")</param>
    /// <param name="metadata">Metadata about the block's types</param>
    /// <exception cref="InvalidOperationException">If a block with the same key is already registered</exception>
    void RegisterBlock(string key, IBlockTypeMetadata metadata);

    /// <summary>
    /// Try to register a block with its metadata.
    /// Returns false if a block with the same key is already registered.
    /// </summary>
    /// <param name="key">The unique key for the block (e.g., "global:producer")</param>
    /// <param name="metadata">Metadata about the block's types</param>
    /// <returns>True if the block was registered, false if already exists</returns>
    bool TryRegisterBlock(string key, IBlockTypeMetadata metadata);

    /// <summary>
    /// Get a block instance from the DI container using the provided service provider.
    /// </summary>
    /// <param name="serviceProvider">The service provider to resolve the block from</param>
    /// <param name="key">The unique key for the block</param>
    /// <returns>The resolved block instance</returns>
    /// <exception cref="InvalidOperationException">If the block is not registered or cannot be resolved</exception>
    IBlock GetBlock(IServiceProvider serviceProvider, string key);

    /// <summary>
    /// Try to get a block instance from the DI container.
    /// </summary>
    /// <param name="serviceProvider">The service provider to resolve the block from</param>
    /// <param name="key">The unique key for the block</param>
    /// <param name="block">The resolved block instance, or null if not found</param>
    /// <returns>True if the block was found and resolved, false otherwise</returns>
    bool TryGetBlock(IServiceProvider serviceProvider, string key, out IBlock? block);

    /// <summary>
    /// Get metadata for a registered block.
    /// </summary>
    /// <param name="key">The unique key for the block</param>
    /// <returns>The block's metadata</returns>
    /// <exception cref="KeyNotFoundException">If the block is not registered</exception>
    IBlockTypeMetadata GetMetadata(string key);

    /// <summary>
    /// Try to get metadata for a registered block.
    /// </summary>
    /// <param name="key">The unique key for the block</param>
    /// <param name="metadata">The block's metadata, or null if not found</param>
    /// <returns>True if the block metadata was found, false otherwise</returns>
    bool TryGetMetadata(string key, out IBlockTypeMetadata? metadata);

    /// <summary>
    /// Check if a block with the given key is registered.
    /// </summary>
    /// <param name="key">The unique key for the block</param>
    /// <returns>True if the block is registered, false otherwise</returns>
    bool IsBlockRegistered(string key);

    /// <summary>
    /// Get all registered block keys.
    /// </summary>
    /// <returns>An enumerable of all block keys</returns>
    IEnumerable<string> GetAllBlockKeys();

    /// <summary>
    /// Get all registered block metadata.
    /// </summary>
    /// <returns>An enumerable of all block metadata with their keys</returns>
    IEnumerable<KeyValuePair<string, IBlockTypeMetadata>> GetAllBlockMetadata();

    /// <summary>
    /// Get all block keys within a specific namespace.
    /// </summary>
    /// <param name="namespacePrefix">The namespace prefix (e.g., "global", "moduleA")</param>
    /// <returns>An enumerable of block keys in the specified namespace</returns>
    IEnumerable<string> GetBlockKeys(string namespacePrefix);
}
