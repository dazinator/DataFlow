## Why not Microsoft TPL Dataflow?

![Data Flow Diagram](/docs/df.png)

Microsoft TPL Dataflow is a library that provides a way to build dataflow pipelines in .NET. It is a library that is part of the .NET framework and is available in the `System.Threading.Tasks.Dataflow` namespace.

Microsoft TPL Dataflow doesn't:-
- doesn't use the modern System.Threading.Channels which have been better optimised for higher performance on more hardware.
- doesn't have nay fluent API for building dataflows.
- wasn't built with DI in mind, so blocks have to be constructed and wired up manually, often involving a lot of tedious boilerplate code.
    - For example if you have concurrent Actions being executed in an Action block, and they need a scoped dependency, you have to manually manage dependency creation.
- to add cross-cutting concerns, you have to typically derive from the base classes anyway and implement custom block types.
- has some api's that aren't necessary for us such as
    - Synchronously `Post` data or Receive data from a block.
        - Synchronous API's are only relevent in a very specialised set of data flows like realtime stock / trading - where if you can't process an item immediately you want to fail immediately at the point of sending the item into the flow.
            - For most use cases we only ever need Async producer / cosumer pattern - as this pattern scales better, and accomodates backpressure without failing the overall flow which is best for resiliency and most business use cases.

## Data Flow Blocks

- Source Block: supply data for downstream blocks.
- Target Block: receive data from upstream blocks.
    - Target blocks pull data from the source, and where they allow for concurrency, they respect the max concurrency settings on the block options passed to them.
- Propagator Block: is a source block and a target block. It both receives data and supplies data for the next target block. Example: TransformBlock, ProjectorBlock.

As data flow is a pipeline, the blocks are connected together in a chain, with the output of one block being the input of the next block.
- All blocks are running concurrently
- The blocks are designed to be able to handle backpressure
    - Output data from a block sits in its output buffer - which is bounded by a max capacity. Once its full, the block will stop pulling new data until its output buffer has space.
    - Target blocks are actively pulling data from the upstream source blocks output buffer - freeing up capacity.
        - A processor block doesn't have an output buffer of its own its a terminal block in the flow.
        - A propogator block does have an output buffer it will be pulling data from the upstream source block's output buffer and writing to its own output buffer for the next target block to pull.
- The blocks can have concurrent actions running on the data.
    - The max concurrency setting on the block options is used to control how many concurrent actions maximum can be running on the data.
    - For example a Producer block can have multiple `IProducer<T>` implementations running concurrently to fill its output buffer.
    - A Transform block can have multiple `ITransformer<TIn, TOut>` implementations running concurrently - all pulling data from the upstream source block adapting it to an output stream which is written to the blocks output buffer.
    - A Projector block can have multiple `IProjector<TIn, TOut>` implementations running concurrently - all pulling data from the upstream source block and producing multiple TOut items for each TIn item, writing them to the blocks output buffer.
    - A Processor block can have multiple `IProcessor<T>` implementations running concurrently - all pulling data from the upstream source block and processing it.
    - This allows you to scale for example the number of concurrent producers, transformers, projectors or processors to tune throughput, with max concurrency settings to throttle things.

### Source Blocks

Source blocks are blocks that:-
- can be used to supply data to a dataflow.
- implement the `ISourceBlock<T>` interface.
- are a starting point for supplying data into a dataflow.
    - A data flow without data flowing into it, isn't much good.

### Target Blocks

Target blocks are blocks that:-
- pull data from a source block upstream in the flow that you link them to.
- implement ITargetBlock<T> interface.

### Propagator Blocks

Propagator blocks act as both source and target blocks.
They:-
- pull data from an upstream source block
- presumably do something with data
- output data to a buffer for a downstream target block to pull
- implement `IPropagatorBlock<TInput, TOutput>` interface which derives from both `ISourceBlock<TOutput>` and `ITargetBlock<TInput>` interfaces.

## Specific Block Implementations

