namespace DataFlow.DeltaSpike.Delta;

public enum DeltaReadStrategy
{
    Cdf,
    VersionDiffFallback,
}

public sealed record DeltaSpikeRow(int Id, string Payload, long CommitVersion);

public sealed record DeltaCommitResult(long Version, int RowCount);

public sealed record DeltaReadResult<T>(
    IReadOnlyList<T> Items,
    DeltaReadStrategy Strategy,
    string? Diagnostic = null);

public interface IDeltaTableClient
{
    Task<long> GetLatestVersionAsync(CancellationToken cancellationToken);

    Task<DeltaCommitResult> AppendAsync(
        IReadOnlyCollection<DeltaSpikeRow> items,
        CancellationToken cancellationToken);

    Task<DeltaReadResult<DeltaSpikeRow>> ReadChangesAsync(
        long fromExclusiveVersion,
        long toInclusiveVersion,
        CancellationToken cancellationToken);
}

public sealed record DeltaTableClientOptions(
    bool PreferCdf = true,
    bool EnableCdfOnCreate = true,
    IReadOnlyDictionary<string, string>? StorageOptions = null);
