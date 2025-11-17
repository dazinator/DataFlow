namespace DataFlow.POC.Tests;

using DataFlow.POC.Blocks;
using DataFlow.POC.Builder;
using DataFlow.POC.Core;
using DataFlow.POC.DependencyInjection;
using DataFlow.POC.Tests.TestHelpers;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

/// <summary>
/// Demonstrates the new canonical DI registration API for DataFlow components.
/// Shows before/after comparison of registration patterns.
/// </summary>
public class DiServiceRegistrationTests
{
    /// <summary>
    /// BEFORE: Traditional approach - inline block creation in graph builder.
    /// </summary>
    [Fact]
    public async Task Traditional_Approach_Inline_Block_Creation()
    {
        // Arrange - blocks created inline
        var results = new List<string>();
        
        var scopeFactory = TestServiceBuilder.Create()
            .WithScoped(new TransformActor<int, string>(x => $"Value: {x}"))
            .WithScoped(new CollectorActor<string>(results))
            .BuildScopeFactory();

        var producer = new ProducerBlock<int>("producer", _ => TestStreams.Integers(5));
        var transformer = new ActorBlock<int, string, TransformActor<int, string>>(
            "transformer",
            scopeFactory);
        var processor = new ActorBlock<string, object, CollectorActor<string>>(
            "processor",
            scopeFactory);

        // Build graph with inline instances
        var builder = new DataFlowGraphBuilder("traditional-flow");
        builder.AddBlock(producer)
            .AddBlock(transformer)
            .AddBlock(processor)
            .Connect(producer, transformer)
            .Connect(transformer, processor);

        var graph = builder.Build();
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var context = new ExecutionContext(serviceProvider, CancellationToken.None);

        // Act
        await graph.ExecuteAsync(context);

        // Assert
        results.Count.ShouldBe(5);
        results.ShouldBe(new[] { "Value: 1", "Value: 2", "Value: 3", "Value: 4", "Value: 5" });
    }

    /// <summary>
    /// AFTER: New canonical approach - register blocks with DI, reference by name.
    /// </summary>
    [Fact]
    public async Task New_Canonical_Approach_DI_Registration()
    {
        // Arrange - centralized service registration
        var results = new List<string>();
        
        var services = new ServiceCollection();
        
        // Register actors
        services.AddScoped(sp => new TransformActor<int, string>(x => $"Value: {x}"));
        services.AddScoped(sp => new CollectorActor<string>(results));
        
        // NEW: Register DataFlow components with canonical DI pattern
        services.AddDataFlows(df => 
        {
            df.AddBlock("producer", sp => 
                new ProducerBlock<int>("producer", _ => TestStreams.Integers(5)));
            
            df.AddBlock("transformer", sp =>
            {
                var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
                return new ActorBlock<int, string, TransformActor<int, string>>(
                    "transformer",
                    scopeFactory);
            });
            
            df.AddBlock("processor", sp =>
            {
                var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
                return new ActorBlock<string, object, CollectorActor<string>>(
                    "processor",
                    scopeFactory);
            });
        });

        var serviceProvider = services.BuildServiceProvider();

        // NEW: Build graph using registered blocks
        var builder = new DataFlowGraphBuilderEx("canonical-flow", serviceProvider);
        builder.UseBlock("producer")
            .UseBlock("transformer")
            .UseBlock("processor")
            .Connect("producer", "transformer")
            .Connect("transformer", "processor");

        var graph = builder.Build();
        var context = new ExecutionContext(serviceProvider, CancellationToken.None);

        // Act
        await graph.ExecuteAsync(context);

        // Assert
        results.Count.ShouldBe(5);
        results.ShouldBe(new[] { "Value: 1", "Value: 2", "Value: 3", "Value: 4", "Value: 5" });
    }

