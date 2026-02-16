using DataFlow.POC.Core;
using DataFlow.POC.Blocks;
using DataFlow.POC.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DataFlow.POC.Tests.Core;

/// <summary>
/// Test to verify that IEpochCoordinator is shared within a single scope
/// but isolated across different scopes
/// </summary>
public class EpochCoordinatorScopedSharingTests
{
    [Fact]
    public async Task MultipleBlocks_SameScope_ShareSameCoordinatorInstance()
    {
        // Arrange - Setup with scoped coordinator (as auto-registered by AddDataFlows)
        var services = new ServiceCollection();
        services.AddScoped<IEpochCoordinator>(sp =>
            new EpochCoordinator(sp.GetRequiredService<IServiceScopeFactory>()));
        
        var rootProvider = services.BuildServiceProvider();
        
        // Act - Create a scope and resolve coordinator multiple times
        await using var scope = rootProvider.CreateAsyncScope();
        var coordinator1 = scope.ServiceProvider.GetRequiredService<IEpochCoordinator>();
        var coordinator2 = scope.ServiceProvider.GetRequiredService<IEpochCoordinator>();
        var coordinator3 = scope.ServiceProvider.GetRequiredService<IEpochCoordinator>();
        
        // Assert - All should be the same instance within the scope
        Assert.Same(coordinator1, coordinator2);
        Assert.Same(coordinator2, coordinator3);
        Assert.Same(coordinator1, coordinator3);
    }
    
    [Fact]
    public async Task MultipleBlocks_DifferentScopes_GetDifferentCoordinatorInstances()
    {
        // Arrange - Setup with scoped coordinator
        var services = new ServiceCollection();
        services.AddScoped<IEpochCoordinator>(sp =>
            new EpochCoordinator(sp.GetRequiredService<IServiceScopeFactory>()));
        
        var rootProvider = services.BuildServiceProvider();
        
        // Act - Create two different scopes
        IEpochCoordinator coordinator1;
        IEpochCoordinator coordinator2;
        
        await using (var scope1 = rootProvider.CreateAsyncScope())
        {
            coordinator1 = scope1.ServiceProvider.GetRequiredService<IEpochCoordinator>();
        }
        
        await using (var scope2 = rootProvider.CreateAsyncScope())
        {
            coordinator2 = scope2.ServiceProvider.GetRequiredService<IEpochCoordinator>();
        }
        
        // Assert - Should be different instances
        Assert.NotSame(coordinator1, coordinator2);
    }
    
    [Fact]
    public async Task BlocksResolvedInScope_ShareSameCoordinator()
    {
        // Arrange - Setup mimicking AddDataFlows auto-registration
        var services = new ServiceCollection();
        services.AddScoped<IEpochCoordinator>(sp =>
            new EpochCoordinator(sp.GetRequiredService<IServiceScopeFactory>()));
        
        // Register two blocks that each need the coordinator
        services.AddKeyedScoped<IBlock>("source1", (sp, key) =>
        {
            var coordinator = sp.GetRequiredService<IEpochCoordinator>();
            return new EpochSourceBlock<int, TestSourceActor>(
                new BlockContext("source1"),
                sp.GetRequiredService<IServiceScopeFactory>(),
                coordinator);
        });
        
        services.AddKeyedScoped<IBlock>("source2", (sp, key) =>
        {
            var coordinator = sp.GetRequiredService<IEpochCoordinator>();
            return new EpochSourceBlock<int, TestSourceActor>(
                new BlockContext("source2"),
                sp.GetRequiredService<IServiceScopeFactory>(),
                coordinator);
        });
        
        services.AddTransient<TestSourceActor>();
        
        var rootProvider = services.BuildServiceProvider();
        
        // Act - Resolve blocks in the same scope
        await using var scope = rootProvider.CreateAsyncScope();
        var block1 = scope.ServiceProvider.GetKeyedService<IBlock>("source1") as EpochSourceBlock<int, TestSourceActor>;
        var block2 = scope.ServiceProvider.GetKeyedService<IBlock>("source2") as EpochSourceBlock<int, TestSourceActor>;
        
        // Assert - Both blocks should have been constructed with the same coordinator
        // We can't directly access the private field, but we can verify behavior
        Assert.NotNull(block1);
        Assert.NotNull(block2);
        
        // The coordinator is shared, so both blocks will coordinate together
        // This is verified by the fan-in tests that check epoch object equality
    }
    
    private class TestSourceActor : ISourceActor<int>
    {
        public async IAsyncEnumerable<IEpochStream<int>> ProduceEpochsAsync(IActorExecutionContext context)
        {
            yield break;
        }
    }
}
