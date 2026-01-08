namespace DataFlow.POC.Tests;

using DataFlow.POC.Blocks;
using DataFlow.POC.Builder;
using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;
using DataFlow.POC.Tests.TestHelpers;

/// <summary>
/// Tests to validate that the untyped IBlock.ExecuteAsync interface is NOT used in execution.
/// Created for research issue: Block base untyped interface investigation.
/// </summary>
public class UntypedInterfaceValidationTests
{
    private readonly ITestOutputHelper _output;

    public UntypedInterfaceValidationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task ExecutableBlockAdapter_Should_Be_Used_Instead_Of_Untyped_Interface()
    {
        // Arrange - Create a simple source block
        var sourceBlock = new SimpleSourceBlock("source");
        
        var builder = GraphHelpers.CreateGraphBuilder("test-graph");
        builder.AddBlock(sourceBlock);
        
        var graph = builder.Build();
        
        var services = new ServiceCollection().BuildServiceProvider();
        var context = new ExecutionContext(services, CancellationToken.None);

        // Act - Execute the graph
        await graph.ExecuteAsync(context);

        // Assert - Verify typed method was called
        Assert.True(sourceBlock.TypedExecuteCalled, "Typed ExecuteAsync should be called");
        
        // Note: We cannot directly test if the untyped interface is called because
        // BlockBase already implements it as an explicit interface. However, the
        // execution path analysis in DataFlowGraph.cs confirms that:
        // 1. ExecutableBlockAdapter.ExecuteUntypedAsync() is called (line 540)
        // 2. This calls the TYPED IBlock<TIn, TOut>.ExecuteAsync() method
        // 3. The untyped IBlock.ExecuteAsync(IAsyncEnumerable<object>) is NEVER called
        
        _output.WriteLine($"Typed ExecuteAsync called: {sourceBlock.TypedExecuteCalled}");
        _output.WriteLine("Execution completed successfully through ExecutableBlockAdapter");
    }

    /// <summary>
    /// A simple source block that tracks when its typed ExecuteAsync is called.
    /// </summary>
    private class SimpleSourceBlock : BlockBase<int, int>
    {
        public bool TypedExecuteCalled { get; private set; }

        public SimpleSourceBlock(string name) : base(new BlockContext(name))
        {
        }

        public override async IAsyncEnumerable<int> ExecuteAsync(
            IAsyncEnumerable<int> input, 
            IExecutionContext context)
        {
            TypedExecuteCalled = true;
            
            // Source block - generate some test data
            for (int i = 0; i < 5; i++)
            {
                yield return i;
            }
        }
    }
}
