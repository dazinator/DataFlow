namespace DataFlow.POC.Checkpointing.Examples;

using System.Runtime.CompilerServices;
using System.Text;

/// <summary>
/// Example source block that demonstrates checkpoint awareness.
/// Saves and restores its current offset position.
/// </summary>
public sealed class CheckpointAwareMessageSource : ICheckpointAware
{
    private long _currentOffset = 0;
    private readonly long _totalMessages;
    
    public CheckpointAwareMessageSource(long totalMessages = 1000)
    {
        _totalMessages = totalMessages;
    }
    
    /// <summary>
    /// Produces messages starting from the current offset.
    /// After recovery, this will resume from the checkpointed offset.
    /// </summary>
    public async IAsyncEnumerable<Message> ProduceAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (_currentOffset < _totalMessages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var message = new Message
            {
                Offset = _currentOffset,
                Payload = $"Message {_currentOffset}",
                Timestamp = DateTimeOffset.UtcNow
            };
            
            yield return message;
            
            _currentOffset++;
            
            // Simulate some processing delay
            await Task.Delay(10, cancellationToken).ConfigureAwait(false);
        }
    }
    
    /// <summary>
    /// Creates checkpoint by saving current offset.
    /// </summary>
    public Task<byte[]?> CreateCheckpointAsync(
        string checkpointId,
        Core.EpochVector epochVector,
        CancellationToken cancellationToken = default)
    {
        // Serialize current offset as UTF8 string
        var state = Encoding.UTF8.GetBytes(_currentOffset.ToString());
        Console.WriteLine($"[Checkpoint] Source saved offset: {_currentOffset}");
        return Task.FromResult<byte[]?>(state);
    }
    
    /// <summary>
    /// Restores from checkpoint by setting current offset.
    /// </summary>
    public Task RestoreFromCheckpointAsync(
        string checkpointId,
        byte[] state,
        CancellationToken cancellationToken = default)
    {
        // Deserialize offset from UTF8 string
        var offsetStr = Encoding.UTF8.GetString(state);
        _currentOffset = long.Parse(offsetStr);
        Console.WriteLine($"[Recovery] Source restored offset: {_currentOffset}");
        return Task.CompletedTask;
    }
    
    /// <summary>
    /// Gets the current offset (for testing).
    /// </summary>
    public long CurrentOffset => _currentOffset;
}

/// <summary>
/// Example message type.
/// </summary>
public sealed class Message
{
    public long Offset { get; init; }
    public required string Payload { get; init; }
    public DateTimeOffset Timestamp { get; init; }
}

/// <summary>
/// Example stateful processor that accumulates count and demonstrates checkpointing.
/// </summary>
public sealed class CheckpointAwareAccumulator : ICheckpointAware
{
    private long _processedCount = 0;
    
    /// <summary>
    /// Processes a message and increments count.
    /// </summary>
    public Task<long> ProcessAsync(Message message)
    {
        _processedCount++;
        return Task.FromResult(_processedCount);
    }
    
    /// <summary>
    /// Creates checkpoint by saving processed count.
    /// </summary>
    public Task<byte[]?> CreateCheckpointAsync(
        string checkpointId,
        Core.EpochVector epochVector,
        CancellationToken cancellationToken = default)
    {
        var state = Encoding.UTF8.GetBytes(_processedCount.ToString());
        Console.WriteLine($"[Checkpoint] Accumulator saved count: {_processedCount}");
        return Task.FromResult<byte[]?>(state);
    }
    
    /// <summary>
    /// Restores from checkpoint by setting processed count.
    /// </summary>
    public Task RestoreFromCheckpointAsync(
        string checkpointId,
        byte[] state,
        CancellationToken cancellationToken = default)
    {
        var countStr = Encoding.UTF8.GetString(state);
        _processedCount = long.Parse(countStr);
        Console.WriteLine($"[Recovery] Accumulator restored count: {_processedCount}");
        return Task.CompletedTask;
    }
    
    /// <summary>
    /// Gets the processed count (for testing).
    /// </summary>
    public long ProcessedCount => _processedCount;
}
