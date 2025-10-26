namespace Benchmarks.Shared;

using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Uniun.DataFlow;
using Uniun.DataFlow.Blocks;
using Uniun.DataFlow.Builder;
using Uniun.DataFlow.Builder.Graph;

/// <summary>
/// Simplified non-POC ETL benchmark focusing on datasource → validators → enrichers.
/// This removes routing, broadcasting, and batching to isolate the core concurrency scaling issue.
/// </summary>
public static class SimpleEtlDataFlow
{
    /// <summary>
    /// Builds a simplified ETL dataflow: DataSource → Validators → Enrichers → Collector
    /// Uses MaxConcurrency for internal block concurrency.
    /// </summary>
    public static StructuredDataFlowBuilder BuildDataFlow(
        IServiceProvider serviceProvider,
        int recordCount,
        int maxConcurrency = 4)
    {
        var builder = new StructuredDataFlowBuilder(serviceProvider, "SimpleEtlBenchmark");

        // Source: Generate raw data records
        builder.AddProducer<RawRecord>("data-source",
            (IServiceProvider sp) => new SimpleDataSourceProducer(recordCount));

        // Transform: Parse and validate records
        builder.AddTransform<RawRecord, ValidatedRecord>("validator", sp =>
            ActivatorUtilities.CreateInstance<SimpleValidationTransformer>(sp),
            new BlockOptions { MaxConcurrency = maxConcurrency, Capacity = 100 })
            .ReceiveFrom("data-source");

        // Transform: Enrich with additional data
        builder.AddTransform<ValidatedRecord, EnrichedRecord>("enricher", sp =>
            ActivatorUtilities.CreateInstance<SimpleEnrichmentTransformer>(sp),
            new BlockOptions { MaxConcurrency = maxConcurrency, Capacity = 100 })
            .ReceiveFrom("validator");

        // Terminal: Collect all enriched records
        Uniun.DataFlow.Builder.Graph.StructuredDataFlowBuilderExtensions.AddProcessor<EnrichedRecord>(
            builder, "collector", (IServiceProvider sp) =>
                ActivatorUtilities.CreateInstance<SimpleCollectorProcessor>(sp),
            new BlockOptions { MaxConcurrency = 1, Capacity = 100 })
            .ReceiveFrom("enricher");

        return builder;
    }

    // Data models
    public record RawRecord(int Id, string Data, DateTime Timestamp);
    public record ValidatedRecord(int Id, string Data, DateTime Timestamp, bool IsValid);
    public record EnrichedRecord(int Id, string Data, DateTime Timestamp, bool IsValid, string Category, decimal Value);

    // Producer
    public class SimpleDataSourceProducer : IStreamProducer<RawRecord>
    {
        private readonly int _recordCount;

        public SimpleDataSourceProducer(int recordCount)
        {
            _recordCount = recordCount;
        }

        public async IAsyncEnumerable<RawRecord> ProduceAsync(
            IDataFlowContext context,
            [EnumeratorCancellation] CancellationToken cancellation)
        {
            for (var i = 0; i < _recordCount; i++)
            {
                cancellation.ThrowIfCancellationRequested();

                yield return new RawRecord(
                    i,
                    $"Data_{i}_{Guid.NewGuid():N}",
                    DateTime.UtcNow
                );

                // Simulate some source latency
                if (i % 100 == 0)
                {
                    await Task.Delay(1, cancellation);
                }
            }
        }
    }

    // Transformers
    public class SimpleValidationTransformer : IStreamTransformer<RawRecord, ValidatedRecord>
    {
        public async IAsyncEnumerable<ValidatedRecord> TransformAsync(
            IDataFlowContext context,
            IAsyncEnumerable<RawRecord> input,
            [EnumeratorCancellation] CancellationToken cancellation)
        {
            await foreach (var record in input.WithCancellation(cancellation))
            {
                // Simulate validation logic
                var isValid = !string.IsNullOrEmpty(record.Data) && record.Id >= 0;

                yield return new ValidatedRecord(
                    record.Id,
                    record.Data,
                    record.Timestamp,
                    isValid
                );
            }
        }
    }

    public class SimpleEnrichmentTransformer : IStreamTransformer<ValidatedRecord, EnrichedRecord>
    {
        public async IAsyncEnumerable<EnrichedRecord> TransformAsync(
            IDataFlowContext context,
            IAsyncEnumerable<ValidatedRecord> input,
            [EnumeratorCancellation] CancellationToken cancellation)
        {
            await foreach (var record in input.WithCancellation(cancellation))
            {
                // Simulate enrichment with external data lookup (1ms delay)
                await Task.Delay(1, cancellation);

                var category = (record.Id % 3) switch
                {
                    0 => "TypeA",
                    1 => "TypeB",
                    _ => "TypeC"
                };

                var value = (decimal)(record.Id % 1000) / 10m;

                yield return new EnrichedRecord(
                    record.Id,
                    record.Data,
                    record.Timestamp,
                    record.IsValid,
                    category,
                    value
                );
            }
        }
    }

    // Processor
    public class SimpleCollectorProcessor : IStreamProcessor<EnrichedRecord>
    {
        public async Task ProcessAsync(
            IDataFlowContext context,
            IAsyncEnumerable<EnrichedRecord> input,
            CancellationToken cancellation)
        {
            await foreach (var record in input.WithCancellation(cancellation))
            {
                // Just consume the records
            }
        }
    }
}
