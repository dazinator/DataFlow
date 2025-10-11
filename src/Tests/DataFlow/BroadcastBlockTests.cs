namespace Tests.DataFlow;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;
using Uniun.DataFlow;
using Uniun.DataFlow.Actor;
using Uniun.DataFlow.Blocks;
using Uniun.DataFlow.Blocks.Broadcast;
using Uniun.DataFlow.Blocks.Producer;
using Uniun.DataFlow.Blocks.Processor;
using Uniun.DataFlow.Metrics;
using Xunit;
using Xunit.Abstractions;

/// <summary>
/// Tests for the BroadcastBlock functionality.
/// BroadcastBlock implements ISourceBlock and allows multiple downstream blocks to connect on-demand.
/// </summary>
[IntegrationTest]
public class BroadcastBlockTests
{
    private readonly ITestOutputHelper _output;
    private readonly ServiceCollection _services;

    public BroadcastBlockTests(ITestOutputHelper output)
    {
        _output = output;
        _services = new ServiceCollection();
        AddDefaultServices(_services);
    }

    private void AddDefaultServices(IServiceCollection services)
    {
        services.AddLogging(builder => builder.AddXUnit(_output));
        services.AddDataFlows();
        services.AddMetrics();
        services.AddDataFlowMetrics();
    }

    private IDataFlowContext CreateContext(string name, Guid guid, IServiceProvider provider, CancellationToken ct = default)
    {
        return DataFlowContextTestUtils.GetContext(name, guid, provider, ct);
    }

    [Fact]
    public async Task BroadcastBlock_Should_Fanout_Items_To_Multiple_Processors()
    {
        // Arrange
        var items = Enumerable.Range(1, 10).ToArray();
        var target1Items = new ConcurrentBag<int>();
        var target2Items = new ConcurrentBag<int>();
        var target3Items = new ConcurrentBag<int>();

        var sp = _services.BuildServiceProvider();
        
        // Create blocks manually
        var producerLogger = sp.GetRequiredService<ILogger<ProducerBlock<int>>>();
        var channelFactory = sp.GetRequiredService<IBoundedChannelFactory>();
        var producerOptions = new ProducerBlockOptions<int>
        {
            ProducersFactory = (ctx, ct) => Task.FromResult<IEnumerable<IStreamProducer<int>>>(
                new[] { new TestProducer<int>(items) })
        };
        var producer = new ProducerBlock<int>("source", producerLogger, channelFactory, producerOptions);
        
        var broadcastLogger = sp.GetRequiredService<ILogger<BroadcastBlock<int>>>();
        var broadcast = new BroadcastBlock<int>("fanout", broadcastLogger);
        broadcast.SetSource(producer);
        
        var proc1Logger = sp.GetRequiredService<ILogger<ProcessorBlock<int>>>();
        var processor1 = new ProcessorBlock<int>("processor1", proc1Logger, 
            sp => new TestProcessor<int>(onProcessItem: item => target1Items.Add(item)));
        processor1.SetSource(broadcast);
        
        var proc2Logger = sp.GetRequiredService<ILogger<ProcessorBlock<int>>>();
        var processor2 = new ProcessorBlock<int>("processor2", proc2Logger,
            sp => new TestProcessor<int>(onProcessItem: item => target2Items.Add(item)));
        processor2.SetSource(broadcast);
        
        var proc3Logger = sp.GetRequiredService<ILogger<ProcessorBlock<int>>>();
        var processor3 = new ProcessorBlock<int>("processor3", proc3Logger,
            sp => new TestProcessor<int>(onProcessItem: item => target3Items.Add(item)));
        processor3.SetSource(broadcast);
        
        var metrics = sp.GetRequiredService<IDataFlowMetrics>();
        var flow = new DataFlow("BroadcastTest", new IBlock[] { producer, broadcast, processor1, processor2, processor3 }.ToList(), metrics);
        
        // Act
        var context = CreateContext("test", Guid.NewGuid(), sp);
        await flow.ExecuteAsync(context);

        // Assert - All targets should receive all items
        target1Items.Count.ShouldBe(10);
        target2Items.Count.ShouldBe(10);
        target3Items.Count.ShouldBe(10);

        target1Items.OrderBy(x => x).ShouldBe(items);
        target2Items.OrderBy(x => x).ShouldBe(items);
        target3Items.OrderBy(x => x).ShouldBe(items);
    }

