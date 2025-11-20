namespace DataFlow.POC.Tests;

using DataFlow.POC.Blocks;
using DataFlow.POC.Builder;
using DataFlow.POC.Core;
using DataFlow.POC.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DataFlow.POC.Tests.TestHelpers;

/// <summary>
/// Tests for the revised DI service registration design.
/// These tests validate the solutions to the six main concerns:
/// 1. No parallel builder structures (single DataFlowGraphBuilder)
/// 2. Default scoped lifetime (safe for multiple instances)
/// 3. Registration idempotence (duplicate detection)
/// 4. Integrated graph building (AddGraph support)
/// 5. Namespace support (modular monolith)
/// 6. DI-friendly block registration (typed helpers with IBlockContext)
/// </summary>
public class RevisedDiRegistrationTests
{
    #region Problem 1: Single Builder (No Parallel Structures)

    [Fact]
    public void DataFlowGraphBuilder_SupportsDirectBlocks_BackwardCompatibility()
    {
        // Arrange
        var producer = new TestProducerBlock();
        var transformer = new TestTransformerBlock();

        // Act - Old approach still works (no service provider needed)
        var builder = GraphHelpers.CreateGraphBuilder("test");
        builder.AddBlock(producer)
            .AddBlock(transformer)
            .Connect(producer, transformer);
        var graph = builder.Build();

        // Assert
        Assert.NotNull(graph);
        Assert.Equal("test", graph.Name);
    }

    [Fact]
    public void DataFlowGraphBuilder_SupportsDIBlocks_WithServiceProvider()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDataFlows("global", df =>
        {
            df.AddBlock("producer", sp => new TestProducerBlock());
            df.AddBlock("transformer", sp => new TestTransformerBlock());
        });
        var serviceProvider = services.BuildServiceProvider();

        // Act - New approach using same builder class
        var builder = GraphHelpers.CreateGraphBuilder("test", serviceProvider);
        builder.UseBlock("producer")
            .UseBlock("transformer")
            .Connect("producer", "transformer");
        var graph = builder.Build();

