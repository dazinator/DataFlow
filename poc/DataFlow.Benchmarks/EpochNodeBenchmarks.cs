using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Channels;
using DataFlow.POC.Registry;

namespace DataFlow.POC.Benchmarks;

/// <summary>
/// Benchmarks for EpochSourceNode and EpochProcessorNode architecture.
/// Validates performance targets:
/// - Epoch creation: &lt; 1μs
/// - Channel allocation: &lt; 100μs for 1000 channels
/// - Operation throughput: &gt; 100k ops/sec
/// - Multi-processor scaling: Linear up to 4 processors
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class EpochNodeBenchmarks : IDisposable
{
    private ServiceProvider? _serviceProvider;
    private IServiceScopeFactory? _scopeFactory;
    private EpochCoordinator? _coordinator;
    private EpochSourceNode? _source;
    private EpochProcessorNode? _processor;
    
    private const int OperationsPerEpoch = 1000;

    [Params(1, 2, 4)]
    public int ProcessorCount { get; set; } = 1;

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddScoped<TestService>();
        _serviceProvider = services.BuildServiceProvider();
        _scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _serviceProvider?.Dispose();
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }

    /// <summary>
    /// Benchmark 1: Measures epoch creation and disposal overhead.
    /// Target: &lt; 1μs per epoch.
    /// </summary>
    [Benchmark(Description = "Epoch Creation")]
    public async Task CreateAndDisposeEpoch()
    {
        var coordinator = new EpochCoordinator(_scopeFactory!);
        var vector = EpochVector.FromSingleSource("test", 1);
        var epoch = await coordinator.GetOrCreateEpochAsync("test", vector);
        await epoch.DisposeAsync();
        await coordinator.DisposeAsync();
    }

    /// <summary>
    /// Benchmark 2: Measures channel allocation impact (GC pressure).
    /// Target: &lt; 100μs for 1000 channels, minimal Gen1/2 collections.
    /// </summary>
    [Benchmark(Description = "Channel Allocation (1000 channels)")]
    public void CreateChannels()
    {
        for (int i = 0; i < 1000; i++)
        {
            var channel = Channel.CreateUnbounded<IEpochOperation>();
            channel.Writer.Complete();
        }
    }

    /// <summary>
    /// Benchmark 3: Measures serialized operation throughput.
    /// Target: &gt; 100k operations/sec.
    /// </summary>
    [Benchmark(Description = "Operation Throughput")]
    public async Task QueueOperations()
    {
        var coordinator = new EpochCoordinator(_scopeFactory!);
        var source = new EpochSourceNode();
        var processor = new EpochProcessorNode(source);

        var vector = EpochVector.FromSingleSource("test", 1);
        var epoch = await coordinator.GetOrCreateEpochAsync("test", vector);

        // Queue 1000 operations
        for (int i = 0; i < OperationsPerEpoch; i++)
        {
            await epoch.QueueSerializedOperationAsync<TestService>(async svc =>
            {
                await Task.Yield();
            });
        }

        await source.PublishEpochAsync(epoch);
        source.SignalCompletion();

        await processor.CompletionTask;
        await processor.DisposeAsync();
        await coordinator.DisposeAsync();
    }

    /// <summary>
    /// Benchmark 4: Measures multi-processor throughput.
    /// Target: Linear scaling up to 4 processors.
    /// Tests with 1, 2, and 4 processors processing 100 epochs.
    /// </summary>
    [Benchmark(Description = "Multi-Processor Throughput")]
    public async Task ProcessEpochsWithProcessors()
    {
        var coordinator = new EpochCoordinator(_scopeFactory!);
        var source = new EpochSourceNode();
        
        // Create N processors
        var processors = new List<EpochProcessorNode>();
        for (int p = 0; p < ProcessorCount; p++)
        {
            processors.Add(new EpochProcessorNode(source));
        }

        const int epochCount = 100;

        // Create and publish epochs
        for (int i = 1; i <= epochCount; i++)
        {
            var vector = EpochVector.FromSingleSource("test", i);
            var epoch = await coordinator.GetOrCreateEpochAsync("test", vector);

            // Queue some operations in each epoch
            for (int j = 0; j < 10; j++)
            {
                await epoch.QueueSerializedOperationAsync<TestService>(async svc =>
                {
                    await Task.Yield();
                });
            }

            await source.PublishEpochAsync(epoch);
        }

        source.SignalCompletion();

        // Wait for all processors
        await Task.WhenAll(processors.Select(p => p.CompletionTask));

        // Cleanup
        foreach (var processor in processors)
        {
            await processor.DisposeAsync();
        }
        await coordinator.DisposeAsync();
    }

    /// <summary>
    /// Benchmark 5: End-to-end epoch processing with hooks.
    /// Measures realistic overhead including transaction lifecycle.
    /// </summary>
    [Benchmark(Description = "End-to-End with Hooks")]
    public async Task ProcessEpochsWithHooks()
    {
        var coordinator = new EpochCoordinator(_scopeFactory!);
        var source = new EpochSourceNode();
        
        var hooks = new EpochHooks
        {
            OnBeginEpoch = async (epoch, ct) =>
            {
                await epoch.QueueSerializedOperationAsync<TestService>(async svc =>
                {
                    svc.BeginTransaction();
                    await Task.Yield();
                }, ct);
            },
            OnCommitEpoch = async (epoch, ct) =>
            {
                await epoch.QueueSerializedOperationAsync<TestService>(async svc =>
                {
                    svc.CommitTransaction();
                    await Task.Yield();
                }, ct);
            }
        };

        var processor = new EpochProcessorNode(source, hooks);

        const int epochCount = 50;

        // Create and publish epochs
        for (int i = 1; i <= epochCount; i++)
        {
            var vector = EpochVector.FromSingleSource("test", i);
            var epoch = await coordinator.GetOrCreateEpochAsync("test", vector);

            // Queue some operations
            for (int j = 0; j < 20; j++)
            {
                await epoch.QueueSerializedOperationAsync<TestService>(async svc =>
                {
                    svc.DoWork();
                    await Task.Yield();
                });
            }

            await source.PublishEpochAsync(epoch);
        }

        source.SignalCompletion();
        await processor.CompletionTask;

        await processor.DisposeAsync();
        await coordinator.DisposeAsync();
    }

    /// <summary>
    /// Benchmark 6: Measures epoch stream overhead (coordination cost).
    /// Compares cost of streaming epochs through source/processor nodes
    /// versus direct epoch processing.
    /// </summary>
    [Benchmark(Description = "Epoch Stream Overhead")]
    public async Task EpochStreamOverhead()
    {
        var coordinator = new EpochCoordinator(_scopeFactory!);
        var source = new EpochSourceNode();
        var processor = new EpochProcessorNode(source);

        const int epochCount = 100;

        for (int i = 1; i <= epochCount; i++)
        {
            var vector = EpochVector.FromSingleSource("test", i);
            var epoch = await coordinator.GetOrCreateEpochAsync("test", vector);
            await source.PublishEpochAsync(epoch);
        }

        source.SignalCompletion();
        await processor.CompletionTask;

        await processor.DisposeAsync();
        await coordinator.DisposeAsync();
    }

    // Test helper class
    private class TestService
    {
        private bool _inTransaction;

        public void BeginTransaction()
        {
            _inTransaction = true;
        }

        public void CommitTransaction()
        {
            if (!_inTransaction)
                throw new InvalidOperationException("No transaction");
            _inTransaction = false;
        }

        public void DoWork()
        {
            // Simulate work
            _ = DateTime.UtcNow.Ticks;
        }
    }
}