    [Fact]
    public async Task BroadcastBlock_Should_Complete_All_Targets_On_Source_Completion()
    {
        // Arrange
        var items = Enumerable.Range(1, 5).ToArray();
        var target1Items = new ConcurrentBag<int>();
        var target2Items = new ConcurrentBag<int>();

        var sp = _services.BuildServiceProvider();
        
        // Create blocks manually
        var producerLogger = sp.GetRequiredService<ILogger<ProducerBlock<int>>>();
        var channelFactory = sp.GetRequiredService<IBoundedChannelFactory>();
        var producerOptions = new ProducerBlockOptions<int>
        {
            ProducersFactory = (ctx, ct) => Task.FromResult<IEnumerable<IStreamProducer<int>>>(
                new[] { new TestProducer<int>(items) })
        };
        var producer = new ProducerBlock<int>("source", producerLogger, channelFactory, producerOptions);
        
        var broadcastLogger = sp.GetRequiredService<ILogger<BroadcastBlock<int>>>();
        var broadcast = new BroadcastBlock<int>("fanout", broadcastLogger);
        broadcast.SetSource(producer);
        
        var proc1Logger = sp.GetRequiredService<ILogger<ProcessorBlock<int>>>();
        var processor1 = new ProcessorBlock<int>("processor1", proc1Logger,
            sp => new TestProcessor<int>(onProcessItem: item => target1Items.Add(item)));
        processor1.SetSource(broadcast);
        
        var proc2Logger = sp.GetRequiredService<ILogger<ProcessorBlock<int>>>();
        var processor2 = new ProcessorBlock<int>("processor2", proc2Logger,
            sp => new TestProcessor<int>(onProcessItem: item => target2Items.Add(item)));
        processor2.SetSource(broadcast);
        
        var metrics = sp.GetRequiredService<IDataFlowMetrics>();
        var flow = new DataFlow("CompletionTest", new IBlock[] { producer, broadcast, processor1, processor2 }.ToList(), metrics);
        
        // Act
        var context = CreateContext("test", Guid.NewGuid(), sp);
        await flow.ExecuteAsync(context);

        // Assert - Both targets should have received all items (indicating completion)
        target1Items.Count.ShouldBe(5);
        target2Items.Count.ShouldBe(5);
    }

    [Fact]
    public async Task BroadcastBlock_Should_Support_Single_Target()
    {
        // Arrange
        var items = Enumerable.Range(1, 5).ToArray();
        var processedItems = new ConcurrentBag<int>();

        var sp = _services.BuildServiceProvider();
        
        // Create blocks manually
        var producerLogger = sp.GetRequiredService<ILogger<ProducerBlock<int>>>();
        var channelFactory = sp.GetRequiredService<IBoundedChannelFactory>();
        var producerOptions = new ProducerBlockOptions<int>
        {
            ProducersFactory = (ctx, ct) => Task.FromResult<IEnumerable<IStreamProducer<int>>>(
                new[] { new TestProducer<int>(items) })
        };
        var producer = new ProducerBlock<int>("source", producerLogger, channelFactory, producerOptions);
        
        var broadcastLogger = sp.GetRequiredService<ILogger<BroadcastBlock<int>>>();
        var broadcast = new BroadcastBlock<int>("fanout", broadcastLogger);
        broadcast.SetSource(producer);
        
        var procLogger = sp.GetRequiredService<ILogger<ProcessorBlock<int>>>();
        var processor = new ProcessorBlock<int>("processor", procLogger,
            sp => new TestProcessor<int>(onProcessItem: item => processedItems.Add(item)));
        processor.SetSource(broadcast);
        
        var metrics = sp.GetRequiredService<IDataFlowMetrics>();
        var flow = new DataFlow("SingleTargetTest", new IBlock[] { producer, broadcast, processor }.ToList(), metrics);
        
        // Act
        var context = CreateContext("test", Guid.NewGuid(), sp);
        await flow.ExecuteAsync(context);

        // Assert
        processedItems.Count.ShouldBe(5);
        processedItems.OrderBy(x => x).ShouldBe(items);
    }

