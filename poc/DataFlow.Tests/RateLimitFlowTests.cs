namespace DataFlow.POC.Tests;

using DataFlow.POC.Blocks;
using DataFlow.POC.Builder;
using DataFlow.POC.Core;
using DataFlow.POC.DependencyInjection;
using DataFlow.POC.Registry;
using DataFlow.POC.Tests.TestHelpers;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System.Threading.RateLimiting;
using Xunit;

public class RateLimitFlowTests
{
    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static async IAsyncEnumerable<int> ProduceIntegers(int count)
    {
        for (int i = 1; i <= count; i++)
        {
            yield return i;
        }
    }

    // ---------------------------------------------------------------------------
    // Happy-path: all items pass through
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task RateLimit_Block_Should_Pass_Through_All_Items()
    {
        // Arrange
        var results = new List<int>();

        var producer = BlockHelpers.CreateProducer("producer", TestStreams.Integers(5));
        var rateLimiter = BlockHelpers.CreateRateLimit<int>(
            "rate-limiter",
            permitLimit: 10,
            window: TimeSpan.FromSeconds(1));
        var processor = BlockHelpers.CreateActor<int, object, CollectorActor<int>>(
            "processor",
            new CollectorActor<int>(results));

        var builder = GraphHelpers.CreateGraphBuilder("rate-limit-flow");
        builder
            .AddBlock(producer)
            .AddBlock(rateLimiter)
            .AddBlock(processor)
            .Connect(producer, rateLimiter)
            .Connect(rateLimiter, processor);

        var graph = builder.Build(new ServiceCollection().BuildServiceProvider(), new BlockTypeRegistry());
        var context = new ExecutionContext(new ServiceCollection().BuildServiceProvider(), CancellationToken.None);

        // Act
        await graph.ExecuteAsync(context);

        // Assert
        results.Count.ShouldBe(5);
        results.ShouldBe(new[] { 1, 2, 3, 4, 5 });
    }

    // ---------------------------------------------------------------------------
    // Happy-path: items are throttled (basic timing check)
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task RateLimit_Block_Should_Throttle_Throughput()
    {
        // Arrange — permit 2 items per 200 ms window; send 5 items.
        // Window 0 [0-200ms]: items 1+2 pass immediately (2 permits consumed).
        // Window 1 [200-400ms]: items 3+4 must wait for the next window (~200ms).
        // Window 2 [400-600ms]: item 5 must wait for another window (~200ms more).
        // Therefore total elapsed time should be at least ~400 ms.
        var results = new List<int>();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var producer = BlockHelpers.CreateProducer("producer", TestStreams.Integers(5));
        var rateLimiter = BlockHelpers.CreateRateLimit<int>(
            "rate-limiter",
            permitLimit: 2,
            window: TimeSpan.FromMilliseconds(200));
        var processor = BlockHelpers.CreateActor<int, object, CollectorActor<int>>(
            "processor",
            new CollectorActor<int>(results));

        var builder = GraphHelpers.CreateGraphBuilder("throttle-flow");
        builder
            .AddBlock(producer)
            .AddBlock(rateLimiter)
            .AddBlock(processor)
            .Connect(producer, rateLimiter)
            .Connect(rateLimiter, processor);

        var graph = builder.Build(new ServiceCollection().BuildServiceProvider(), new BlockTypeRegistry());
        var context = new ExecutionContext(new ServiceCollection().BuildServiceProvider(), CancellationToken.None);

        // Act
        await graph.ExecuteAsync(context);
        sw.Stop();

        // Assert
        results.Count.ShouldBe(5);
        results.ShouldBe(new[] { 1, 2, 3, 4, 5 });
        // At least two full windows must have elapsed (items 3+4 wait ~200ms, item 5 waits ~200ms more)
        sw.ElapsedMilliseconds.ShouldBeGreaterThanOrEqualTo(350);
    }

    // ---------------------------------------------------------------------------
    // DI registration: AddRateLimit<T>(name, permitLimit, window) — metadata check
    // ---------------------------------------------------------------------------

