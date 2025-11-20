namespace DataFlow.POC.Tests;

using DataFlow.POC.Blocks;
using DataFlow.POC.Builder;
using DataFlow.POC.Core;
using DataFlow.POC.DependencyInjection;
using DataFlow.POC.Registry;
using DataFlow.POC.Tests.TestHelpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// Tests for the centralized block type registry.
/// Validates registration, lookup, introspection, and integration with DI.
/// </summary>
public class BlockTypeRegistryTests
{
    #region Basic Registration and Lookup

    [Fact]
    public void RegisterBlock_WithValidMetadata_Succeeds()
    {
        // Arrange
        var registry = new BlockTypeRegistry();
        var metadata = new BlockTypeMetadata(typeof(int), typeof(string));

        // Act
        registry.RegisterBlock("test:block", metadata);

        // Assert
        Assert.True(registry.IsBlockRegistered("test:block"));
    }

    [Fact]
    public void RegisterBlock_WithDuplicateKey_ThrowsInvalidOperationException()
    {
        // Arrange
        var registry = new BlockTypeRegistry();
        var metadata = new BlockTypeMetadata(typeof(int), typeof(string));
        registry.RegisterBlock("test:block", metadata);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            registry.RegisterBlock("test:block", metadata));
        Assert.Contains("already registered", ex.Message);
    }

    [Fact]
    public void RegisterBlock_WithNullKey_ThrowsArgumentNullException()
    {
        // Arrange
        var registry = new BlockTypeRegistry();
        var metadata = new BlockTypeMetadata(typeof(int), typeof(string));

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            registry.RegisterBlock(null!, metadata));
    }

    [Fact]
    public void RegisterBlock_WithNullMetadata_ThrowsArgumentNullException()
    {
        // Arrange
        var registry = new BlockTypeRegistry();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            registry.RegisterBlock("test:block", null!));
    }

    #endregion

    #region Metadata Retrieval

    [Fact]
    public void GetMetadata_WithRegisteredBlock_ReturnsCorrectMetadata()
    {
        // Arrange
        var registry = new BlockTypeRegistry();
        var metadata = new BlockTypeMetadata(typeof(int), typeof(string));
        registry.RegisterBlock("test:block", metadata);

        // Act
        var retrieved = registry.GetMetadata("test:block");

        // Assert
        Assert.Same(metadata, retrieved);
        Assert.Equal(typeof(int), retrieved.InputType);
        Assert.Equal(typeof(string), retrieved.OutputType);
    }

    [Fact]
    public void GetMetadata_WithUnregisteredBlock_ThrowsKeyNotFoundException()
    {
        // Arrange
        var registry = new BlockTypeRegistry();

        // Act & Assert
        var ex = Assert.Throws<KeyNotFoundException>(() =>
            registry.GetMetadata("test:missing"));
        Assert.Contains("not registered", ex.Message);
    }

    [Fact]
    public void TryGetMetadata_WithRegisteredBlock_ReturnsTrue()
    {
        // Arrange
        var registry = new BlockTypeRegistry();
        var metadata = new BlockTypeMetadata(typeof(int), typeof(string));
        registry.RegisterBlock("test:block", metadata);

        // Act
        var result = registry.TryGetMetadata("test:block", out var retrieved);

        // Assert
        Assert.True(result);
        Assert.NotNull(retrieved);
        Assert.Same(metadata, retrieved);
    }

    [Fact]
    public void TryGetMetadata_WithUnregisteredBlock_ReturnsFalse()
    {
        // Arrange
        var registry = new BlockTypeRegistry();

        // Act
        var result = registry.TryGetMetadata("test:missing", out var retrieved);

        // Assert
        Assert.False(result);
        Assert.Null(retrieved);
    }

    #endregion

    #region Block Resolution

    [Fact]
    public void GetBlock_WithRegisteredBlock_ResolvesFromDI()
    {
        // Arrange
        var services = new ServiceCollection();
        var registry = new BlockTypeRegistry();
        var metadata = new BlockTypeMetadata(typeof(int), typeof(string));
        registry.RegisterBlock("global:producer", metadata);
        
        services.AddSingleton<IBlockTypeRegistry>(registry);
        services.AddKeyedScoped<IBlock>("global:producer", (sp, key) => new TestProducerBlock());
        
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var block = registry.GetBlock(serviceProvider, "global:producer");

        // Assert
        Assert.NotNull(block);
        Assert.IsType<TestProducerBlock>(block);
    }

    [Fact]
    public void GetBlock_WithUnregisteredBlock_ThrowsInvalidOperationException()
    {
        // Arrange
        var services = new ServiceCollection();
        var registry = new BlockTypeRegistry();
        var serviceProvider = services.BuildServiceProvider();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            registry.GetBlock(serviceProvider, "global:missing"));
        Assert.Contains("not found", ex.Message);
    }

    [Fact]
    public void TryGetBlock_WithRegisteredBlock_ReturnsTrue()
    {
        // Arrange
        var services = new ServiceCollection();
        var registry = new BlockTypeRegistry();
        var metadata = new BlockTypeMetadata(typeof(int), typeof(string));
        registry.RegisterBlock("global:producer", metadata);
        
        services.AddSingleton<IBlockTypeRegistry>(registry);
        services.AddKeyedScoped<IBlock>("global:producer", (sp, key) => new TestProducerBlock());
        
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var result = registry.TryGetBlock(serviceProvider, "global:producer", out var block);

        // Assert
        Assert.True(result);
        Assert.NotNull(block);
        Assert.IsType<TestProducerBlock>(block);
    }

    [Fact]
    public void TryGetBlock_WithUnregisteredBlock_ReturnsFalse()
    {
        // Arrange
        var services = new ServiceCollection();
        var registry = new BlockTypeRegistry();
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var result = registry.TryGetBlock(serviceProvider, "global:missing", out var block);

        // Assert
        Assert.False(result);
        Assert.Null(block);
    }

    #endregion

    #region Introspection

    [Fact]
    public void GetAllBlockKeys_ReturnsAllRegisteredKeys()
    {
        // Arrange
        var registry = new BlockTypeRegistry();
        registry.RegisterBlock("global:producer", new BlockTypeMetadata(typeof(int), typeof(int)));
        registry.RegisterBlock("global:transformer", new BlockTypeMetadata(typeof(int), typeof(string)));
        registry.RegisterBlock("moduleA:processor", new BlockTypeMetadata(typeof(string), typeof(bool)));

        // Act
        var keys = registry.GetAllBlockKeys().ToList();

        // Assert
        Assert.Equal(3, keys.Count);
        Assert.Contains("global:producer", keys);
        Assert.Contains("global:transformer", keys);
        Assert.Contains("moduleA:processor", keys);
    }

    [Fact]
    public void GetAllBlockMetadata_ReturnsAllRegisteredMetadata()
    {
        // Arrange
        var registry = new BlockTypeRegistry();
        registry.RegisterBlock("global:producer", new BlockTypeMetadata(typeof(int), typeof(int)));
        registry.RegisterBlock("global:transformer", new BlockTypeMetadata(typeof(int), typeof(string)));

        // Act
        var metadata = registry.GetAllBlockMetadata().ToList();

        // Assert
        Assert.Equal(2, metadata.Count);
        Assert.Contains(metadata, m => m.Key == "global:producer" && m.Value.InputType == typeof(int));
        Assert.Contains(metadata, m => m.Key == "global:transformer" && m.Value.OutputType == typeof(string));
    }

    [Fact]
    public void GetBlockKeys_WithNamespaceFilter_ReturnsMatchingKeys()
    {
        // Arrange
        var registry = new BlockTypeRegistry();
        registry.RegisterBlock("global:producer", new BlockTypeMetadata(typeof(int), typeof(int)));
        registry.RegisterBlock("global:transformer", new BlockTypeMetadata(typeof(int), typeof(string)));
        registry.RegisterBlock("moduleA:processor", new BlockTypeMetadata(typeof(string), typeof(bool)));
        registry.RegisterBlock("moduleB:processor", new BlockTypeMetadata(typeof(bool), typeof(int)));

        // Act
        var globalKeys = registry.GetBlockKeys("global").ToList();
        var moduleAKeys = registry.GetBlockKeys("moduleA").ToList();

        // Assert
        Assert.Equal(2, globalKeys.Count);
        Assert.Contains("global:producer", globalKeys);
        Assert.Contains("global:transformer", globalKeys);
        
        Assert.Single(moduleAKeys);
        Assert.Contains("moduleA:processor", moduleAKeys);
    }

    [Fact]
    public void GetBlockKeys_WithNamespaceWithColon_ReturnsMatchingKeys()
    {
        // Arrange
        var registry = new BlockTypeRegistry();
        registry.RegisterBlock("global:producer", new BlockTypeMetadata(typeof(int), typeof(int)));
        registry.RegisterBlock("moduleA:processor", new BlockTypeMetadata(typeof(string), typeof(bool)));

        // Act - namespace with trailing colon should work
        var keys = registry.GetBlockKeys("global:").ToList();

        // Assert
        Assert.Single(keys);
        Assert.Contains("global:producer", keys);
    }

    #endregion

    #region Integration with DI Registration

    [Fact]
    public void AddDataFlows_RegistersBlocksInRegistry()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddDataFlows("global", df =>
        {
            df.AddBlock("producer", sp => new TestProducerBlock());
            df.AddBlock("transformer", sp => new TestTransformerBlock());
        });

        var serviceProvider = services.BuildServiceProvider();
        var registry = serviceProvider.GetRequiredService<IBlockTypeRegistry>();

        // Assert
        Assert.True(registry.IsBlockRegistered("global:producer"));
        Assert.True(registry.IsBlockRegistered("global:transformer"));
        
        var metadata = registry.GetMetadata("global:producer");
        Assert.NotNull(metadata);
    }

    [Fact]
    public void AddDataFlows_WithTypedActorBlock_RegistersCorrectMetadata()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddDataFlows("global", df =>
        {
            df.AddActorBlock<int, string, TestStringifyActor>("stringifier");
        });

        var serviceProvider = services.BuildServiceProvider();
        var registry = serviceProvider.GetRequiredService<IBlockTypeRegistry>();

        // Assert
        Assert.True(registry.IsBlockRegistered("global:stringifier"));
        
        var metadata = registry.GetMetadata("global:stringifier");
        Assert.Equal(typeof(int), metadata.InputType);
        Assert.Equal(typeof(string), metadata.OutputType);
    }

    [Fact]
    public void AddDataFlows_AcrossMultipleNamespaces_SharesSameRegistry()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act - Register blocks in different namespaces
        services.AddDataFlows("global", df =>
        {
            df.AddBlock("producer", sp => new TestProducerBlock());
        });

        services.AddDataFlows("moduleA", df =>
        {
            df.AddBlock("processor", sp => new TestTransformerBlock());
        });

        var serviceProvider = services.BuildServiceProvider();
        var registry = serviceProvider.GetRequiredService<IBlockTypeRegistry>();

        // Assert - Both blocks should be in the same registry
        Assert.True(registry.IsBlockRegistered("global:producer"));
        Assert.True(registry.IsBlockRegistered("moduleA:processor"));
        
        var allKeys = registry.GetAllBlockKeys().ToList();
        Assert.Contains("global:producer", allKeys);
        Assert.Contains("moduleA:processor", allKeys);
    }

    [Fact]
    public void DataFlowGraphBuilder_UsesRegistryForBlockResolution()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDataFlows("global", df =>
        {
            df.AddBlock("producer", sp => new TestProducerBlock());
            df.AddBlock("transformer", sp => new TestTransformerBlock());
        });

        var serviceProvider = services.BuildServiceProvider();

        // Act - Build graph using registered blocks
        var builder = GraphHelpers.CreateGraphBuilder("test", serviceProvider);
        builder.UseBlock("producer")
            .UseBlock("transformer")
            .Connect("producer", "transformer");
        var graph = builder.Build();

        // Assert
        Assert.NotNull(graph);
        Assert.Equal("test", graph.Name);
    }

    #endregion

    #region Thread Safety

    [Fact]
    public void Registry_ConcurrentRegistration_IsThreadSafe()
    {
        // Arrange
        var registry = new BlockTypeRegistry();
        var tasks = new List<Task>();
        var successCount = 0;
        var failureCount = 0;

        // Act - Try to register the same block concurrently
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    registry.RegisterBlock("test:block", new BlockTypeMetadata(typeof(int), typeof(string)));
                    Interlocked.Increment(ref successCount);
                }
                catch (InvalidOperationException)
                {
                    // Expected for duplicate registrations
                    Interlocked.Increment(ref failureCount);
                }
            }));
        }

        Task.WaitAll(tasks.ToArray());

        // Assert - Exactly one should succeed, rest should fail
        Assert.Equal(1, successCount);
        Assert.Equal(9, failureCount);
        Assert.True(registry.IsBlockRegistered("test:block"));
    }

    [Fact]
    public void Registry_ConcurrentReadAndWrite_IsThreadSafe()
    {
        // Arrange
        var registry = new BlockTypeRegistry();
        registry.RegisterBlock("test:block1", new BlockTypeMetadata(typeof(int), typeof(string)));
        
        var tasks = new List<Task>();

        // Act - Concurrent reads and writes
        for (int i = 0; i < 5; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() =>
            {
                // Write
                registry.RegisterBlock($"test:block{index + 2}", new BlockTypeMetadata(typeof(int), typeof(string)));
            }));

            tasks.Add(Task.Run(() =>
            {
                // Read
                var keys = registry.GetAllBlockKeys().ToList();
                Assert.NotEmpty(keys);
            }));
        }

        Task.WaitAll(tasks.ToArray());

        // Assert
        var allKeys = registry.GetAllBlockKeys().ToList();
        Assert.Equal(6, allKeys.Count); // block1 + block2-6
    }

    #endregion

    #region Helper Test Blocks and Actors

    private class TestProducerBlock : BlockBase<object, int>
    {
        private readonly List<int> _items = new() { 1, 2, 3 };

        public TestProducerBlock() : base(new BlockContext("producer")) { }

        public override async IAsyncEnumerable<int> ExecuteAsync(
            IAsyncEnumerable<object> input,
            IExecutionContext context)
        {
            foreach (var item in _items)
            {
                yield return item;
            }
            await Task.CompletedTask;
        }
    }

    private class TestTransformerBlock : BlockBase<int, string>
    {
        public TestTransformerBlock() : base(new BlockContext("transformer")) { }

        public override async IAsyncEnumerable<string> ExecuteAsync(
            IAsyncEnumerable<int> input,
            IExecutionContext context)
        {
            await foreach (var item in input)
            {
                yield return item.ToString();
            }
        }
    }

    private class TestStringifyActor : IStreamActor<int, string>
    {
        public async IAsyncEnumerable<string> RunAsync(
            IAsyncEnumerable<int> input,
            IActorExecutionContext context)
        {
            await foreach (var item in input)
            {
                yield return item.ToString();
            }
        }
    }

    #endregion
}
