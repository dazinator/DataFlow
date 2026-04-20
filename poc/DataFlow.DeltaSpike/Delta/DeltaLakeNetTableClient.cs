using Apache.Arrow;
using Apache.Arrow.Arrays;
using Apache.Arrow.Types;
using DeltaLake.Errors;
using DeltaLake.Interfaces;
using DeltaLake.Table;

namespace DataFlow.DeltaSpike.Delta;

public sealed class DeltaLakeNetTableClient : IDeltaTableClient
{
    private const string IdColumn = "id";
    private const string PayloadColumn = "payload";
    private const string CommitVersionColumn = "commit_version";
    private const string CdfConfigKey = "delta.enableChangeDataFeed";

    private static readonly Schema TableSchema = new Schema.Builder()
        .Field(f => f.Name(IdColumn).DataType(Int32Type.Default).Nullable(false))
        .Field(f => f.Name(PayloadColumn).DataType(StringType.Default).Nullable(false))
        .Field(f => f.Name(CommitVersionColumn).DataType(Int64Type.Default).Nullable(false))
        .Build();

    private readonly string _tableLocation;
    private readonly DeltaTableClientOptions _options;

    public DeltaLakeNetTableClient(string tableLocation, DeltaTableClientOptions? options = null)
    {
        _tableLocation = tableLocation ?? throw new ArgumentNullException(nameof(tableLocation));
        _options = options ?? new DeltaTableClientOptions();
    }

    public async Task<long> GetLatestVersionAsync(CancellationToken cancellationToken)
    {
        using IEngine engine = new DeltaEngine(EngineOptions.Default);
        using var table = await EnsureTableAsync(engine, cancellationToken);
        return (long)(table.Version() ?? 0UL);
    }

    public async Task<DeltaCommitResult> AppendAsync(
        IReadOnlyCollection<DeltaSpikeRow> items,
        CancellationToken cancellationToken)
    {
        if (items is null)
        {
            throw new ArgumentNullException(nameof(items));
        }

        using IEngine engine = new DeltaEngine(EngineOptions.Default);
        using var table = await EnsureTableAsync(engine, cancellationToken);

        if (items.Count == 0)
        {
            return new DeltaCommitResult((long)(table.Version() ?? 0UL), 0);
        }

        var commitVersion = (long)(table.Version() ?? 0UL) + 1;
        var normalizedItems = items
            .Select(x => new DeltaSpikeRow(x.Id, x.Payload, commitVersion))
            .ToList();

        var batch = BuildRecordBatch(normalizedItems);

        await table.InsertAsync(
            [batch],
            TableSchema,
            new InsertOptions { SaveMode = SaveMode.Append },
            cancellationToken);

        return new DeltaCommitResult((long)(table.Version() ?? (ulong)commitVersion), normalizedItems.Count);
    }

    public async Task<DeltaReadResult<DeltaSpikeRow>> ReadChangesAsync(
        long fromExclusiveVersion,
        long toInclusiveVersion,
        CancellationToken cancellationToken)
    {
        if (toInclusiveVersion <= fromExclusiveVersion)
        {
            return new DeltaReadResult<DeltaSpikeRow>(System.Array.Empty<DeltaSpikeRow>(), DeltaReadStrategy.VersionDiffFallback);
        }

        using IEngine engine = new DeltaEngine(EngineOptions.Default);
        using var table = await EnsureTableAsync(engine, cancellationToken);

        var latestVersion = (long)(table.Version() ?? 0UL);
        var effectiveToVersion = Math.Min(latestVersion, toInclusiveVersion);

        if (effectiveToVersion <= fromExclusiveVersion)
        {
            return new DeltaReadResult<DeltaSpikeRow>(System.Array.Empty<DeltaSpikeRow>(), DeltaReadStrategy.VersionDiffFallback);
        }

        if (_options.PreferCdf && IsCdfEnabled(table))
        {
            try
            {
                var cdfRows = await ReadViaCdfAsync(
                    table,
                    fromExclusiveVersion,
                    effectiveToVersion,
                    cancellationToken);
                return new DeltaReadResult<DeltaSpikeRow>(cdfRows, DeltaReadStrategy.Cdf);
            }
            catch (Exception ex) when (ex is NotSupportedException || ex is DeltaRuntimeException)
            {
                var fallbackRows = await ReadViaVersionDiffAsync(
                    table,
                    fromExclusiveVersion,
                    effectiveToVersion,
                    cancellationToken);
                return new DeltaReadResult<DeltaSpikeRow>(
                    fallbackRows,
                    DeltaReadStrategy.VersionDiffFallback,
                    $"CDF unavailable in current .NET API surface, fallback used: {ex.Message}");
            }
        }

        var rows = await ReadViaVersionDiffAsync(table, fromExclusiveVersion, effectiveToVersion, cancellationToken);
        return new DeltaReadResult<DeltaSpikeRow>(rows, DeltaReadStrategy.VersionDiffFallback);
    }

