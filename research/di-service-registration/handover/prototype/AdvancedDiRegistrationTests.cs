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
/// Tests for advanced DI registration features:
/// - Class-based DataFlow definitions
/// - Isolated DataFlow with separate service collection
/// </summary>
public class AdvancedDiRegistrationTests
{
    /// <summary>
    /// Test class-based DataFlow definition with separate Register and Configure methods.
    /// </summary>
    [Fact]
    public async Task ClassBased_DataFlowDefinition_Should_Work()
    {
        // Arrange
        var results = new List<string>();
        
        var services = new ServiceCollection();
        services.AddScoped(sp => new TransformActor<int, string>(x => $"Value: {x}"));
        services.AddScoped(sp => new CollectorActor<string>(results));
        
        // Register class-based definition
        services.AddDataFlowDefinition<TestDataFlowDefinition>("my-flow");
        
        var serviceProvider = services.BuildServiceProvider();
        
        // Resolve the definition and build the graph
        var definition = serviceProvider.GetRequiredService<TestDataFlowDefinition>();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        
        var graphBuilder = new DataFlowGraphBuilderEx("my-flow", serviceProvider);
        definition.ConfigureGraph(graphBuilder, scopeFactory);
        var graph = graphBuilder.Build();
        
        var context = new ExecutionContext(serviceProvider, CancellationToken.None);

        // Act
        await graph.ExecuteAsync(context);

        // Assert
        results.Count.ShouldBe(5);
        results.ShouldBe(new[] { "Value: 1", "Value: 2", "Value: 3", "Value: 4", "Value: 5" });
    }

    /// <summary>
    /// Test isolated DataFlow with its own service collection.
    /// </summary>
    [Fact]
    public void IsolatedDataFlow_Should_Have_Separate_Services()
    {
        // Arrange
        var mainServices = new ServiceCollection();
        
        // Configure isolated DataFlow
        mainServices.AddIsolatedDataFlow("isolated-flow")
            .ConfigureServices(df =>
            {
                df.AddBlock("producer", sp => new ProducerBlock<int>("producer", _ => TestStreams.Integers(3)));
            })
            .ConfigureGraph((builder, sp) =>
            {
                builder.UseBlock("producer");
            })
            .BuildIsolated();
        
        var mainServiceProvider = mainServices.BuildServiceProvider();
        
        // Act - Get the isolated services collection
        var isolatedServices = mainServiceProvider.GetRequiredKeyedService<IServiceCollection>("__isolated__isolated-flow");
        
        // Assert - Isolated services should be separate from main
        isolatedServices.ShouldNotBe(mainServices);
        
        // The isolated services should have the registered blocks
        var isolatedSp = isolatedServices.BuildServiceProvider();
        var block = isolatedSp.GetKeyedService<IBlock>("producer");
        block.ShouldNotBeNull();
        block.Name.ShouldBe("producer");
    }

    /// <summary>
    /// Test that isolated DataFlow can create graphs independently.
    /// </summary>
    [Fact]
    public async Task IsolatedDataFlow_Should_Create_Independent_Graphs()
    {
        // Arrange
        var results = new List<string>();
        
        var mainServices = new ServiceCollection();
        
        // Configure isolated DataFlow with its own services
        mainServices.AddIsolatedDataFlow("isolated-flow")
            .ConfigureServices(df =>
            {
                // These services are isolated - not in main service collection
                df.AddBlock("producer", sp => new ProducerBlock<int>("producer", _ => TestStreams.Integers(3)));
            })
            .ConfigureGraph((builder, isolatedSp) =>
            {
                // Build graph using isolated services
                var scopeFactory = new ServiceCollection()
                    .AddScoped(sp => new TransformActor<int, string>(x => $"Isolated: {x}"))
                    .AddScoped(sp => new CollectorActor<string>(results))
                    .BuildServiceProvider()
                    .GetRequiredService<IServiceScopeFactory>();

                builder.UseBlock("producer")
                    .AddBlock(new ActorBlock<int, string, TransformActor<int, string>>("transformer", scopeFactory))
                    .AddBlock(new ActorBlock<string, object, CollectorActor<string>>("collector", scopeFactory))
                    .Connect("producer", "transformer")
                    .Connect("transformer", "collector");
            })
            .BuildIsolated();
        
        var mainServiceProvider = mainServices.BuildServiceProvider();
        
        // Act - Build the isolated service provider and create graph
        var isolatedServices = mainServiceProvider.GetRequiredKeyedService<IServiceCollection>("__isolated__isolated-flow");
        var isolatedSp = isolatedServices.BuildServiceProvider();
        
        var graphFactory = mainServiceProvider.GetRequiredKeyedService<Func<IServiceProvider, DataFlowGraph>>("isolated-flow");
        var graph = graphFactory(isolatedSp);
        
        var context = new ExecutionContext(isolatedSp, CancellationToken.None);
        await graph.ExecuteAsync(context);

        // Assert
        results.Count.ShouldBe(3);
        results.ShouldBe(new[] { "Isolated: 1", "Isolated: 2", "Isolated: 3" });
    }

    /// <summary>
    /// Test DataFlow definition interface implementation.
    /// </summary>
    [Fact]
    public void DataFlowDefinition_Interface_Should_Be_Implemented()
    {
        // Arrange
        var definition = new TestDataFlowDefinition();
        
        // Assert
        (definition is IDataFlowDefinition).ShouldBeTrue();
    }

    // Example class-based DataFlow definition
    public class TestDataFlowDefinition : IDataFlowDefinition
    {
        public void RegisterServices(DataFlowBuilder builder)
        {
            // Register blocks and strategies
            builder.AddBlock("producer", sp => 
                new ProducerBlock<int>("producer", _ => TestStreams.Integers(5)));
        }

        public void ConfigureGraph(DataFlowGraphBuilderEx builder)
        {
            // This signature is defined by the interface but for this test
            // we need access to the scope factory, so we'll use an overload
            throw new NotImplementedException("Use ConfigureGraph with scopeFactory parameter");
        }
        
        // Helper method for test
        public void ConfigureGraph(DataFlowGraphBuilderEx builder, IServiceScopeFactory scopeFactory)
        {
            // Build the graph structure
            builder.UseBlock("producer")
                .AddBlock(new ActorBlock<int, string, TransformActor<int, string>>("transformer", scopeFactory))
                .AddBlock(new ActorBlock<string, object, CollectorActor<string>>("collector", scopeFactory))
                .Connect("producer", "transformer")
                .Connect("transformer", "collector");
        }
    }
}