| Block Name        |  Is Source         | Is Target | Description                                                                                                                                                                                                                                                                       |
|-------------------|------------------------------|-----------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| InputChannelBlock | Yes | No        | Allows you to directly supply input data into the flow, by acting as a source block over which you can supply data by writing to its ChannelWriter<T>. Data is then bufferred in the channel, for downstream target blocks to consume.                                            | 
| ProducerBlock     | Yes | No        | A source block that allows you to supply data via  `IProducer<T>` dependencies. You can supply an enumerable of these and they will be executed concurrently honouring max concurrency settings.                                                                                  |
| ProcessorBlock    | No | Yes       | Perform some processing on each item received. No data is propogated onwards.                                                                                                                                                                                                     |
| TransformBlock    | Yes | Yes       | Allows you to adapt the source / input stream to a new output stream utilising `ITransformer<TIn, TOut>` with concurrency options. |
| ProjectorBlock    | Yes | Yes       | Allows you to supply an enumerable of `IProjector<TIn, TOut>` which will then be used concurrently (upto max concurrency settings) to pull data from the upstream source, project each TIn into multiple TOut items which are written to the blocks output buffer for the next target block to pull. |
| BatchBlock        | Yes | Yes       | Collects incoming items into batches based on max batch size and/or time window criteria. Emits batches as arrays when either the max batch size is reached or the time window elapses. Useful for optimizing downstream processing efficiency. |
| OutputBlock       | No | Yes       | A terminal block that allows the application to receive the output of the data flow (each item) via a supplied `Func<T>`. Similar to a Processor Block but does not require implementing an `IProcessor`.    |


### Producer Block

Producer blocks:-
- are source blocks that supply data into the dataflow.
- activates (using DI scope) a specified number of concurrent `IProducer<T>` implementations supply items concurrently.
- Implements `ISourceBlock<T>` interface so that items can be propagated onwards to recipient blocks.

### Input Channel Block

- can be used to supply data into the flow via a ChannelWriter<T>
- Implements `ISourceBlock<T>` so that they can be connected to downstream target blocks like Processor, or Transform etc.

### Projector Block

Projector blocks:-
- are propagator blocks that allow projecting one input item into multiple output items.
- activates (using DI scope) a specified number of concurrent `IProjector<TIn, TOut>` implementations to project items concurrently.
- Uses IAsyncEnumerable for efficient streaming of output items.
- Implements `IPropagatorBlock<TIn, TOut>` interface so it can receive input and supply output items in the flow.

## Transformer Bloc

Transformer blocks:-
- are propagator blocks that allow adapting the input stream into an output stream which is written to the blocks output buffer.
- activates (using DI scope) a specified number of concurrent `ITransformer<TIn, TOut>` implementations to transform items concurrently.
- Important Notes:
  - To preserve order, you should supply a single `ITransformer<TIn, TOut>`. If you supply multiple, they are competing consumers, and their results will be interleaved in the output channel - potentially losing the original order. Therefore only add concurrent transformes where
     - You don't need to preserve order of the stream.
     - Your transformation logic is expensive and expected to not "keep pace" with the input stream - in other words you want to add concurrent transformers to alleviate backpressure.

### Processor Block

Processor blocks:-
- are terminal blocks in the data flow, used to process items that do not flow onwards.
- activates (using DI scope) a specified number of concurrent `IProcessor<T>` implementations to processed received items concurrently.
- Implements `IRecipientBlock<T>` interface so that items can be passed to it in the flow.

### Batch Block

Batch blocks:-
- are propagator blocks that group incoming items into batches based on specified criteria
- emit batches as arrays when either:
  - The maximum batch size is reached
  - The specified time window period has elapsed since the first item in the current batch 
  - The source completes (any remaining items are flushed as a final batch)
- process items serially to maintain order
- uses an efficient object pool for managing batch collections with zero locking.
- implements IPropagatorBlock<TIn, TIn[]> interface
- particularly useful for:
  - Optimizing downstream processing by reducing the number of operations
  - Controlling throughput and resource usage
  - Accumulating items for bulk operations like database writes

Key features:
- Configurable maximum batch size - controls how many items can accumulate before a batch is emitted
- Configurable window period - ensures batches are emitted even if max size isn't reached, important when a fixed batch size isn't guaranteed otherwise it would wait indefinitely for a complete batch.
- Automatic handling of partial batches when source completes
- Thread-safe batch accumulation and emission that doesn't use locking
- Memory-efficient batch collection management using object pooling
- Proper handling of cancellation and errors
- Ordered processing of items to maintain sequence

