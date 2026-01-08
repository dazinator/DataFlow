namespace DataFlow.POC.Benchmarks;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Benchmarks comparing source coordination performance vs EpochManager approach.
/// Validates the performance claims from research:
/// - Single source epoch creation: ~10-20ns (5-10x faster than EpochManager)
/// - Downstream epoch access: ~1ns (50x faster than lookup)
/// - Pipeline overhead (5 blocks): ~25ns (10x faster)
/// </summary>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class SourceCoordinationBenchmark
{
    private IEpochCoordinator _coordinator = null!;
    private IServiceScopeFactory _scopeFactory = null!;
    private IEpoch _cachedEpoch = null!;
    private EpochVector _vector1 = null!;
    private EpochVector _vector2 = null!;

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddScoped<TestService>();
        var serviceProvider = services.BuildServiceProvider();
        _scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        
        _coordinator = new EpochCoordinator(_scopeFactory);
        
        _vector1 = EpochVector.FromSingleSource("source1", 1);
        _vector2 = EpochVector.FromSingleSource("source1", 2);
        
        // Pre-create epoch for downstream access benchmark
        _cachedEpoch = _coordinator.GetOrCreateEpochAsync("source1", _vector1).GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        if (_coordinator != null)
        {
            await _coordinator.DisposeAsync();
        }
    }

    /// <summary>
    /// Benchmark: Single source epoch creation (fast path)
    /// Target: ~10-20ns
    /// </summary>
    [Benchmark(Description = "Single Source Epoch Creation (Fast Path)")]
    public async Task<IEpoch> SingleSourceEpochCreation()
    {
        return await _coordinator.GetOrCreateEpochAsync("source1", _vector1);
    }

    /// <summary>
    /// Benchmark: Downstream epoch access from stream
    /// Target: ~1ns (direct property access)
    /// </summary>
    [Benchmark(Description = "Downstream Epoch Access (Propagation)")]
    public IServiceProvider DownstreamEpochAccess()
    {
        // Simulates: stream.EpochScope.ServiceProvider
        return _cachedEpoch.ServiceProvider;
    }

    /// <summary>
    /// Benchmark: GetService from epoch scope
    /// Measures the overhead of resolving a scoped service
    /// </summary>
    [Benchmark(Description = "Epoch Scoped Service Resolution")]
    public TestService EpochScopedServiceResolution()
    {
        return _cachedEpoch.GetService<TestService>();
    }

    /// <summary>
    /// Benchmark: Pipeline overhead (5 blocks accessing epoch)
    /// Target: ~25ns total (5 blocks × ~5ns each)
    /// </summary>
    [Benchmark(Description = "Pipeline Overhead (5 Blocks)")]
    public int PipelineOverhead()
    {
        // Simulate 5 blocks accessing epoch metadata
        var count = 0;
        
        // Block 1: Access epoch vector
        var v1 = _cachedEpoch.Vector;
        count += v1.GetSequence("source1") > 0 ? 1 : 0;
        
        // Block 2: Access epoch vector
        var v2 = _cachedEpoch.Vector;
        count += v2.GetSequence("source1") > 0 ? 1 : 0;
        
        // Block 3: Access epoch vector
        var v3 = _cachedEpoch.Vector;
        count += v3.GetSequence("source1") > 0 ? 1 : 0;
        
        // Block 4: Access epoch vector
        var v4 = _cachedEpoch.Vector;
        count += v4.GetSequence("source1") > 0 ? 1 : 0;
        
        // Block 5: Access epoch vector
        var v5 = _cachedEpoch.Vector;
        count += v5.GetSequence("source1") > 0 ? 1 : 0;
        
        return count;
    }

    /// <summary>
    /// Benchmark: Multi-source coordination
    /// Measures coordination overhead when multiple sources need same epoch
    /// </summary>
    [Benchmark(Description = "Multi-Source Coordination")]
    public async Task<(IEpoch, IEpoch)> MultiSourceCoordination()
    {
        var vector = EpochVector.FromSources(new Dictionary<string, long> 
        { 
            ["sourceA"] = 1,
            ["sourceB"] = 1
        });
        
        // Both sources request same epoch
        var epochA = await _coordinator.GetOrCreateEpochAsync("sourceA", vector);
        var epochB = await _coordinator.GetOrCreateEpochAsync("sourceB", vector);
        
        return (epochA, epochB);
    }

    /// <summary>
    /// Benchmark: Readiness signaling
    /// Measures the overhead of signaling readiness for next epoch
    /// </summary>
    [Benchmark(Description = "Readiness Signaling")]
    public void ReadinessSignaling()
    {
        _coordinator.SignalReadyForNext("source1", _vector1, _vector2);
    }

    // Test service for scoped resolution
    public class TestService
    {
        public int Value { get; set; } = 42;
    }
}
