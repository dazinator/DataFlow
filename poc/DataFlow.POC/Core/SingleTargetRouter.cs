namespace DataFlow.POC.Core;

using System.Threading.Channels;

/// <summary>
/// Single-target router that eliminates dictionary lookups in the routing hot path.
/// Each router knows its single target block and has a direct reference to the channel writer.
/// This is used by EdgeStrategy to implement routing topology without per-item lookups.
/// </summary>
/// <typeparam name="T">The type of items being routed</typeparam>
public sealed class SingleTargetRouter<T>
{
    private readonly IBlock _targetBlock;
    private readonly ChannelWriter<T> _writer;

    /// <summary>
    /// Creates a single-target router with a direct writer reference.
    /// </summary>
    /// <param name="targetBlock">The target block</param>
    /// <param name="writer">The channel writer for this target</param>
    public SingleTargetRouter(IBlock targetBlock, ChannelWriter<T> writer)
    {
        _targetBlock = targetBlock ?? throw new ArgumentNullException(nameof(targetBlock));
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
    }

    /// <summary>
    /// Gets the target block for this router.
    /// </summary>
    public IBlock TargetBlock => _targetBlock;

    /// <summary>
    /// Gets the channel writer for this router.
    /// </summary>
    public ChannelWriter<T> Writer => _writer;

    /// <summary>
    /// Writes an item directly to the channel without dictionary lookup.
    /// </summary>
    public async ValueTask WriteAsync(T item, CancellationToken cancellationToken)
    {
        await _writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);
    }
}