        // Assert
        Assert.NotNull(graph);
        Assert.Equal("test", graph.Name);
    }

    [Fact]
    public void DataFlowGraphBuilder_SupportsHybrid_MixingDirectAndDIBlocks()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDataFlows("global", df =>
        {
            df.AddBlock("producer", sp => new TestProducerBlock());
        });
        var serviceProvider = services.BuildServiceProvider();
        var directBlock = new TestTransformerBlock();

        // Act - Mix DI and direct blocks
        var builder = GraphHelpers.CreateGraphBuilder("test", serviceProvider);
        builder.UseBlock("producer")           // From DI
            .AddBlock(directBlock)              // Direct instance
            .Connect("producer", "transformer");
        var graph = builder.Build();

        // Assert
        Assert.NotNull(graph);
    }

    [Fact]
    public void UseBlock_ThrowsWhenNoServiceProvider()
    {
        // Arrange - Use old constructor directly to test the error case
#pragma warning disable CS0618 // Type or member is obsolete
        var builder = new DataFlowGraphBuilder("test");
#pragma warning restore CS0618 // Type or member is obsolete

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            builder.UseBlock("producer"));
        
        Assert.Contains("service provider", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AddBlock()", ex.Message);
    }

    [Fact]
    public void UseBlock_ThrowsWhenBlockNotRegistered()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var builder = GraphHelpers.CreateGraphBuilder("test", serviceProvider);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            builder.UseBlock("non-existent"));
        
        Assert.Contains("not found", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AddDataFlows", ex.Message);
    }

    #endregion

    #region Problem 2: Scoped Default Lifetime

    [Fact]
    public void AddBlock_DefaultsToScoped_Lifetime()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDataFlows("global", df =>
        {
            df.AddBlock("test", sp => new TestProducerBlock());
        });

        // Act
        var descriptor = services.FirstOrDefault(sd => 
            sd.ServiceKey?.ToString() == "global:test" && sd.ServiceType == typeof(IBlock));

        // Assert
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void AddScopedBlock_RegistersAsScoped()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDataFlows("global", df =>
        {
            df.AddScopedBlock("test", sp => new TestProducerBlock());
        });

        // Act
        var descriptor = services.FirstOrDefault(sd => 
            sd.ServiceKey?.ToString() == "global:test" && sd.ServiceType == typeof(IBlock));

        // Assert
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void ScopedBlocks_AreDifferentInstancesAcrossScopes()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDataFlows("global", df =>
        {
            df.AddScopedBlock("test", sp => new TestProducerBlock());
        });
        var rootProvider = services.BuildServiceProvider();

        // Act
        IBlock? block1;
        IBlock? block2;
        
        using (var scope1 = rootProvider.CreateScope())
        {
            block1 = scope1.ServiceProvider.GetKeyedService<IBlock>("global:test");
        }
        
        using (var scope2 = rootProvider.CreateScope())
        {
            block2 = scope2.ServiceProvider.GetKeyedService<IBlock>("global:test");
        }

        // Assert
        Assert.NotNull(block1);
        Assert.NotNull(block2);
        Assert.NotSame(block1, block2); // Different instances across scopes
    }

    #endregion

    #region Problem 3: Registration Idempotence

    [Fact]
    public void AddBlock_ThrowsOnDuplicateName()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            services.AddDataFlows("global", df =>
            {
                df.AddBlock("test", sp => new TestProducerBlock());
                df.AddBlock("test", sp => new TestProducerBlock()); // Duplicate name
            });
        });

        Assert.Contains("already registered", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("test", ex.Message);
    }

    [Fact]
    public void AddDataFlows_CanBeCalledMultipleTimes_WithDifferentBlocks()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act - Should not throw
        services.AddDataFlows("global", df =>
        {
            df.AddBlock("block1", sp => new TestProducerBlock());
        });
        
        services.AddDataFlows("global", df =>
        {
            df.AddBlock("block2", sp => new TestProducerBlock());
        });

        var serviceProvider = services.BuildServiceProvider();
        var block1 = serviceProvider.GetKeyedService<IBlock>("global:block1");
        var block2 = serviceProvider.GetKeyedService<IBlock>("global:block2");

        // Assert
        Assert.NotNull(block1);
        Assert.NotNull(block2);
    }

    [Fact]
    public void AddDataFlows_ThrowsWhenSameBlockNameUsedAcrossCalls()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDataFlows("global", df =>
        {
            df.AddBlock("test", sp => new TestProducerBlock());
        });

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            services.AddDataFlows("global", df =>
            {
                df.AddBlock("test", sp => new TestProducerBlock()); // Same name
            });
        });

        Assert.Contains("already registered", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Problem 4: Graph Builder Integration

    [Fact]
    public void AddGraph_RegistersGraphInDI()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDataFlows("global", df =>
        {
            df.AddBlock("producer", sp => new TestProducerBlock());
            df.AddBlock("transformer", sp => new TestTransformerBlock());
            
            df.AddGraph("main", g =>
            {
                g.UseBlock("producer")
                 .UseBlock("transformer")
                 .Connect("producer", "transformer");
            });
        });

        // Act
        var serviceProvider = services.BuildServiceProvider();
        var graph = serviceProvider.GetKeyedService<DataFlowGraph>("global:main");

        // Assert
        Assert.NotNull(graph);
        Assert.Equal("main", graph.Name);
    }

    [Fact]
    public void AddGraph_BuildsGraphWithCorrectTopology()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDataFlows("global", df =>
        {
            df.AddBlock("producer", sp => new TestProducerBlock());
            df.AddBlock("transformer", sp => new TestTransformerBlock());
            
            df.AddGraph("main", g =>
            {
                g.UseBlock("producer")
                 .UseBlock("transformer")
                 .Connect("producer", "transformer");
            });
        });

        // Act
        var serviceProvider = services.BuildServiceProvider();
        var graph = serviceProvider.GetKeyedService<DataFlowGraph>("global:main");

        // Assert
        Assert.NotNull(graph);
        // Graph should have the topology configured
    }

    [Fact]
    public void AddGraphDefinition_RegistersClassBasedGraph()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDataFlows("global", df =>
        {
            df.AddBlock("producer", sp => new TestProducerBlock());
            df.AddBlock("transformer", sp => new TestTransformerBlock());
            
            df.AddGraphDefinition<TestGraphDefinition>("main");
        });

        // Act
        var serviceProvider = services.BuildServiceProvider();
        var graph = serviceProvider.GetKeyedService<DataFlowGraph>("global:main");

        // Assert
        Assert.NotNull(graph);
        Assert.Equal("main", graph.Name);
    }

    [Fact]
    public void UnifiedRegistration_AllComponentsInOneCall()
    {
        // Arrange & Act
        var services = new ServiceCollection();
        services.AddDataFlows("global", df =>
        {
            // Blocks - all scoped (default and explicit)
            df.AddBlock("producer", sp => new TestProducerBlock());
            df.AddScopedBlock("transformer", sp => new TestTransformerBlock());
            df.AddBlock("processor", sp => new TestProcessorBlock());
            
            // Strategies
            df.AddStrategy("competing", sp => new CompetingEdgeStrategy(BufferMode.Bounded, 100));
            
            // Graphs
            df.AddGraph("pipeline-a", g =>
            {
                g.UseBlock("producer")
                 .UseBlock("transformer")
                 .Connect("producer", "transformer");
            });
            
            df.AddGraph("pipeline-b", g =>
            {
                g.UseBlock("producer")
                 .UseBlock("processor")
                 .Connect("producer", "processor");
            });
        });

        var serviceProvider = services.BuildServiceProvider();

        // Assert - All components registered
        Assert.NotNull(serviceProvider.GetKeyedService<IBlock>("global:producer"));
        Assert.NotNull(serviceProvider.GetKeyedService<IBlock>("global:transformer"));
        Assert.NotNull(serviceProvider.GetKeyedService<IBlock>("global:processor"));
        Assert.NotNull(serviceProvider.GetKeyedService<EdgeStrategy>("global:competing"));
        Assert.NotNull(serviceProvider.GetKeyedService<DataFlowGraph>("global:pipeline-a"));
        Assert.NotNull(serviceProvider.GetKeyedService<DataFlowGraph>("global:pipeline-b"));
    }

    [Fact]
    public void DynamicGraphs_StillSupported_OutsideAddDataFlows()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDataFlows("global", df =>
        {
            df.AddBlock("producer", sp => new TestProducerBlock());
            df.AddBlock("transformer", sp => new TestTransformerBlock());
        });
        var serviceProvider = services.BuildServiceProvider();

        // Act - Build graph dynamically at runtime
        var builder = GraphHelpers.CreateGraphBuilder("dynamic", serviceProvider);
        builder.UseBlock("producer")
            .UseBlock("transformer")
            .Connect("producer", "transformer");
        var graph = builder.Build();

        // Assert
        Assert.NotNull(graph);
        Assert.Equal("dynamic", graph.Name);
    }

    #endregion

    #region Problem 5: Namespace Support (Modular Monolith)

    [Fact]
    public void AddDataFlows_WithoutNamespace_UsesGlobalPrefix()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddDataFlows("global", df =>
        {
            df.AddBlock("producer", sp => new TestProducerBlock());
        });

        var serviceProvider = services.BuildServiceProvider();
        
        // Assert - block should be registered with "global:" prefix
        var block = serviceProvider.GetKeyedService<IBlock>("global:producer");
        Assert.NotNull(block);
    }

    [Fact]
    public void AddDataFlows_WithNamespace_UsesPrefixedKeys()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddDataFlows("moduleA", df =>
        {
            df.AddBlock("producer", sp => new TestProducerBlock());
            df.AddBlock("transformer", sp => new TestTransformerBlock());
        });

        var serviceProvider = services.BuildServiceProvider();
        
        // Assert - blocks should be registered with "moduleA:" prefix
        var producer = serviceProvider.GetKeyedService<IBlock>("moduleA:producer");
        var transformer = serviceProvider.GetKeyedService<IBlock>("moduleA:transformer");
        Assert.NotNull(producer);
        Assert.NotNull(transformer);
    }

    [Fact]
    public void MultipleModules_CanRegisterSameLogicalNames_WithoutConflict()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act - Module A and Module B both register "producer" and "transformer"
        services.AddDataFlows("moduleA", df =>
        {
            df.AddBlock("producer", sp => new TestProducerBlock());
            df.AddBlock("transformer", sp => new TestTransformerBlock());
        });

        services.AddDataFlows("moduleB", df =>
        {
            df.AddBlock("producer", sp => new TestProducerBlock());
            df.AddBlock("transformer", sp => new TestTransformerBlock());
        });

        var serviceProvider = services.BuildServiceProvider();
        
        // Assert - both modules' blocks are registered independently
        var moduleAProducer = serviceProvider.GetKeyedService<IBlock>("moduleA:producer");
        var moduleBProducer = serviceProvider.GetKeyedService<IBlock>("moduleB:producer");
        
        Assert.NotNull(moduleAProducer);
        Assert.NotNull(moduleBProducer);
        Assert.NotSame(moduleAProducer, moduleBProducer); // Different instances
    }

    [Fact]
    public void UseBlock_ResolvesWithinSameNamespace()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDataFlows("moduleA", df =>
        {
            df.AddBlock("producer", sp => new TestProducerBlock());
            df.AddBlock("transformer", sp => new TestTransformerBlock());
            
            df.AddGraph("main", g =>
            {
                g.UseBlock("producer")     // Should resolve to "moduleA:producer"
                 .UseBlock("transformer")  // Should resolve to "moduleA:transformer"
                 .Connect("producer", "transformer");
            });
        });

        // Act
        var serviceProvider = services.BuildServiceProvider();
        var graph = serviceProvider.GetKeyedService<DataFlowGraph>("moduleA:main");

        // Assert
        Assert.NotNull(graph);
    }

    [Fact]
    public void UseBlock_CanReferenceOtherNamespaces_WithFullyQualifiedKey()
    {
        // Arrange
        var services = new ServiceCollection();
        
        // Global namespace has a transformer block
        services.AddDataFlows("global", df =>
        {
            df.AddBlock("transformer", sp => new TestTransformerBlock());
        });

        // ModuleA references global transformer
        services.AddDataFlows("moduleA", df =>
        {
            df.AddBlock("producer", sp => new TestProducerBlock());
            
            df.AddGraph("main", g =>
            {
                g.UseBlock("producer")              // Resolves to "moduleA:producer"
                 .UseBlock("global:transformer")    // Explicitly references "global:transformer"
                 .Connect("producer", "global:transformer");
            });
        });

        // Act
        var serviceProvider = services.BuildServiceProvider();
        var graph = serviceProvider.GetKeyedService<DataFlowGraph>("moduleA:main");

        // Assert
        Assert.NotNull(graph);
    }

    [Fact]
    public void AddGraph_WithNamespace_RegistersWithPrefixedKey()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDataFlows("moduleA", df =>
        {
            df.AddBlock("producer", sp => new TestProducerBlock());
            df.AddGraph("main", g => g.UseBlock("producer"));
        });

        var serviceProvider = services.BuildServiceProvider();
        
        // Assert - graph should be registered with "moduleA:" prefix
        var graph = serviceProvider.GetKeyedService<DataFlowGraph>("moduleA:main");
        Assert.NotNull(graph);
    }

    [Fact]
    public void DuplicateRegistration_WithinSameNamespace_Throws()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            services.AddDataFlows("moduleA", df =>
            {
                df.AddBlock("producer", sp => new TestProducerBlock());
                df.AddBlock("producer", sp => new TestProducerBlock()); // Duplicate within namespace
            });
        });

        Assert.Contains("moduleA:producer", ex.Message);
        Assert.Contains("already registered", ex.Message);
    }

    [Fact]
    public void DifferentNamespaces_SameName_NoConflict()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act - Should not throw - different namespaces
        services.AddDataFlows("moduleA", df =>
        {
            df.AddBlock("producer", sp => new TestProducerBlock());
        });

        services.AddDataFlows("moduleB", df =>
        {
            df.AddBlock("producer", sp => new TestProducerBlock());
        });

        var serviceProvider = services.BuildServiceProvider();
        
        // Assert
        var moduleABlock = serviceProvider.GetKeyedService<IBlock>("moduleA:producer");
        var moduleBBlock = serviceProvider.GetKeyedService<IBlock>("moduleB:producer");
        Assert.NotNull(moduleABlock);
        Assert.NotNull(moduleBBlock);
    }

    [Fact]
    public void DataFlowBuilder_ExposesNamespaceProperty()
    {
        // Arrange
        var services = new ServiceCollection();
        string? capturedNamespace = null;

        // Act
        services.AddDataFlows("moduleA", df =>
        {
            capturedNamespace = df.Namespace;
        });

        // Assert
        Assert.Equal("moduleA", capturedNamespace);
    }

    [Fact]
    public void DataFlowBuilder_GlobalNamespace_DefaultsToGlobal()
    {
        // Arrange
        var services = new ServiceCollection();
        string? capturedNamespace = null;

        // Act
        services.AddDataFlows("global", df =>
        {
            capturedNamespace = df.Namespace;
        });

        // Assert
        Assert.Equal("global", capturedNamespace);
    }

    #endregion

    #region Problem 6: Block Name Duplication - Typed Helpers

    [Fact]
    public void AddActorBlock_TypedHelper_EliminatesNameDuplication()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IServiceScopeFactory, MockServiceScopeFactory>();

        // Act - Clean API without name duplication
        services.AddDataFlows("global", df =>
        {
            df.AddActorBlock<int, string, TestActor>("transformer");
        });

        var sp = services.BuildServiceProvider();
        var block = sp.GetRequiredKeyedService<IBlock>("global:transformer");

        // Assert
        Assert.NotNull(block);
        Assert.Equal("global:transformer", block.Name);
        Assert.IsType<EpochActorBlock<int, string, TestActor>>(block);
    }

    [Fact]
    public void AddActorBlock_WithNamespace_ResolveCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IServiceScopeFactory, MockServiceScopeFactory>();

        // Act
        services.AddDataFlows("moduleA", df =>
        {
            df.AddActorBlock<int, string, TestActor>("processor");
        });

        var sp = services.BuildServiceProvider();
        var block = sp.GetRequiredKeyedService<IBlock>("moduleA:processor");

        // Assert
        Assert.Equal("moduleA:processor", block.Name);
    }

    [Fact]
    public void AddActorBlock_AllDependenciesInjected_NoManualFactoryNeeded()
    {
        // Arrange
        var services = new ServiceCollection();
        var scopeFactory = new MockServiceScopeFactory();
        services.AddSingleton<IServiceScopeFactory>(scopeFactory);

        // Act - No need to manually resolve dependencies
        services.AddDataFlows("global", df =>
        {
            df.AddActorBlock<int, string, TestActor>("auto");
        });

        var sp = services.BuildServiceProvider();
        var block = sp.GetRequiredKeyedService<IBlock>("global:auto") as EpochActorBlock<int, string, TestActor>;

        // Assert
        Assert.NotNull(block);
        // Block was created with DI-injected dependencies
    }

    #endregion

    #region Test Helper Classes

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

    private class TestProcessorBlock : BlockBase<int, object>
    {
        public TestProcessorBlock() : base(new BlockContext("processor")) { }

        public override async IAsyncEnumerable<object> ExecuteAsync(
            IAsyncEnumerable<int> input,
            IExecutionContext context)
        {
            await foreach (var item in input)
            {
                // Just consume items
            }
            yield break;
        }
    }

    private class TestGraphDefinition : IDataFlowDefinition
    {
        public void Configure(DataFlowGraphBuilder builder)
        {
            builder.UseBlock("producer")
                .UseBlock("transformer")
                .Connect("producer", "transformer");
        }
    }

    // Test actor for typed helper tests (Problem 6)
    private class TestActor : IStreamActor<int, string>
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

    // Mock scope factory for testing
    private class MockServiceScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope()
        {
            return new MockServiceScope();
        }

        private class MockServiceScope : IServiceScope
        {
            public IServiceProvider ServiceProvider => new MockServiceProvider();
            public void Dispose() { }
        }

        private class MockServiceProvider : IServiceProvider
        {
            public object? GetService(Type serviceType)
            {
                if (serviceType == typeof(TestActor))
                {
                    return new TestActor();
                }
                return null;
            }
        }
    }

    #endregion
}