    /// <summary>
    /// Demonstrates mixing both approaches - some blocks registered, some inline.
    /// This provides backward compatibility.
    /// </summary>
    [Fact]
    public async Task Hybrid_Approach_Mix_DI_And_Inline()
    {
        // Arrange
        var results = new List<string>();
        
        var services = new ServiceCollection();
        services.AddScoped(sp => new TransformActor<int, string>(x => $"Value: {x}"));
        services.AddScoped(sp => new CollectorActor<string>(results));
        
        // Register only transformer with DI
        services.AddDataFlows(df => 
        {
            df.AddBlock("transformer", sp =>
            {
                var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
                return new ActorBlock<int, string, TransformActor<int, string>>(
                    "transformer",
                    scopeFactory);
            });
        });

        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        // Build graph - mix registered and inline blocks
        var builder = new DataFlowGraphBuilderEx("hybrid-flow", serviceProvider);
        
        // Inline producer
        builder.AddBlock(new ProducerBlock<int>("producer", _ => TestStreams.Integers(5)))
            .UseBlock("transformer")  // From DI
            // Inline processor
            .AddBlock(new ActorBlock<string, object, CollectorActor<string>>(
                "processor",
                scopeFactory))
            .Connect("producer", "transformer")
            .Connect("transformer", "processor");

        var graph = builder.Build();
        var context = new ExecutionContext(serviceProvider, CancellationToken.None);

        // Act
        await graph.ExecuteAsync(context);

        // Assert
        results.Count.ShouldBe(5);
        results.ShouldBe(new[] { "Value: 1", "Value: 2", "Value: 3", "Value: 4", "Value: 5" });
    }

    /// <summary>
    /// Demonstrates error handling when trying to use unregistered block.
    /// </summary>
    [Fact]
    public void UseBlock_Should_Throw_When_Block_Not_Registered()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();

        var builder = new DataFlowGraphBuilderEx("test-flow", serviceProvider);

        // Act & Assert
        var ex = Should.Throw<InvalidOperationException>(() => 
            builder.UseBlock("non-existent"));
        
        ex.Message.ShouldContain("not found in service collection");
        ex.Message.ShouldContain("AddDataFlows");
    }

    /// <summary>
    /// Demonstrates error handling when trying to use DI without service provider.
    /// </summary>
    [Fact]
    public void UseBlock_Should_Throw_When_No_ServiceProvider()
    {
        // Arrange - no service provider
        var builder = new DataFlowGraphBuilderEx("test-flow");

        // Act & Assert
        var ex = Should.Throw<InvalidOperationException>(() => 
            builder.UseBlock("some-block"));
        
        ex.Message.ShouldContain("without a service provider");
    }

    /// <summary>
    /// Demonstrates registering edge strategies (for future enhancement).
    /// </summary>
    [Fact]
    public void AddStrategy_Should_Register_EdgeStrategy()
    {
        // Arrange
        var services = new ServiceCollection();
        
        services.AddDataFlows(df => 
        {
            df.AddStrategy("competing", sp => 
                new CompetingEdgeStrategy(BufferMode.Bounded, 100));
        });

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var strategy = serviceProvider.GetKeyedService<EdgeStrategy>("competing");

        // Assert
        strategy.ShouldNotBeNull();
        strategy.EdgeType.ShouldBe(EdgeType.Competing);
    }

    /// <summary>
    /// Demonstrates that blocks are singletons - same instance reused.
    /// </summary>
    [Fact]
    public void AddBlock_Should_Register_As_Singleton()
    {
        // Arrange
        var services = new ServiceCollection();
        
        services.AddDataFlows(df => 
        {
            df.AddBlock("producer", sp => 
                new ProducerBlock<int>("producer", _ => TestStreams.Integers(5)));
        });

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var block1 = serviceProvider.GetKeyedService<IBlock>("producer");
        var block2 = serviceProvider.GetKeyedService<IBlock>("producer");

        // Assert
        block1.ShouldNotBeNull();
        block2.ShouldNotBeNull();
        ReferenceEquals(block1, block2).ShouldBeTrue();
    }
}