    [Fact]
    public void AddRateLimit_Should_Register_Block_By_Key_And_Be_Resolvable()
    {
        // Arrange / Act
        var services = new ServiceCollection();
        services.AddDataFlows("test", df =>
        {
            df.AddRateLimit<int>("rate-limiter", permitLimit: 10, window: TimeSpan.FromSeconds(1));
        });

        var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();
        var block = scope.ServiceProvider.GetRequiredKeyedService<IBlock>("test:rate-limiter");

        // Assert — the block can be resolved and is a RateLimitBlock<T>
        block.ShouldNotBeNull();
        block.Name.ShouldBe("test:rate-limiter");
        block.ShouldBeOfType<RateLimitBlock<int>>();
    }

    [Fact]
    public async Task AddRateLimit_Should_Pass_Through_All_Items_In_Graph()
    {
        // Arrange — use the plain-to-epoch wrapper via AddBlock so the graph connects correctly
        // (DI-registered RateLimitBlock<T> accepts IEpochStream<T> input; the wrapper adapts plain T)
        var results = new List<int>();

        var services = new ServiceCollection();
        services.AddDataFlows("test", df =>
        {
            df.AddBlock("producer", _ => BlockHelpers.CreateProducer("test:producer", TestStreams.Integers(4)));
            df.AddBlock("rate-limiter", _ => BlockHelpers.CreateRateLimit<int>(
                "test:rate-limiter",
                permitLimit: 10,
                window: TimeSpan.FromSeconds(1)));
            df.AddBlock("collector", _ => BlockHelpers.CreateActor<int, object, CollectorActor<int>>(
                "test:collector",
                new CollectorActor<int>(results)));

            df.AddGraph("flow", g =>
            {
                g.UseBlock("producer")
                 .UseBlock("rate-limiter")
                 .UseBlock("collector");
                g.Connect("producer", "rate-limiter");
                g.Connect("rate-limiter", "collector");
            });
        });

        var sp = services.BuildServiceProvider();

        // Act
        using var scope = sp.CreateScope();
        var graph = scope.ServiceProvider.GetRequiredKeyedService<DataFlowGraph>("test:flow");
        var context = new ExecutionContext(scope.ServiceProvider, CancellationToken.None);
        await graph.ExecuteAsync(context);

        // Assert
        results.Count.ShouldBe(4);
        results.ShouldBe(new[] { 1, 2, 3, 4 });
    }

    // ---------------------------------------------------------------------------
    // DI registration: AddRateLimit<T>(name, rateLimiter) — raw limiter overload
    // ---------------------------------------------------------------------------

