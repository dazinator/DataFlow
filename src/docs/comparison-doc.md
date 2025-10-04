# Comparison to Microsoft TPL DataFlow

This document outlines the key differences between our DataFlow implementation and Microsoft's TPL DataFlow library.

## Architecture: Pull vs Push Models

Both TPL DataFlow and our implementation use buffering and asynchronous message passing, but differ in how data flows between blocks:

### TPL DataFlow
Uses a primarily push-based model where source blocks actively send data to target blocks:

```csharp
var sourceBlock = new TransformBlock<int, int>(i => i * 2);
var targetBlock = new ActionBlock<int>(i => Console.WriteLine(i));

// Source block will try to push items to target
sourceBlock.LinkTo(targetBlock);
```

Characteristics:
- Offers both synchronous and asynchronous posting options
- Configurable bounded/unbounded buffering per block
- Backpressure through bounded buffers and flow control
- Suitable for scenarios requiring immediate feedback on message acceptance
- Useful for real-time trading/market data where immediate post failure is desired

### Our Implementation
Uses a pull-based model built on System.Threading.Channels where target blocks request data from source blocks:

```csharp
builder
    .AddProducer<int>("source", producer)
    .AddProcessor<int>("target", processor)
    .ReceiveFrom("source");  // Target requests data from source
```

Characteristics:
- Built on modern System.Threading.Channels
- Always asynchronous
- Centralized buffering through channels
- Backpressure through channel capacity
- Designed for async producer/consumer patterns
- Optimized for throughput over immediate feedback

### When to Use Each

Push-based (TPL DataFlow) may be preferable when:
- Immediate feedback on message acceptance is required
- Fine-grained buffer control per block is needed
- Synchronous operations are necessary
- Working with real-time market data or trading systems

Pull-based (Our Implementation) may be preferable when:
- Building typical async producer/consumer workflows
- Using modern .NET features and patterns
- Throughput is prioritized over immediate feedback
- Simpler buffering model is desired

Both approaches can handle high-performance scenarios when properly configured. The choice often depends on specific requirements around message handling, feedback, and flow control.

## Dependency Injection and Service Lifetimes

### TPL DataFlow
- Doesn't have built-in DI support
- Requires manual construction and wiring of blocks
- No built-in scoping for block execution
- Complex to manage dependencies for concurrent operations

Example:
```csharp
// Manual dependency management
var service = new MyService();
var block = new TransformBlock<Data, Result>(
    async data => {
        // Manual service usage
        return await service.ProcessAsync(data);
    });
```

### Our DataFlow
- First-class DI support
- Automatic scope management per execution
- Can use scoped services safely with concurrent operations
- Clean integration with standard DI patterns

Example:
```csharp
builder.AddProcessor<Data, MyProcessor>("processor")
    .WithBlock(b => b.UseSeperateScopes = true); // each concurrent activity that uses the MyProcessor, gets its own scope to resolve MyProcessor from

public class MyProcessor : IStreamProcessor<Data> 
{
    private readonly IMyService _service;
    public MyProcessor(IMyService service) => _service = service; // scoped service
    
    public async Task ProcessAsync(IDataFlowContext context, IAsyncEnumerable<Data> input, CancellationToken cancellationToken)
    {
        await foreach (var item in input.WithCancellation(cancellationToken))
        {
            await _service.ProcessAsync(item);
        }
    }
}
```

## Error Handling and Propagation

### TPL DataFlow
- Independent error handling per block
- Errors don't automatically halt the pipeline
- Must explicitly observe Completion tasks
- Complex error propagation configuration needed

Example:
```csharp
var source = new TransformBlock<string, string>(item => {
    if (item == "error") throw new Exception("Test error");
    return item;
});

var target = new ActionBlock<string>(item => 
    Console.WriteLine($"Processing {item}"));

// Errors don't halt pipeline by default
source.LinkTo(target);
await source.SendAsync("item1");
await source.SendAsync("error");  // Throws but pipeline continues
await source.SendAsync("item2");  // Still processes

// Must explicitly check for errors
try {
    await source.Completion;
} catch (Exception ex) {
    // Handle error here
}
```

### Our DataFlow
- Pipeline-wide error handling
- Errors halt the entire pipeline by default
- Simpler error handling model
- Strong consistency guarantees

Example:
```csharp
builder
    .AddProducer<string>("source", 
        new ErrorProducer<string>(items, 
            shouldError: item => item == "error"))
    .AddProcessor<string>("target", processor)
    .ReceiveFrom("source");

// Error in any block halts the pipeline
await Assert.ThrowsAsync<InvalidOperationException>(() => 
    pipeline.ExecuteAsync());
```

## API Design and Usability

### TPL DataFlow
- Complex API with many options
- Manual block construction and linking
- Limited fluent API support
- Verbose configuration required

Example:
```csharp
var options = new ExecutionDataflowBlockOptions 
{
    MaxDegreeOfParallelism = 4,
    BoundedCapacity = 100
};

var transform = new TransformBlock<Data, Result>(
    async data => await ProcessAsync(data), 
    options);

var action = new ActionBlock<Result>(
    async result => await SaveAsync(result),
    options);

var linkOptions = new DataflowLinkOptions 
{
    PropagateCompletion = true
};

transform.LinkTo(action, linkOptions);
```

### Our DataFlow
- Fluent, expressive API
- Configuration through options pattern
- Built-in common block types
- Simpler pipeline construction

Example:
```csharp
builder
    .AddProducer<Data>("source", producer)
    .AddTransform<Data, Result>("transform", transformer)
    .ReceiveFrom("source")
    .AddProcessor<Result>("save", processor)
    .ReceiveFrom("transform")
    .WithBlock(b => {
        b.MaxConcurrency = 4;
        b.UseSeperateScopes = true;
    });
```

## Channel Integration

### TPL DataFlow
- Custom buffering mechanism
- Complex buffer management
- Manual configuration required for backpressure

### Our DataFlow
- Built on System.Threading.Channels
- Modern, efficient channel-based communication
- Automatic backpressure handling
- Better performance characteristics for modern hardware

## Performance Considerations

### TPL DataFlow
- Can be performant but requires careful tuning
- Complex buffering can impact memory usage
- Push model can lead to contention

### Our DataFlow
- Optimized for modern hardware
- Efficient channel-based communication
- Natural backpressure reduces contention
- Better memory characteristics due to pull model

## When to Choose Which

### Choose TPL DataFlow when:
- You prefer a Push based, or synchronous model
    - Immediate feedback on message acceptance is required
- You're working with existing TPL DataFlow code

### Choose Our DataFlow when:
- You want a simpler, more opinionated API that is fully asychronous.
