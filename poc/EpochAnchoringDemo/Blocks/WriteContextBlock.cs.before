namespace EpochAnchoringDemo.Blocks;

using DataFlow.POC.Core;
using EpochAnchoringDemo.Core;
using EpochAnchoringDemo.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

/// <summary>
/// Block that processes items within epoch boundaries using scoped DbContext instances.
/// Each epoch gets its own DbContext and transaction, ensuring transactional boundaries
/// align with epoch completion.
/// 
/// NOTE: This is sample/reference code demonstrating per-epoch DbContext scoping.
/// In a full DataFlow integration, this would be a proper block that can be composed
/// with routing, allowing selective processing where only routed items participate in
/// the epoch completion transaction. This composability aligns with DataFlow concepts
/// but requires future work to simplify block development with epoch-tied lifetimes.
/// 
/// TODO: Await simplified block/actor development patterns with epoch-scoped lifetimes
/// to make this easier to implement as a proper composable block.
/// </summary>
public sealed class WriteContextBlock
{
    private readonly DbContextOptions<DemoDbContext> _dbOptions;
    private readonly ILogger<WriteContextBlock>? _logger;

    public WriteContextBlock(
        DbContextOptions<DemoDbContext> dbOptions,
        ILogger<WriteContextBlock>? logger = null)
    {
        _dbOptions = dbOptions ?? throw new ArgumentNullException(nameof(dbOptions));
        _logger = logger;
    }

    /// <summary>
    /// Processes epoch streams, tracking and saving changes per epoch.
    /// </summary>
    public async IAsyncEnumerable<IEpochStream<DataRecord>> ProcessAsync(
        IAsyncEnumerable<IEpochStream<DataRecord>> input,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var epochStream in input.WithCancellation(cancellationToken))
        {
            yield return CreateEpochStream(
                epochStream.Epoch,
                ProcessEpochItems(epochStream.Epoch, epochStream.Items, cancellationToken));
        }
    }

    private async IAsyncEnumerable<DataRecord> ProcessEpochItems(
        EpochVector epoch,
        IAsyncEnumerable<DataRecord> items,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("Starting WriteContextBlock for epoch {Epoch}", epoch);

        await using var dbContext = new DemoDbContext(_dbOptions);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var processedCount = 0;
        var processedItems = new List<DataRecord>();

        // First, collect items and track changes
        await foreach (var item in items.WithCancellation(cancellationToken))
        {
            // Track the entity as processed
            var trackedRecord = await dbContext.DataRecords.FindAsync(new object[] { item.Id }, cancellationToken);
            
            if (trackedRecord != null)
            {
                trackedRecord.Processed = true;
                processedCount++;
            }

            processedItems.Add(item);
        }

        // Commit the transaction after all items in the epoch are processed
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger?.LogInformation(
                "WriteContextBlock completed epoch {Epoch} with {Count} items",
                epoch,
                processedCount);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error processing epoch {Epoch}, rolling back transaction", epoch);
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        // Now yield the processed items
        foreach (var item in processedItems)
        {
            yield return item;
        }
    }

    private static IEpochStream<DataRecord> CreateEpochStream(EpochVector epoch, IAsyncEnumerable<DataRecord> items)
    {
        // Use a helper class that implements IEpochStream
        return new EpochStreamWrapper(epoch, items);
    }

    private sealed class EpochStreamWrapper : IEpochStream<DataRecord>
    {
        public EpochVector Epoch { get; }
        public IAsyncEnumerable<DataRecord> Items { get; }

        public EpochStreamWrapper(EpochVector epoch, IAsyncEnumerable<DataRecord> items)
        {
            Epoch = epoch;
            Items = items;
        }
    }
}
