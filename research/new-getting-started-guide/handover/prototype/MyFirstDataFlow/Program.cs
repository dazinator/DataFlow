using System.Runtime.CompilerServices;

// ===== MAIN PROGRAM (must be first) =====
var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();

// Register DataFlow with namespace "app"
services.AddDataFlows("app", df =>
{
    // Register blocks with names
    df.AddBlock("input", sp => new ConsoleInputBlock());
    df.AddBlock("uppercase", sp => new UppercaseBlock());
    df.AddBlock("writer", sp => new ConsoleWriterBlock());
    
    // Define graph
    df.AddGraph("main", g =>
    {
        g.UseBlock("input")          // Reference by name
         .UseBlock("uppercase")
         .UseBlock("writer")
         .Connect("input", "uppercase")
         .Connect("uppercase", "writer");
    });
});

var serviceProvider = services.BuildServiceProvider();

// Resolve graph using keyed services
var graph = serviceProvider.GetKeyedService<DataFlowGraph>("app:main");

if (graph == null)
{
    Console.WriteLine("ERROR: Graph not found!");
    return;
}

// Create execution context
var context = new DataFlow.POC.Core.ExecutionContext(serviceProvider, CancellationToken.None);

// Execute!
await graph.ExecuteAsync(context);

Console.WriteLine("Pipeline completed!");

// ===== BLOCK DEFINITIONS (must come after top-level statements) =====

// Producer: Reads console input
public class ConsoleInputBlock : BlockBase<object, string>
{
    public ConsoleInputBlock() : base(new BlockContext("input")) { }

    public override async IAsyncEnumerable<string> ExecuteAsync(
        IAsyncEnumerable<object> input,
        IExecutionContext context)
    {
        Console.WriteLine("Enter lines (empty to quit):");
        while (!context.CancellationToken.IsCancellationRequested)
        {
            var line = Console.ReadLine();
            if (string.IsNullOrEmpty(line)) break;
            yield return line;
            await Task.CompletedTask;
        }
    }
}

// Transformer: Converts to uppercase
public class UppercaseBlock : BlockBase<string, string>
{
    public UppercaseBlock() : base(new BlockContext("uppercase")) { }

    public override async IAsyncEnumerable<string> ExecuteAsync(
        IAsyncEnumerable<string> input,
        IExecutionContext context)
    {
        await foreach (var item in input.WithCancellation(context.CancellationToken))
        {
            yield return item.ToUpperInvariant();
        }
    }
}

// Processor: Writes to console
public class ConsoleWriterBlock : BlockBase<string, object>
{
    public ConsoleWriterBlock() : base(new BlockContext("writer")) { }

    public override async IAsyncEnumerable<object> ExecuteAsync(
        IAsyncEnumerable<string> input,
        IExecutionContext context)
    {
        await foreach (var item in input.WithCancellation(context.CancellationToken))
        {
            Console.WriteLine($"Output: {item}");
        }
        yield break; // Terminal block
    }
}
