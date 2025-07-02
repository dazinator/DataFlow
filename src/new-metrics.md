Flow Rate vs Data Volume
Flow Rate (Block-Level Metrics)

What: How fast a block processes stream operations
TransformBlock: Transform rate (operations/sec)
BatchBlock: Batch rate (operations/sec)
ProducerBlock: Production rate (operations/sec)
ProcessorBlock: Processing rate (operations/sec)

Data Volume (Business Metrics)

What: Actual business items flowing through
Invoices processed: Business entities/sec
Records validated: Data quality/sec
Dollars processed: Financial volume/sec


        

        _blockOperationsCompleted = meter.CreateCounter<long>(InstrumentNames.BlockOperationsCompleted,
           unit: "operation",
           description: "Number of operations a block is completing");


        /// <summary>
        /// Number of stream items processed by a block
        /// </summary>
        [Description("Number of individual operations completed by a block (e.g batches, transforms etc)")]
        public const string BlockOperationsCompleted = "dataflow.block.operations";