    private bool IsCdfEnabled(ITable table)
    {
        if (!table.Metadata().Configuration.TryGetValue(CdfConfigKey, out var value))
        {
            return false;
        }

        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    private static Task<IReadOnlyList<DeltaSpikeRow>> ReadViaCdfAsync(
        ITable table,
        long fromExclusiveVersion,
        long toInclusiveVersion,
        CancellationToken cancellationToken)
    {
        _ = table;
        _ = fromExclusiveVersion;
        _ = toInclusiveVersion;
        _ = cancellationToken;

        throw new NotSupportedException("DeltaLake.Net 0.31.1 does not expose a dedicated CDF read API.");
    }

    private static async Task<IReadOnlyList<DeltaSpikeRow>> ReadViaVersionDiffAsync(
        ITable table,
        long fromExclusiveVersion,
        long toInclusiveVersion,
        CancellationToken cancellationToken)
    {
        var results = new List<DeltaSpikeRow>();

        for (var version = fromExclusiveVersion + 1; version <= toInclusiveVersion; version++)
        {
            await table.LoadVersionAsync((ulong)version, cancellationToken);
            var query = new SelectQuery($"SELECT {IdColumn}, {PayloadColumn}, {CommitVersionColumn} FROM deltatable WHERE {CommitVersionColumn} = {version}");

            await foreach (var batch in table.QueryAsync(query, cancellationToken).WithCancellation(cancellationToken))
            {
                results.AddRange(ReadBatch(batch));
            }
        }

        return results;
    }

    private static IReadOnlyList<DeltaSpikeRow> ReadBatch(RecordBatch batch)
    {
        var idColumn = batch.Column(FindColumnIndex(batch, IdColumn));
        var payloadColumn = batch.Column(FindColumnIndex(batch, PayloadColumn));
        var commitVersionColumn = batch.Column(FindColumnIndex(batch, CommitVersionColumn));

        var rows = new List<DeltaSpikeRow>(batch.Length);
        for (var i = 0; i < batch.Length; i++)
        {
            if (!TryReadInt32(idColumn, i, out var id)
                || !TryReadString(payloadColumn, i, out var payload)
                || !TryReadInt64(commitVersionColumn, i, out var commitVersion))
            {
                continue;
            }

            rows.Add(new DeltaSpikeRow(
                id,
                payload,
                commitVersion));
        }

        return rows;
    }

    private static int FindColumnIndex(RecordBatch batch, string expectedName)
    {
        for (var i = 0; i < batch.Schema.FieldsList.Count; i++)
        {
            var currentName = batch.Schema.GetFieldByIndex(i).Name;
            if (string.Equals(currentName, expectedName, StringComparison.OrdinalIgnoreCase)
                || currentName.EndsWith($".{expectedName}", StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        var available = string.Join(", ", batch.Schema.FieldsList.Select(field => field.Name));
        throw new InvalidOperationException($"Missing expected column '{expectedName}'. Available columns: {available}");
    }

    private static bool TryReadInt32(IArrowArray array, int index, out int value)
    {
        switch (array)
        {
            case Int32Array int32 when int32.IsValid(index):
                value = int32.GetValue(index)!.Value;
                return true;
            case Int64Array int64 when int64.IsValid(index):
                value = checked((int)int64.GetValue(index)!.Value);
                return true;
            default:
                value = default;
                return false;
        }
    }

    private static bool TryReadInt64(IArrowArray array, int index, out long value)
    {
        switch (array)
        {
            case Int64Array int64 when int64.IsValid(index):
                value = int64.GetValue(index)!.Value;
                return true;
            case Int32Array int32 when int32.IsValid(index):
                value = int32.GetValue(index)!.Value;
                return true;
            default:
                value = default;
                return false;
        }
    }

    private static bool TryReadString(IArrowArray array, int index, out string value)
    {
        switch (array)
        {
            case StringArray str when str.IsValid(index):
                value = str.GetString(index) ?? string.Empty;
                return true;
            case StringViewArray stringView when stringView.IsValid(index):
                value = stringView.GetString(index) ?? string.Empty;
                return true;
            case LargeStringArray largeString when largeString.IsValid(index):
                value = largeString.GetString(index) ?? string.Empty;
                return true;
            default:
                value = string.Empty;
                return false;
        }
    }

    private RecordBatch BuildRecordBatch(IReadOnlyList<DeltaSpikeRow> items)
    {
        var ids = new Int32Array.Builder();
        var payloads = new StringArray.Builder();
        var versions = new Int64Array.Builder();

        foreach (var item in items)
        {
            ids.Append(item.Id);
            payloads.Append(item.Payload);
            versions.Append(item.CommitVersion);
        }

        var arrays = new IArrowArray[]
        {
            ids.Build(),
            payloads.Build(),
            versions.Build(),
        };

        return new RecordBatch(TableSchema, arrays, items.Count);
    }

    private async Task<ITable> EnsureTableAsync(IEngine engine, CancellationToken cancellationToken)
    {
        try
        {
            return await engine.LoadTableAsync(
                new TableOptions
                {
                    TableLocation = _tableLocation,
                    StorageOptions = StorageOptionsDictionary(),
                },
                cancellationToken);
        }
        catch
        {
            return await engine.CreateTableAsync(
                new TableCreateOptions(_tableLocation, TableSchema)
                {
                    SaveMode = SaveMode.ErrorIfExists,
                    Configuration = _options.EnableCdfOnCreate
                        ? new Dictionary<string, string> { [CdfConfigKey] = "true" }
                        : null,
                    StorageOptions = StorageOptionsDictionary(),
                },
                cancellationToken);
        }
    }

    private Dictionary<string, string> StorageOptionsDictionary() =>
        _options.StorageOptions is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(_options.StorageOptions);
}
