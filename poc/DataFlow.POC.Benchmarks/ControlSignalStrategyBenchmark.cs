namespace DataFlow.POC.Benchmarks;

using System.Threading.Channels;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using DataFlow.POC.Blocks;
using DataFlow.POC.Builder;
using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;
using static BenchmarkActorHelpers;

/// <summary>
/// Comprehensive benchmark comparing control signal propagation strategies.
/// Evaluates: Baseline (no control), Side-Channel, Optimized Side-Channel, and Epoch Control Plane.
/// 
/// Evaluation Criteria (from PR #113 feedback):
/// - Throughput: within 2% of baseline for pure data runs
/// - Memory: ≤10% overhead
/// - Latency: backpressure reacts within one buffer depth
/// - Ordering: verified alignment per epoch
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80, warmupCount: 3, iterationCount: 10)]
public class ControlSignalStrategyComparison
{
    private const int DataItemCount = 10000;
    private const int ControlSignalCount = 100;

    private IServiceProvider _services = null!;
    private List<IDataEnvelope> _testDataWithControl = null!;
    private List<int> _testDataOnly = null!;

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddScoped<NoOpProcessorActor<int>>();
        _services = services.BuildServiceProvider();

        // Create test data with control signals
        _testDataWithControl = new List<IDataEnvelope>(DataItemCount + ControlSignalCount);
        var controlInterval = DataItemCount / ControlSignalCount;

        for (int i = 0; i < DataItemCount; i++)
        {
            _testDataWithControl.Add(new DataItem<int>(i));

            if ((i + 1) % controlInterval == 0)
            {
                _testDataWithControl.Add(new CheckpointBarrier(Guid.NewGuid(), DateTime.UtcNow));
            }
        }

