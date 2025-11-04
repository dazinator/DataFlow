namespace DataFlow.POC.Core;

/// <summary>
/// Event emitted when a block begins processing a new epoch.
/// This event is per-block - each block that processes an epoch will generate this event.
/// </summary>
/// <param name="Epoch">The epoch vector identifying the epoch being created</param>
/// <param name="Block">The block context identifying which block is creating the epoch</param>
public record EpochCreatedEvent(EpochVector Epoch, IBlockContext Block);

/// <summary>
/// Event emitted when a single block completes processing all items in an epoch.
/// ⚠️ Important: This is a per-block event and does NOT indicate global alignment.
/// Other blocks may still be processing the same epoch.
/// This is NOT a safe transaction boundary - use GlobalAlignmentEvent for that.
/// </summary>
/// <param name="Epoch">The epoch vector identifying the completed epoch</param>
/// <param name="Block">The block context identifying which block completed</param>
public record EpochCompletedEvent(EpochVector Epoch, IBlockContext Block);

/// <summary>
/// Event emitted when ALL blocks in the graph have completed processing an epoch.
/// This represents the watermark - the globally-aligned completion point.
/// ✅ This IS a safe transaction boundary - all blocks have finished processing.
/// </summary>
/// <param name="Watermark">The epoch vector representing the global completion watermark</param>
public record GlobalAlignmentEvent(EpochVector Watermark);
