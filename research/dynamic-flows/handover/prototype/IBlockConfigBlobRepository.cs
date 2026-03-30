namespace DataFlow.DynamicFlows.Prototype;

// ---------------------------------------------------------------------------
// IBlockConfigBlobRepository
// Abstraction for storing per-block configuration payloads.
// The repository is the single enforcement point for secret encryption —
// consumers interact with plain-text JSON; the repository transparently
// encrypts/decrypts x-secret fields using ASP.NET Core Data Protection.
// ---------------------------------------------------------------------------

/// <summary>
/// Stores and retrieves per-block configuration payloads as versioned blobs.
///
/// <para>
/// <b>Secret handling</b>: The implementation is responsible for detecting JSON Schema
/// fields annotated with <c>"x-secret": true</c> and transparently encrypting their
/// values (using ASP.NET Core Data Protection) before persisting. On load, the values
/// are decrypted before being returned to the caller. Callers always work with
/// plain-text JSON; encryption is an implementation detail of the repository.
/// </para>
///
/// <para>
/// <b>Indirection</b>: <see cref="FlowDefinition.BlockConfigRefs"/> stores only the
/// stable blob ID returned by <see cref="SaveAsync"/>. The actual config payload
/// (including secrets) is never embedded in the flow definition JSON.
/// </para>
/// </summary>
public interface IBlockConfigBlobRepository
{
    /// <summary>
    /// Persist a plain-text config JSON payload and return a stable blob ID.
    /// If <paramref name="configSchema"/> is provided, fields marked <c>"x-secret": true</c>
    /// are encrypted before storage.
    /// </summary>
    /// <param name="blockKey">The registered block key (e.g. "global:producer"). Used for audit/lookup.</param>
    /// <param name="configJson">Plain-text JSON config. Secret field values will be encrypted.</param>
    /// <param name="configSchema">Optional JSON Schema string describing the config type.
    ///   Required for secret field detection. If null, no encryption is applied.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A stable blob ID. Store this in <see cref="FlowDefinition.BlockConfigRefs"/>.</returns>
    Task<string> SaveAsync(
        string blockKey,
        string configJson,
        string? configSchema = null,
        CancellationToken ct = default);

    /// <summary>
    /// Load a config blob by ID, transparently decrypting any encrypted secret fields.
    /// Returns null if the blob does not exist.
    /// </summary>
    Task<string?> LoadAsync(string blobId, CancellationToken ct = default);

    /// <summary>Delete a config blob (e.g. when a definition version is purged).</summary>
    Task DeleteAsync(string blobId, CancellationToken ct = default);
}

/// <summary>
/// In-memory implementation of <see cref="IBlockConfigBlobRepository"/>.
/// Does NOT encrypt — suitable for demos and tests only.
/// Use <c>EfCoreBlockConfigBlobRepository</c> with Data Protection for production.
/// </summary>
public sealed class InMemoryBlockConfigBlobRepository : IBlockConfigBlobRepository
{
    private readonly Dictionary<string, (string BlockKey, string Payload)> _store = new();
    private readonly object _lock = new();

    public Task<string> SaveAsync(
        string blockKey,
        string configJson,
        string? configSchema = null,
        CancellationToken ct = default)
    {
        // No encryption in the in-memory implementation.
        var blobId = $"cfg-blob-{Guid.NewGuid():N}";
        lock (_lock)
        {
            _store[blobId] = (blockKey, configJson);
        }
        return Task.FromResult(blobId);
    }

    public Task<string?> LoadAsync(string blobId, CancellationToken ct = default)
    {
        lock (_lock)
        {
            return Task.FromResult<string?>(
                _store.TryGetValue(blobId, out var entry) ? entry.Payload : null);
        }
    }

    public Task DeleteAsync(string blobId, CancellationToken ct = default)
    {
        lock (_lock)
        {
            _store.Remove(blobId);
        }
        return Task.CompletedTask;
    }
}
