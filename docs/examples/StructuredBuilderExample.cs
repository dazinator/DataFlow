namespace Examples.StructuredBuilder;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using Uniun.DataFlow;
using Uniun.DataFlow.Blocks;
using Uniun.DataFlow.Builder.Graph;

/// <summary>
/// Example demonstrating the structured builder approach for building dataflows.
/// This shows how to:
/// 1. Define a flow with metadata before instantiation
/// 2. Inspect the flow structure
/// 3. Export diagrams for documentation
/// 4. Execute the flow
/// </summary>
public class StructuredBuilderExample
{
    public static async Task Main(string[] args)
    {
        // Setup dependency injection
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        services.AddDataFlows();
        services.AddDataFlowMetrics();
        
        var serviceProvider = services.BuildServiceProvider();

        // Example 1: Simple data processing pipeline
        await SimpleDataPipeline(serviceProvider);

        // Example 2: Complex multi-stage pipeline
        await ComplexPipeline(serviceProvider);

        // Example 3: Graph inspection and visualization
        GraphInspectionExample(serviceProvider);
    }

    /// <summary>
    /// Example 1: Simple data processing pipeline
    /// Source -> Transform -> Processor
    /// </summary>
    static async Task SimpleDataPipeline(IServiceProvider serviceProvider)
    {
        Console.WriteLine("=== Example 1: Simple Data Pipeline ===");
        Console.WriteLine();

        // Create the structured builder
        var builder = new StructuredDataFlowBuilder(serviceProvider, "SimpleDataPipeline");

        // Define the flow - blocks and connections are recorded but not instantiated yet
        builder.AddProducer("dataSource", sp => 
                new RangeProducer(1, 10))
            .AddTransform("doubler", sp => 
                new DoublerTransform())
            .AddProcessor("printer", sp => 
                new PrintProcessor());

        // At this point, we can inspect the flow structure
        Console.WriteLine($"Blocks defined: {builder.Graph.BlockDefinitions.Count}");
        Console.WriteLine($"Connections: {builder.Graph.Connections.Count}");
        Console.WriteLine();

        // Generate a Mermaid diagram
        Console.WriteLine("Flow Diagram (Mermaid):");
        Console.WriteLine(builder.Graph.ToMermaidDiagram());
        Console.WriteLine();

        // Now build the actual dataflow (instantiates blocks)
        var dataflow = builder.Build();

        // Execute the flow
        var context = new DataFlowContext
        {
            InvocationId = Guid.NewGuid(),
            CancellationToken = CancellationToken.None
        };

        await dataflow.ExecuteAsync(context);
        
        Console.WriteLine();
    }

    /// <summary>
    /// Example 2: Complex multi-stage pipeline with batching
    /// Source -> Transform -> Batch -> Processor
    /// </summary>
    static async Task ComplexPipeline(IServiceProvider serviceProvider)
    {
        Console.WriteLine("=== Example 2: Complex Pipeline with Batching ===");
        Console.WriteLine();

        var builder = new StructuredDataFlowBuilder(serviceProvider, "ComplexBatchPipeline");

        // Build a more complex flow
        builder.AddProducer("dataGenerator", sp => 
                new RangeProducer(1, 20))
            .AddTransform("squarer", sp => 
                new SquareTransform())
            .AddBatch("batcher", 
                maxBatchSize: 5, 
                windowPeriod: TimeSpan.FromSeconds(1))
            .AddProcessor("batchPrinter", sp => 
                new BatchPrintProcessor());

        // Get a summary of the flow
        var summary = builder.Graph.GetSummary();
        Console.WriteLine($"Flow: {summary.Name}");
        Console.WriteLine($"Total Blocks: {summary.BlockCount}");
        Console.WriteLine($"Source Blocks: {summary.SourceBlockCount}");
        Console.WriteLine($"Target Blocks: {summary.TargetBlockCount}");
        Console.WriteLine();

        // Show text diagram
        Console.WriteLine("Text Diagram:");
        Console.WriteLine(builder.Graph.ToTextDiagram());
        Console.WriteLine();

        // Build and execute
        var dataflow = builder.Build();
        
        var context = new DataFlowContext
        {
            InvocationId = Guid.NewGuid(),
            CancellationToken = CancellationToken.None
        };

        await dataflow.ExecuteAsync(context);
        
        Console.WriteLine();
    }

