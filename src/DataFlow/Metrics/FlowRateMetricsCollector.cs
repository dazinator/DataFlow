namespace Uniun.DataFlow.Metrics;

using System.Threading.RateLimiting;

public class FlowRateMetricsCollector : IDisposable
{
    private readonly FixedWindowRateLimiter _sampler;
    private readonly BlockMetricsTagsContext _metricsContext;

    // Stream operation counters
    private long _totalStreamOperations = 0;
    private long _unreportedOperations = 0;
    private readonly DateTime _startTime = DateTime.UtcNow;

    public FlowRateMetricsCollector(
        BlockMetricsTagsContext metricsContext,
        int samplesPerSecond = 1)
    {
        _metricsContext = metricsContext;

        var options = new FixedWindowRateLimiterOptions
        {
            PermitLimit = samplesPerSecond,
            Window = TimeSpan.FromSeconds(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        };

        _sampler = new FixedWindowRateLimiter(options);
    }

    /// <summary>
    /// Record a block operation (transform, batch, etc.)
    /// </summary>
    /// <remarks>
    ///  Thread A: RecordOperation(10) → Lease OK → Reports 10
    ///  Thread B: RecordOperation(5)  → No lease → _unreportedOperations += 5
    ///  Thread C: RecordOperation(3)  → No lease → _unreportedOperations += 3  
    ///  Thread D: RecordOperation(7)  → Lease OK → Reports 7 + 8 = 15 (clears unreported)
    /// Block completes: FlushPendingOperations() → Reports any remaining
    /// </remarks>
    public void RecordOperation(int count = 1)
    {
        // Always increment total count
        Interlocked.Add(ref _totalStreamOperations, count);

        // Try to get a lease to report immediately
        using var lease = _sampler.AttemptAcquire();
        if (lease.IsAcquired)
        {
            // Report this count plus any accumulated unreported count
            var unreported = Interlocked.Exchange(ref _unreportedOperations, 0);
            var totalToReport = count + unreported;

            if (totalToReport > 0)
            {
                _metricsContext.OperationsComplete(totalToReport);
            }
        }
        else
        {
            // No lease available, add to unreported count
            Interlocked.Add(ref _unreportedOperations, count);
        }
    }

    /// <summary>
    /// Force reporting of all unreported operations (call when block completes)
    /// </summary>
    public void FlushPendingOperations()
    {
        var unreported = Interlocked.Exchange(ref _unreportedOperations, 0);
        if (unreported > 0)
        {
            _metricsContext.OperationsComplete(unreported);
        }
    }

    public FlowRateStatistics GetStatistics()
    {
        var elapsed = DateTime.UtcNow - _startTime;
        var total = Interlocked.Read(ref _totalStreamOperations);
        var unreported = Interlocked.Read(ref _unreportedOperations);

        return new FlowRateStatistics
        {
            BlockName = _metricsContext.Name,
            TotalStreamOperations = total,
            UnreportedOperations = unreported,
            ElapsedTime = elapsed,
            FlowRate = elapsed.TotalSeconds > 0 ? total / elapsed.TotalSeconds : 0
        };
    }

    public void Dispose()
    {
        FlushPendingOperations();
        _sampler?.Dispose();
    }

    public class FlowRateStatistics
    {
        public string BlockName { get; set; }
        public long TotalStreamOperations { get; set; }
        public long UnreportedOperations { get; set; }
        public TimeSpan ElapsedTime { get; set; }
        public double FlowRate { get; set; }

        public override string ToString()
        {
            return $"Operations: {FlowRate:F1}/sec (Total: {TotalStreamOperations}, Unreported: {UnreportedOperations})";
        }
    }
}