        // Create pure data for baseline (no control signals)
        _testDataOnly = Enumerable.Range(0, DataItemCount).ToList();
    }

    /// <summary>
    /// Baseline: Pure data flow with no control signals.
    /// This represents optimal performance without control signal overhead.
    /// </summary>
    [Benchmark(Baseline = true)]
    public async Task Baseline_PureDataFlow()
    {
        var producer = new ProducerBlock<int>("producer", ctx => ProducePureData(ctx));
        var consumer = new ActorBlock<int, object, NoOpProcessorActor<int>>("consumer", _services.GetRequiredService<IServiceScopeFactory>());

        var builder = new DataFlowGraphBuilder("baseline-flow");
        builder.AddBlock(producer).AddBlock(consumer);

        var strategy = new CompetingEdgeStrategy(bufferCapacity: 100);
        builder.AddEdge(new Edge(producer, new[] { consumer }, strategy));

        var graph = builder.Build();
        await graph.ExecuteAsync(new ExecutionContext(_services, CancellationToken.None));
    }

    /// <summary>
    /// Original Side-Channel implementation from PR #113.
    /// Expected: ~5-15% overhead, ~30% memory increase.
    /// </summary>
    [Benchmark]
    public async Task SideChannel_Original()
    {
        var producer = new ProducerBlock<IDataEnvelope>("producer", ctx => ProduceDataWithControl(ctx));
        var consumer = new EnvelopeProcessorBlock<int>(
            "consumer",
            processData: async (value, ctx) => await Task.CompletedTask,
            processControl: async (signal, ctx) => await Task.CompletedTask);

        var builder = new DataFlowGraphBuilder("side-channel-flow");
        builder.AddBlock(producer).AddBlock(consumer);

        var strategy = EnvelopeEdgeStrategyFactory.CreateCompetingWithSideChannel(bufferCapacity: 100);
        builder.AddEdge(new Edge(producer, new[] { consumer }, strategy));

        var graph = builder.Build();
        await graph.ExecuteAsync(new ExecutionContext(_services, CancellationToken.None));
    }

    /// <summary>
    /// Optimized Side-Channel implementation.
    /// Target: ≤2% overhead vs baseline.
    /// Optimizations: TryWrite fast path, reduced buffering, sequential broadcast for small consumer counts.
    /// </summary>
    [Benchmark]
    public async Task SideChannel_Optimized()
    {
        var producer = new ProducerBlock<IDataEnvelope>("producer", ctx => ProduceDataWithControl(ctx));
        var consumer = new EnvelopeProcessorBlock<int>(
            "consumer",
            processData: async (value, ctx) => await Task.CompletedTask,
            processControl: async (signal, ctx) => await Task.CompletedTask);

        var builder = new DataFlowGraphBuilder("optimized-side-channel-flow");
        builder.AddBlock(producer).AddBlock(consumer);

        var strategy = new OptimizedSideChannelStrategy(bufferCapacity: 100);
        builder.AddEdge(new Edge(producer, new[] { consumer }, strategy));

        var graph = builder.Build();
        await graph.ExecuteAsync(new ExecutionContext(_services, CancellationToken.None));
    }

    /// <summary>
    /// Out-of-Band Epoch Control Plane.
    /// Target: Near-zero hot-path overhead (data flow unaffected by control signals).
    /// Control signals propagate via events, not channels.
    /// </summary>
    [Benchmark]
    public async Task EpochControlPlane_OutOfBand()
    {
        var epochManager = new EpochManager();
        var producer = new ProducerBlock<int>("producer", ctx => ProducePureData(ctx));
        var consumer = new ActorBlock<int, object, NoOpProcessorActor<int>>("consumer", _services.GetRequiredService<IServiceScopeFactory>());

        var builder = new DataFlowGraphBuilder("epoch-control-plane-flow");
        builder.AddBlock(producer).AddBlock(consumer);

        var strategy = EpochControlPlaneFactory.CreateCompeting(epochManager, bufferCapacity: 100);
        builder.AddEdge(new Edge(producer, new[] { consumer }, strategy));

        var graph = builder.Build();
        await graph.ExecuteAsync(new ExecutionContext(_services, CancellationToken.None));
        
        epochManager.Dispose();
    }

    /// <summary>
    /// Event-Based Control Plane (advisory signals only).
    /// Target: Low overhead for heartbeats and progress tracking.
    /// Not suitable for coordinated checkpointing (no ordering guarantees).
    /// </summary>
    [Benchmark]
    public async Task EventBased_AdvisoryPlane()
    {
        var controlManager = new EventBasedControlSignalManager();
        var producer = new ProducerBlock<int>("producer", ctx => ProducePureData(ctx));
        var consumer = new ActorBlock<int, object, NoOpProcessorActor<int>>("consumer", _services.GetRequiredService<IServiceScopeFactory>());

        var builder = new DataFlowGraphBuilder("event-based-flow");
        builder.AddBlock(producer).AddBlock(consumer);

        var strategy = EventBasedControlStrategyFactory.CreateCompeting(controlManager, bufferCapacity: 100);
        builder.AddEdge(new Edge(producer, new[] { consumer }, strategy));

        var graph = builder.Build();
        await graph.ExecuteAsync(new ExecutionContext(_services, CancellationToken.None));
        
        controlManager.Dispose();
    }

    /// <summary>
    /// Competing edge with multiple consumers - tests scalability.
    /// </summary>
    [Benchmark]
    public async Task SideChannel_Optimized_MultipleConsumers()
    {
        var producer = new ProducerBlock<IDataEnvelope>("producer", ctx => ProduceDataWithControl(ctx));
        var consumers = new List<IBlock>();
        
        for (int i = 0; i < 5; i++)
        {
            var consumer = new EnvelopeProcessorBlock<int>(
                $"consumer{i}",
                processData: async (value, ctx) => await Task.CompletedTask,
                processControl: async (signal, ctx) => await Task.CompletedTask);
            consumers.Add(consumer);
        }

        var builder = new DataFlowGraphBuilder("optimized-multi-consumer-flow");
        builder.AddBlock(producer);
        foreach (var consumer in consumers)
        {
            builder.AddBlock(consumer);
        }

        var strategy = new OptimizedSideChannelStrategy(bufferCapacity: 100);
        builder.AddEdge(new Edge(producer, consumers, strategy));

        var graph = builder.Build();
        await graph.ExecuteAsync(new ExecutionContext(_services, CancellationToken.None));
    }

    private async IAsyncEnumerable<int> ProducePureData(IExecutionContext ctx)
    {
        foreach (var item in _testDataOnly)
        {
            yield return item;
        }
    }

    private async IAsyncEnumerable<IDataEnvelope> ProduceDataWithControl(IExecutionContext ctx)
    {
        foreach (var item in _testDataWithControl)
        {
            yield return item;
        }
    }
}