Example usage for batching database writes:
```csharp
builder.AddProducer<string>("source", sp => new ItemProducer())
    .AddBatch<string>("batcher", 
        maxBatchSize: 100,            // Emit batches when 100 items accumulated  
        windowPeriod: TimeSpan.FromSeconds(5))  // Or after 5 seconds
    .ReceiveFrom("source")
    .AddProcessor<string[], DatabaseBatchWriter>()
    .ReceiveFrom("batcher");

## Walkthrough - Building a Data Flow

1. Define a class that implements `IDataFlowConfiguration` interface and use this to define your data flow.

```csharp
  private class NumberProcessingFlowConfig : IDataFlowConfiguration
    {
        public void Configure(DataFlowBuilder builder)
        {
            // add blocks for standard ETL flow
            builder
                   .AddProducer<int, NumberProducer>("source")
                   .AddTransform<int, string, NumberToStringTransformer>("transform")
                    .ReceiveFrom("source") // connects the block
                   .AddProcessor<string, NumberProcessor>("processor")
                    .ReceiveFrom("transform"); // connects the block
        }
    }
```

In this example we
- Produce the source stream
- Transform the source stream to a new output stream
- Process the transformed item stream.


```csharp
public class NumberProducer : IProducer<int>
{
    private readonly List<string> _testLog;

    // supports dependency injection
    public NumberProducer(List<string> testLog)
    {
        _testLog = testLog;
    }   

    public async IAsyncEnumerable<int> ProduceAsync(
        [EnumeratorCancellation] CancellationToken cancellation)
    {

        for (int i = 0; i < 10; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            _testLog.Add($"Producing Number: {i}");
            yield return i;
            // Small delay to make tests more realistic
            await Task.Delay(10, cancellation);
        }
    }
}

public class NumberToStringTransformer : ITransformer<int, string>
{
    private readonly List<string> _testLog;

    public async IAsyncEnumerable<string> TransformAsync(
            IAsyncEnumerable<int> input,
            PipelineContext context)
        {           
            // could do any one time setup here before enumerating the source stream
            // await LoadSettingsAsync();

            await foreach (var item in input)
            {               
                yield return item.ToString();
            }
        }   
}

public class NumberProcessor : IProcessor<string>
{
    private readonly List<string> _testLog;

     // supports dependency injection
    public NumberProcessor(List<string> testLog)
    {
        _testLog = testLog;
    }

    public async Task ProcessAsync(IAsyncEnumerable<string> input, CancellationToken cancellationToken)
    {
        await foreach (var item in input)
        {
           _testLog.Add($"Processed Number: {item}");            
        }
    }   
}

```

2. Add the data flow to the services collection in startup.

```csharp

        // Arrange
        Services.AddDataFlow<NumberProcessingFlowConfig>();      

```

3. Run the data flow.

```csharp

        using var sp = GetServiceProvider();
        var executor = sp.GetRequiredService<FlowExecutor<NumberProcessingFlowConfig>>();
        var context = new PipelineContext("test", Guid.NewGuid(), sp);

        // Act
        await executor.ExecuteAsync(context);     

```

The `await executor.ExecuteAsync(context);` will run the data flow, and the blocks will be executed concurrently. The blocks will complete as they finish processing data.
When all the blocks are finished, the `await` will complete and the data flow will have finished.

### Use Case 1: Supplying data from outside the flow

- Use `AddInputChannelBlock` then get that blocks `ChannelWriter<T>` and write to it to supply data to the flow.
  - Important: don't forget to call .Complete() on the `ChannelWriter<T>` when you are done supplying data to the flow - so the downstream blocks know that the channel is complete and not to expect any more data. They will then be able to complete.

### Use Case 2: Transforming data in the flow
Connect any Source Block (such as a `ProducerBlock`) to a Transform Block (such as a `TranformBlock`) then connect that to a target block to continue the flow with the transformed data.

1. The ProducerBlock will naturally buffer the produced data.
2. The transform block will allow us to adapt the source input stream from the source block output to a new output stream, using a concurrent set of `ITransformer`s.
3. The connected downstream target block will be consuming the transformed data from the transform block output buffer.


### Use case 3 - Simple actions on data

Use a single block flow - it just has a `ProcessorBlock` to receive data and act upon it.
This block buffers the received data, and then acts on it with the specified max concurrency of `IProcessor<T>` implementations.
If you just want a simple `Channel` for a single producer / consumer scenario this is the simplest option.