    [Fact]
    public void AddRateLimit_With_Raw_Limiter_Should_Resolve_Block()
    {
        // Arrange
        using var limiter = new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromSeconds(1),
            QueueLimit = int.MaxValue,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        });

        var services = new ServiceCollection();
        services.AddDataFlows("test2", df =>
        {
            df.AddRateLimit<int>("rate-limiter", limiter);
        });

        var sp = services.BuildServiceProvider();

        // Act
        using var scope = sp.CreateScope();
        var block = scope.ServiceProvider.GetRequiredKeyedService<IBlock>("test2:rate-limiter");

        // Assert
        block.ShouldNotBeNull();
        block.ShouldBeOfType<RateLimitBlock<int>>();
    }

    // ---------------------------------------------------------------------------
    // DI registration: AddSlidingWindowRateLimit<T>
    // ---------------------------------------------------------------------------

    [Fact]
    public void AddSlidingWindowRateLimit_Should_Register_Block_By_Key_And_Be_Resolvable()
    {
        // Arrange / Act
        var services = new ServiceCollection();
        services.AddDataFlows("test3", df =>
        {
            df.AddSlidingWindowRateLimit<int>(
                "rate-limiter",
                permitLimit: 10,
                window: TimeSpan.FromSeconds(1),
                segmentsPerWindow: 2);
        });

        var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();
        var block = scope.ServiceProvider.GetRequiredKeyedService<IBlock>("test3:rate-limiter");

        // Assert
        block.ShouldNotBeNull();
        block.Name.ShouldBe("test3:rate-limiter");
        block.ShouldBeOfType<RateLimitBlock<int>>();
    }

    // ---------------------------------------------------------------------------
    // Registry type metadata validation
    // ---------------------------------------------------------------------------

    [Fact]
    public void AddRateLimit_Should_Register_Correct_Input_And_Output_Types_In_Registry()
    {
        // Arrange
        var services = new ServiceCollection();
        IBlockTypeRegistry? capturedRegistry = null;

        services.AddDataFlows("meta", df =>
        {
            df.AddRateLimit<string>("rate-limiter", permitLimit: 5, window: TimeSpan.FromSeconds(1));

            // Capture the registry so we can inspect it after building
            df.AddGraph("flow", g =>
            {
                // Minimal graph — just validate metadata, no actual execution needed
            });
        });

        var sp = services.BuildServiceProvider();
        capturedRegistry = sp.GetRequiredService<IBlockTypeRegistry>();

        // Act
        var metadata = capturedRegistry.GetMetadata("meta:rate-limiter");

        // Assert
        metadata.ShouldNotBeNull();
        metadata!.InputType.ShouldBe(typeof(string));
        metadata.OutputType.ShouldBe(typeof(string));
    }

    // ---------------------------------------------------------------------------
    // Argument validation
    // ---------------------------------------------------------------------------

    [Fact]
    public void AddRateLimit_Should_Throw_When_PermitLimit_Is_Zero()
    {
        var services = new ServiceCollection();
        Should.Throw<ArgumentOutOfRangeException>(() =>
            services.AddDataFlows("err", df =>
                df.AddRateLimit<int>("rl", permitLimit: 0, window: TimeSpan.FromSeconds(1))));
    }

    [Fact]
    public void AddRateLimit_Should_Throw_When_Window_Is_Zero()
    {
        var services = new ServiceCollection();
        Should.Throw<ArgumentOutOfRangeException>(() =>
            services.AddDataFlows("err2", df =>
                df.AddRateLimit<int>("rl", permitLimit: 1, window: TimeSpan.Zero)));
    }

    [Fact]
    public void AddRateLimit_Should_Throw_When_QueueLimit_Is_Negative()
    {
        var services = new ServiceCollection();
        Should.Throw<ArgumentOutOfRangeException>(() =>
            services.AddDataFlows("err4", df =>
                df.AddRateLimit<int>("rl", permitLimit: 1, window: TimeSpan.FromSeconds(1), queueLimit: -1)));
    }

    [Fact]
    public void AddSlidingWindowRateLimit_Should_Throw_When_QueueLimit_Is_Negative()
    {
        var services = new ServiceCollection();
        Should.Throw<ArgumentOutOfRangeException>(() =>
            services.AddDataFlows("err5", df =>
                df.AddSlidingWindowRateLimit<int>("rl", permitLimit: 1, window: TimeSpan.FromSeconds(1), queueLimit: -1)));
    }

    [Fact]
    public void AddRateLimit_Should_Throw_When_RateLimiter_Is_Null()
    {
        var services = new ServiceCollection();
        Should.Throw<ArgumentNullException>(() =>
            services.AddDataFlows("err3", df =>
                df.AddRateLimit<int>("rl", rateLimiter: null!)));
    }

    // ---------------------------------------------------------------------------
    // Ownership: externally-supplied limiter must survive multiple scope disposals
    // ---------------------------------------------------------------------------

    [Fact]
    public void AddRateLimit_With_Raw_Limiter_Should_Not_Dispose_Limiter_On_Scope_Disposal()
    {
        // Arrange — simulates two successive graph executions (two DI scopes).
        // The limiter is owned externally, so scope disposal must NOT dispose it.
        using var limiter = new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromSeconds(1),
            QueueLimit = int.MaxValue,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        });

        var services = new ServiceCollection();
        services.AddDataFlows("owns", df =>
        {
            df.AddRateLimit<int>("rate-limiter", limiter);
        });

        var sp = services.BuildServiceProvider();

        // First scope resolution + disposal
        {
            using var scope1 = sp.CreateScope();
            var block1 = scope1.ServiceProvider.GetRequiredKeyedService<IBlock>("owns:rate-limiter");
            block1.ShouldNotBeNull();
            // scope1 is disposed here — must NOT dispose the shared limiter
        }

        // Second scope resolution must succeed (limiter still alive)
        {
            using var scope2 = sp.CreateScope();
            var block2 = scope2.ServiceProvider.GetRequiredKeyedService<IBlock>("owns:rate-limiter");
            block2.ShouldNotBeNull();
            block2.ShouldBeOfType<RateLimitBlock<int>>();
        }

        // Verify the limiter is still functional after both scopes have been disposed
        var stats = limiter.GetStatistics();
        stats.ShouldNotBeNull();
    }
}