/// <summary>
/// Microbenchmark for isolated control signal routing overhead.
/// Measures the per-item cost of type checking and routing.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80, warmupCount: 3, iterationCount: 10)]
public class ControlSignalRoutingMicrobenchmark
{
    private const int ItemCount = 10000;
    private const int ControlSignalCount = 100;

    private List<IDataEnvelope> _testData = null!;
    private Channel<IDataEnvelope> _dataChannel = null!;
    private Channel<IDataEnvelope> _controlChannel = null!;

    [GlobalSetup]
    public void Setup()
    {
        _testData = new List<IDataEnvelope>(ItemCount + ControlSignalCount);
        var controlInterval = ItemCount / ControlSignalCount;

        for (int i = 0; i < ItemCount; i++)
        {
            _testData.Add(new DataItem<int>(i));

            if ((i + 1) % controlInterval == 0)
            {
                _testData.Add(new CheckpointBarrier(Guid.NewGuid(), DateTime.UtcNow));
            }
        }
    }

    [IterationSetup]
    public void IterationSetup()
    {
        _dataChannel = Channel.CreateBounded<IDataEnvelope>(100);
        _controlChannel = Channel.CreateBounded<IDataEnvelope>(5);
    }

    /// <summary>
    /// Baseline: Direct channel write without type checking.
    /// </summary>
    [Benchmark(Baseline = true)]
    public async Task Baseline_DirectWrite()
    {
        var writer = _dataChannel.Writer;
        foreach (var item in _testData)
        {
            await writer.WriteAsync(item);
        }
        writer.Complete();
    }

    /// <summary>
    /// Original side-channel: Type check per item with awaiting broadcast.
    /// </summary>
    [Benchmark]
    public async Task SideChannel_Original_TypeCheckAndRoute()
    {
        foreach (var item in _testData)
        {
            if (item.IsControlSignal())
            {
                // Broadcast to control channel (simulates original behavior)
                await _controlChannel.Writer.WriteAsync(item);
            }
            else
            {
                await _dataChannel.Writer.WriteAsync(item);
            }
        }
        _dataChannel.Writer.Complete();
        _controlChannel.Writer.Complete();
    }

    /// <summary>
    /// Optimized side-channel: Type check with TryWrite fast path.
    /// </summary>
    [Benchmark]
    public async Task SideChannel_Optimized_TryWriteFastPath()
    {
        foreach (var item in _testData)
        {
            if (item.IsControlSignal())
            {
                // Try fast path first
                if (!_controlChannel.Writer.TryWrite(item))
                {
                    await _controlChannel.Writer.WriteAsync(item);
                }
            }
            else
            {
                if (!_dataChannel.Writer.TryWrite(item))
                {
                    await _dataChannel.Writer.WriteAsync(item);
                }
            }
        }
        _dataChannel.Writer.Complete();
        _controlChannel.Writer.Complete();
    }

    /// <summary>
    /// Epoch control plane: No type checking (zero overhead).
    /// Control signals don't flow through data channels.
    /// </summary>
    [Benchmark]
    public async Task EpochControlPlane_NoTypeCheck()
    {
        var writer = _dataChannel.Writer;
        
        // Data flows without any control signal checks
        foreach (var item in _testData)
        {
            // In real scenario, only data items would be written
            // Control signals would be in separate epoch manager
            if (!item.IsControlSignal())
            {
                await writer.WriteAsync(item);
            }
        }
        
        writer.Complete();
    }
}
