## Tests

1. Tests should re-use the existing Processors / Producers / Transformers where possible.
  - Processor
    - `TestProcessor` can be used to process items with a configurable delay, and callback for each item - as well as a func callback used to work out whether to throw an error whilst processing an item.
  - Producer
    - `ConcurrencyTestProducer` - tracks concurrency whilst yielding from a supplied IEnumberable, with configurable delay.
    - `TestProducer` - yields from a supplied IEnumerable, with configurable delay, and invocking a callback per item.
    - `ErrorProducer` - same as `TestProducer` but invokes a predicate to work out whether to throw an error for an item.
  - Transformer
    - `TestProjector` - used to project one item to many others. Takes a func which is called per input item to get an enumerable of output items which it yields.
       - utilises the concurrency tracker for processing the stream.
       - Calls a callback for each item in the input stream.
       - Calls another callback to get the output items for each input item.
       - Calls another callback for each output item yielded.
       - Takes an optional delay between yielding output items.
    - `PassthroughTransformer` - just passes through its input as output.
    - `NumberTransformer` - takes a number as input and yields it as a string with an optional prefix.


    ## Categories
    - Use `[IntegrationTest]` attribute or [UnitTest] appropriately. For Performance tests, use `[Category("Performance")]` attribute.


    ## Setup
    Tests should create a ServiceCollection in the constructor, and add the required services to it in the setup phase.

       ```
       
        private readonly ServiceProvider _serviceProvider;

        public RoutingBlockTests(ITestOutputHelper output)
        {
            Output = output;
            Services = new ServiceCollection();
            AddDefaultServices(Services);

        }

        public void AddDefaultServices(IServiceCollection services)
        {
            Services.AddLogging(builder => builder.AddXUnit(Output));
            
            // add relevent services
            services.AddDataFlows();

            Services.AddMetrics();
            Services.AddDataFlowMetrics();
            Services.AddMemoryCache();
        }

        public ITestOutputHelper Output { get; }
        public ServiceCollection Services { get; }
       ```

    This means the individual tests can then make any last ammendments to the Services collection before building the ServiceProvider should they need to.

