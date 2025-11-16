namespace DataFlow.POC.Core;

/// <summary>
/// Defines the policy for when epochs should be created (windowing/batching strategy).
/// </summary>
public sealed class EpochPolicy
{
    private EpochPolicy(int itemCount, TimeSpan? windowPeriod = null)
    {
        ItemCount = itemCount;
        WindowPeriod = windowPeriod;
    }
    
    /// <summary>
    /// Gets the default epoch policy (create epoch every 100 items).
    /// </summary>
    public static EpochPolicy Default => ByCount(100);
    
    /// <summary>
    /// Creates an epoch policy based on item count.
    /// An epoch is created after processing the specified number of items.
    /// </summary>
    /// <param name="count">Number of items per epoch.</param>
    public static EpochPolicy ByCount(int count)
    {
        if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Count must be greater than zero");
        }
        return new EpochPolicy(count);
    }
    
    /// <summary>
    /// Creates an epoch policy based on time window.
    /// An epoch is created after the specified time period elapses.
    /// </summary>
    /// <param name="period">Time period per epoch.</param>
    public static EpochPolicy ByTime(TimeSpan period)
    {
        if (period <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than zero");
        }
        return new EpochPolicy(int.MaxValue, period);
    }
    
    /// <summary>
    /// Creates an epoch policy based on both item count and time window.
    /// An epoch is created when either the item count is reached OR the time period elapses.
    /// </summary>
    /// <param name="count">Number of items per epoch.</param>
    /// <param name="period">Time period per epoch.</param>
    public static EpochPolicy ByCountOrTime(int count, TimeSpan period)
    {
        if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Count must be greater than zero");
        }
        if (period <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than zero");
        }
        return new EpochPolicy(count, period);
    }
    
    /// <summary>
    /// Gets the number of items per epoch (or int.MaxValue for time-only policies).
    /// </summary>
    public int ItemCount { get; }
    
    /// <summary>
    /// Gets the time period per epoch (or null for count-only policies).
    /// </summary>
    public TimeSpan? WindowPeriod { get; }
    
    /// <summary>
    /// Returns true if this policy uses time-based windowing.
    /// </summary>
    public bool HasTimeWindow => WindowPeriod.HasValue;
    
    public override string ToString()
    {
        if (HasTimeWindow && ItemCount < int.MaxValue)
        {
            return $"EpochPolicy(Count={ItemCount}, Time={WindowPeriod})";
        }
        if (HasTimeWindow)
        {
            return $"EpochPolicy(Time={WindowPeriod})";
        }
        return $"EpochPolicy(Count={ItemCount})";
    }
}
