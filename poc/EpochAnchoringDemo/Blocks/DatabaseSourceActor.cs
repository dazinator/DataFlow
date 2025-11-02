namespace EpochAnchoringDemo.Blocks;

using DataFlow.POC.Core;
using EpochAnchoringDemo.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

/// <summary>
/// Source actor that streams data from an EF Core database and emits epoch substreams.
/// This is a demonstration of how to integrate EF Core with the SourceActor pattern.
/// 
/// IMPORTANT: This implementation properly separates concerns:
/// - Domain Anchor: lastProcessedId (used for query resumption: WHERE Id > lastProcessedId)
/// - Epoch Sequence: Framework-level counter for alignment tracking
/// 
/// The anchor is domain-specific (database primary key) while epoch sequence is framework-level.
/// 
/// FUTURE CHECKPOINT INTEGRATION:
/// =================================
/// In production, the domain anchor would be contributed to checkpoints at global epoch alignment:
/// 
/// 1. On checkpoint creation (at global alignment):
///    - The source calls GetLastProcessedId() and contributes it to the checkpoint object
///    - Checkpoint coordinator aggregates anchors from all sources + epoch vectors
///    - Example: { epochVector: {source: 42}, sources: {database-source: {lastProcessedId: 4200}} }
/// 
/// 2. On restart/recovery:
///    - Checkpoint is loaded and deserialized
///    - Source receives its anchor from checkpoint via constructor or initialization method
///    - Source resumes with WHERE Id > lastProcessedId
/// 
/// 3. No standalone anchor store needed:
///    - Source manages anchor internally as private field
///    - Checkpoint system provides centralized persistence
///    - All sources and blocks share consistent recovery boundary
/// 
/// This ensures all sources and blocks share a consistent recovery boundary at global epoch alignment.
/// </summary>
public sealed class DatabaseSourceActor : SourceActorBase<DataRecord>
{
    private readonly DbContextOptions<DemoDbContext> _dbOptions;
    private readonly int _epochSize;
    private readonly string _sourceId;
    private readonly ILogger<DatabaseSourceActor>? _logger;
    
    // Domain anchor: tracks the last processed record ID
    // This is the source-specific resume point that would be contributed to checkpoints
    private int _lastProcessedId;

    /// <summary>
    /// Creates a new DatabaseSourceActor.
    /// </summary>
    /// <param name="dbOptions">Database context options</param>
    /// <param name="epochSize">Number of records per epoch</param>
    /// <param name="sourceId">Unique identifier for this source</param>
    /// <param name="initialLastProcessedId">
    /// Optional initial anchor value for resumption.
    /// In production, this would come from a loaded checkpoint.
    /// If not provided, starts from the beginning (Id > 0).
    /// </param>
    /// <param name="logger">Optional logger</param>
    public DatabaseSourceActor(
        DbContextOptions<DemoDbContext> dbOptions,
        int epochSize,
        string sourceId = "database-source",
        int initialLastProcessedId = 0,
        ILogger<DatabaseSourceActor>? logger = null)
    {
        _dbOptions = dbOptions ?? throw new ArgumentNullException(nameof(dbOptions));
        _epochSize = epochSize > 0 ? epochSize : throw new ArgumentOutOfRangeException(nameof(epochSize));
        _sourceId = sourceId;
        _lastProcessedId = initialLastProcessedId;
        _logger = logger;
    }

    public override async IAsyncEnumerable<IEpochStream<DataRecord>> ProduceEpochsAsync(
        IActorExecutionContext context)
    {
        _logger?.LogInformation(
            "Starting DatabaseSourceActor from anchor: lastProcessedId={LastProcessedId}, sourceId={SourceId}",
            _lastProcessedId,
            _sourceId);

        long currentSequence = 1;

        await using var dbContext = new DemoDbContext(_dbOptions);
        
        // Query based on domain anchor (lastProcessedId), not on Processed flag
        // This is the proper resumption pattern: WHERE Id > lastProcessedId
        var query = dbContext.DataRecords
            .AsNoTracking()
            .Where(r => r.Id > _lastProcessedId)
            .OrderBy(r => r.Id);

        var recordsEnumerator = query.AsAsyncEnumerable().GetAsyncEnumerator(context.CancellationToken);
        
        try
        {
            var hasMore = await recordsEnumerator.MoveNextAsync();
            
            while (hasMore)
            {
                // Framework-level epoch for alignment tracking
                var epoch = CreateEpoch(_sourceId, currentSequence);
                
                _logger?.LogDebug("Yielding epoch {Epoch} starting from Id > {LastId}", epoch, _lastProcessedId);
                
                int epochStartId = _lastProcessedId;
                
                yield return CreateEpochStream(epoch, StreamEpochItems());

                async IAsyncEnumerable<DataRecord> StreamEpochItems(
                    [EnumeratorCancellation] CancellationToken ct = default)
                {
                    var itemsInEpoch = 0;
                    int lastIdInEpoch = epochStartId;

                    while (true)
                    {
                        var record = recordsEnumerator.Current;
                        yield return record;
                        
                        itemsInEpoch++;
                        lastIdInEpoch = record.Id;

                        if (itemsInEpoch >= _epochSize)
                        {
                            hasMore = await recordsEnumerator.MoveNextAsync();
                            
                            // Update domain anchor after epoch completion
                            // FUTURE: This anchor value would be contributed to checkpoint at global alignment
                            _lastProcessedId = lastIdInEpoch;
                            
                            _logger?.LogDebug(
                                "Epoch {Sequence} completed with {Count} items, lastProcessedId updated to {LastId}",
                                currentSequence,
                                itemsInEpoch,
                                _lastProcessedId);
                            
                            yield break;
                        }

                        hasMore = await recordsEnumerator.MoveNextAsync();
                        
                        if (!hasMore)
                        {
                            // Update domain anchor at end of data
                            // FUTURE: This anchor value would be contributed to checkpoint at global alignment
                            _lastProcessedId = lastIdInEpoch;
                            
                            _logger?.LogDebug(
                                "Final epoch {Sequence} with {Count} items, lastProcessedId={LastId}",
                                currentSequence,
                                itemsInEpoch,
                                _lastProcessedId);
                            
                            yield break;
                        }
                    }
                }

                currentSequence++;
            }
        }
        finally
        {
            await recordsEnumerator.DisposeAsync();
        }

        _logger?.LogInformation(
            "DatabaseSourceActor completed after {Count} epochs, final lastProcessedId={LastId}",
            currentSequence - 1,
            _lastProcessedId);
    }
    
    /// <summary>
    /// Gets the current domain anchor (lastProcessedId) for this source.
    /// 
    /// FUTURE CHECKPOINT INTEGRATION:
    /// This method would be called by the checkpoint system when creating a checkpoint
    /// at global epoch alignment. The returned value would be included in the checkpoint
    /// metadata under the source's entry:
    /// 
    /// Example checkpoint:
    /// {
    ///   "epochVector": {"database-source": 42},
    ///   "sources": {
    ///     "database-source": { "lastProcessedId": 4200 }
    ///   },
    ///   "timestamp": "2025-11-03T10:45:00Z"
    /// }
    /// 
    /// On restart, the checkpoint would be loaded and this anchor passed to the constructor.
    /// </summary>
    public int GetLastProcessedId() => _lastProcessedId;
}