    [Fact]
    public async Task BroadcastBlock_Should_Use_CloneFunc_To_Create_Separate_Instances()
    {
        // Arrange
        var items = Enumerable.Range(1, 5).Select(i => new MutableCounter { Value = i }).ToArray();
        var target1Items = new ConcurrentBag<MutableCounter>();
        var target2Items = new ConcurrentBag<MutableCounter>();

        var sp = _services.BuildServiceProvider();
        
        // Create blocks manually
        var producerLogger = sp.GetRequiredService<ILogger<ProducerBlock<MutableCounter>>>();
        var channelFactory = sp.GetRequiredService<IBoundedChannelFactory>();
        var producerOptions = new ProducerBlockOptions<MutableCounter>
        {
            ProducersFactory = (ctx, ct) => Task.FromResult<IEnumerable<IStreamProducer<MutableCounter>>>(
                new[] { new TestProducer<MutableCounter>(items) })
        };
        var producer = new ProducerBlock<MutableCounter>("source", producerLogger, channelFactory, producerOptions);
        
        // BroadcastBlock with clone function - creates new instance for each subscriber
        var broadcastLogger = sp.GetRequiredService<ILogger<BroadcastBlock<MutableCounter>>>();
        var broadcast = new BroadcastBlock<MutableCounter>(
            "fanout", 
            broadcastLogger, 
            defaultCloneFunc: item => new MutableCounter { Value = item.Value }); // Clone function
        broadcast.SetSource(producer);
        
        var proc1Logger = sp.GetRequiredService<ILogger<ProcessorBlock<MutableCounter>>>();
        var processor1 = new ProcessorBlock<MutableCounter>("processor1", proc1Logger, 
            sp => new TestProcessor<MutableCounter>(onProcessItem: item =>
            {
                item.Value *= 10; // Modify in first processor
                target1Items.Add(item);
            }));
        processor1.SetSource(broadcast);
        
        var proc2Logger = sp.GetRequiredService<ILogger<ProcessorBlock<MutableCounter>>>();
        var processor2 = new ProcessorBlock<MutableCounter>("processor2", proc2Logger,
            sp => new TestProcessor<MutableCounter>(onProcessItem: item =>
            {
                item.Value *= 100; // Modify in second processor
                target2Items.Add(item);
            }));
        processor2.SetSource(broadcast);
        
        var metrics = sp.GetRequiredService<IDataFlowMetrics>();
        var flow = new DataFlow("CloneFuncTest", new IBlock[] { producer, broadcast, processor1, processor2 }.ToList(), metrics);
        
        // Act
        var context = CreateContext("test", Guid.NewGuid(), sp);
        await flow.ExecuteAsync(context);

        // Assert - Each target should have received cloned instances
        // The modifications made by each processor should not affect the other
        target1Items.Count.ShouldBe(5);
        target2Items.Count.ShouldBe(5);

        // Target 1 values should be original * 10
        target1Items.OrderBy(x => x.Value).Select(x => x.Value).ShouldBe(new[] { 10, 20, 30, 40, 50 });
        
        // Target 2 values should be original * 100 (not affected by target 1's modifications)
        target2Items.OrderBy(x => x.Value).Select(x => x.Value).ShouldBe(new[] { 100, 200, 300, 400, 500 });
    }

    /// <summary>
    /// Test class with mutable state to verify cloning behavior
    /// </summary>
    [Fact]
    public async Task BroadcastBlock_WithTargetConfiguration_UsesPerTargetCloneFunctions()
    {
        // Arrange
        var items = new[] { 1, 2, 3 };
        var sp = _services.BuildServiceProvider();
        
        var channelFactory = sp.GetRequiredService<IBoundedChannelFactory>();
        var producer = new ProducerBlock<int>("producer", 
            sp.GetRequiredService<ILogger<ProducerBlock<int>>>(),
            channelFactory,
            new ProducerBlockOptions<int>
            {
                ProducersFactory = async (context, ct) => new[] 
                {
                    new TestProducer<int>(items) 
                }
            });

        // BroadcastBlock with per-target clone configuration
        var broadcastLogger = sp.GetRequiredService<ILogger<BroadcastBlock<int>>>();
        var broadcast = new BroadcastBlock<int>(
            "fanout", 
            broadcastLogger,
            defaultCloneFunc: null); // No default cloning
        
        // Configure specific targets with clone functions
        broadcast.ConfigureTarget("doubler", item => item * 2);      // Double the value
        broadcast.ConfigureTarget("tripler", item => item * 3);      // Triple the value
        broadcast.ConfigureTarget("passthrough", null);               // No transformation
        
        broadcast.SetSource(producer);
        
        var doublerItems = new List<int>();
        var doublerLogger = sp.GetRequiredService<ILogger<ProcessorBlock<int>>>();
        var doubler = new ProcessorBlock<int>("doubler", doublerLogger, 
            sp => new TestProcessor<int>(onProcessItem: item => doublerItems.Add(item)));
        doubler.SetSource(broadcast);
        
        var triplerItems = new List<int>();
        var triplerLogger = sp.GetRequiredService<ILogger<ProcessorBlock<int>>>();
        var tripler = new ProcessorBlock<int>("tripler", triplerLogger, 
            sp => new TestProcessor<int>(onProcessItem: item => triplerItems.Add(item)));
        tripler.SetSource(broadcast);
        
        var passthroughItems = new List<int>();
        var passthroughLogger = sp.GetRequiredService<ILogger<ProcessorBlock<int>>>();
        var passthrough = new ProcessorBlock<int>("passthrough", passthroughLogger, 
            sp => new TestProcessor<int>(onProcessItem: item => passthroughItems.Add(item)));
        passthrough.SetSource(broadcast);
        
        var context = CreateContext("test", Guid.NewGuid(), sp);
        
        // Act
        await Task.WhenAll(
            producer.ExecuteAsync(context),
            broadcast.ExecuteAsync(context),
            doubler.ExecuteAsync(context),
            tripler.ExecuteAsync(context),
            passthrough.ExecuteAsync(context)
        );
        
        // Assert
        doublerItems.ShouldBe(new[] { 2, 4, 6 });           // Doubled
        triplerItems.ShouldBe(new[] { 3, 6, 9 });           // Tripled
        passthroughItems.ShouldBe(new[] { 1, 2, 3 });       // Original values
    }

    private class MutableCounter
    {
        public int Value { get; set; }
    }
}