    /// <summary>
    /// Example 3: Demonstrating graph inspection capabilities
    /// </summary>
    static void GraphInspectionExample(IServiceProvider serviceProvider)
    {
        Console.WriteLine("=== Example 3: Graph Inspection ===");
        Console.WriteLine();

        var builder = new StructuredDataFlowBuilder(serviceProvider, "InspectionExample");

        // Create a flow with multiple paths
        builder.AddProducer("source", sp => new RangeProducer(1, 5));
        
        builder.AddTransform("transform1", sp => new DoublerTransform())
            .ReceiveFrom("source");
        
        builder.AddTransform("transform2", sp => new SquareTransform())
            .ReceiveFrom("source");
        
        builder.AddProcessor("processor", sp => new PrintProcessor())
            .ReceiveFrom("transform1");

        // Inspect block definitions
        Console.WriteLine("Block Definitions:");
        foreach (var block in builder.Graph.BlockDefinitions.Values)
        {
            Console.WriteLine($"  Name: {block.Name}");
            Console.WriteLine($"    Type: {block.BlockType.Name}");
            Console.WriteLine($"    Input: {block.InputType?.Name ?? "none"}");
            Console.WriteLine($"    Output: {block.OutputType?.Name ?? "none"}");
        }
        Console.WriteLine();

        // Inspect connections
        Console.WriteLine("Connections:");
        foreach (var conn in builder.Graph.Connections)
        {
            Console.WriteLine($"  {conn.SourceBlockName} --({conn.DataType?.Name ?? "data"})--> {conn.TargetBlockName}");
        }
        Console.WriteLine();

        // Analyze the source block
        Console.WriteLine("Analyzing 'source' block:");
        var outgoing = builder.Graph.GetOutgoingConnections("source").ToList();
        Console.WriteLine($"  Outgoing connections: {outgoing.Count}");
        foreach (var conn in outgoing)
        {
            Console.WriteLine($"    -> {conn.TargetBlockName}");
        }
        Console.WriteLine();

        // Generate Mermaid diagram
        Console.WriteLine("Mermaid Diagram:");
        Console.WriteLine(builder.Graph.ToMermaidDiagram("TB")); // Top to Bottom layout
        Console.WriteLine();
    }
}

#region Helper Classes

/// <summary>
/// Producer that generates a range of integers.
/// </summary>
public class RangeProducer : IStreamProducer<int>
{
    private readonly int _start;
    private readonly int _end;

    public RangeProducer(int start, int end)
    {
        _start = start;
        _end = end;
    }

    public async IAsyncEnumerable<int> ProduceAsync(
        IDataFlowContext context, 
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        for (int i = _start; i <= _end; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return i;
            await Task.Delay(10, cancellationToken); // Simulate work
        }
    }
}

/// <summary>
/// Transformer that doubles input values.
/// </summary>
public class DoublerTransform : IStreamTransformer<int, int>
{
    public async IAsyncEnumerable<int> TransformAsync(
        IDataFlowContext context, 
        IAsyncEnumerable<int> input, 
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var item in input.WithCancellation(cancellationToken))
        {
            yield return item * 2;
        }
    }
}

/// <summary>
/// Transformer that squares input values.
/// </summary>
public class SquareTransform : IStreamTransformer<int, int>
{
    public async IAsyncEnumerable<int> TransformAsync(
        IDataFlowContext context, 
        IAsyncEnumerable<int> input, 
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var item in input.WithCancellation(cancellationToken))
        {
            yield return item * item;
        }
    }
}

/// <summary>
/// Processor that prints individual items.
/// </summary>
public class PrintProcessor : IStreamProcessor<int>
{
    public async Task ProcessAsync(
        IDataFlowContext context, 
        IAsyncEnumerable<int> input, 
        CancellationToken cancellationToken)
    {
        await foreach (var item in input.WithCancellation(cancellationToken))
        {
            Console.WriteLine($"  Processed: {item}");
        }
    }
}

/// <summary>
/// Processor that prints batches of items.
/// </summary>
public class BatchPrintProcessor : IStreamProcessor<int[]>
{
    public async Task ProcessAsync(
        IDataFlowContext context, 
        IAsyncEnumerable<int[]> input, 
        CancellationToken cancellationToken)
    {
        await foreach (var batch in input.WithCancellation(cancellationToken))
        {
            Console.WriteLine($"  Batch of {batch.Length}: [{string.Join(", ", batch)}]");
        }
    }
}

#endregion
