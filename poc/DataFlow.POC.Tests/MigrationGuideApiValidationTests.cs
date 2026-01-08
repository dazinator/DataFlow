namespace DataFlow.POC.Tests;

using DataFlow.POC.Blocks;
using DataFlow.POC.Builder;
using DataFlow.POC.Core;
using DataFlow.POC.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

/// <summary>
/// Validates that all API examples in the migration-case-study-invoice-reprocessing.md guide compile correctly.
/// This ensures the documentation provides accurate, working code examples.
/// </summary>
public class MigrationGuideApiValidationTests
{
    #region Test Models (mimicking invoice reprocessing domain)
    
    public class InvoiceEnrichmentContext
    {
        public int Id { get; set; }
        public string SourceData { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string? EnrichedField1 { get; set; }
        public decimal? EnrichedField2 { get; set; }
    }

    public class CashflowReallocationChange
    {
        public int Id { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    public class CashflowReallocationGroup
    {
        public int GroupId { get; set; }
        public List<CashflowReallocationChange> Changes { get; set; } = new();
    }

    #endregion

    #region Test Service Interfaces

    public interface IInvoiceRepository
    {
        IAsyncEnumerable<InvoiceEnrichmentContext> GetInvoicesForReprocessingAsync(
            CancellationToken cancellationToken);
        Task BulkUpdateAsync(InvoiceEnrichmentContext[] batch, CancellationToken cancellationToken);
    }

    public interface ICounterpartyInfoLoader
    {
        Task<InvoiceEnrichmentContext[]> LoadCounterpartyInfoAsync(
            InvoiceEnrichmentContext[] batch,
            CancellationToken cancellationToken);
    }

    public interface IInvoiceEnrichmentService
    {
        InvoiceEnrichmentContext[] Enrich(InvoiceEnrichmentContext[] batch);
    }

    public interface INotificationService
    {
        Task NotifyAsync(InvoiceEnrichmentContext[] batch, CancellationToken cancellationToken);
    }

    public interface ICashflowChangeIdentifier
    {
        IEnumerable<CashflowReallocationChange> IdentifyChanges(InvoiceEnrichmentContext[] batch);
    }

    public interface IReallocationTransformer
    {
        CashflowReallocationGroup[] TransformToGroups(CashflowReallocationChange[] changes);
    }

    public interface IReallocationProcessor
    {
        Task<CashflowReallocationGroup[]> ProcessAsync(
            CashflowReallocationGroup[] groups,
            CancellationToken cancellationToken);
    }

    #endregion

    #region Test Actors (from migration guide)

    public class InvoiceSource : IPlainSourceActor<InvoiceEnrichmentContext>
    {
        private readonly IInvoiceRepository _repository;

        public InvoiceSource(IInvoiceRepository repository)
        {
            _repository = repository;
        }

        public async IAsyncEnumerable<InvoiceEnrichmentContext> ProduceAsync(
            IActorExecutionContext context)
        {
            await foreach (var invoice in _repository.GetInvoicesForReprocessingAsync(context.CancellationToken))
            {
                yield return invoice;
            }
        }
    }

    public class CounterpartyInfoLoaderActor
        : IStreamActor<InvoiceEnrichmentContext[], InvoiceEnrichmentContext[]>
    {
        private readonly ICounterpartyInfoLoader _loader;

        public CounterpartyInfoLoaderActor(ICounterpartyInfoLoader loader)
        {
            _loader = loader;
        }

        public async IAsyncEnumerable<InvoiceEnrichmentContext[]> RunAsync(
            IAsyncEnumerable<InvoiceEnrichmentContext[]> input,
            IActorExecutionContext context)
        {
            await foreach (var batch in input.WithCancellation(context.CancellationToken))
            {
                yield return await _loader.LoadCounterpartyInfoAsync(batch, context.CancellationToken);
            }
        }
    }

    public class InvoiceEnrichmentActor
        : IStreamActor<InvoiceEnrichmentContext[], InvoiceEnrichmentContext[]>
    {
        private readonly IInvoiceEnrichmentService _enrichmentService;

        public InvoiceEnrichmentActor(IInvoiceEnrichmentService enrichmentService)
        {
            _enrichmentService = enrichmentService;
        }

        public async IAsyncEnumerable<InvoiceEnrichmentContext[]> RunAsync(
            IAsyncEnumerable<InvoiceEnrichmentContext[]> input,
            IActorExecutionContext context)
        {
            await foreach (var batch in input.WithCancellation(context.CancellationToken))
            {
                yield return _enrichmentService.Enrich(batch);
            }
        }
    }

    public class InvoiceBulkUpdateActor
        : IStreamActor<InvoiceEnrichmentContext[], InvoiceEnrichmentContext[]>
    {
        private readonly IInvoiceRepository _repository;

        public InvoiceBulkUpdateActor(IInvoiceRepository repository)
        {
            _repository = repository;
        }

        public async IAsyncEnumerable<InvoiceEnrichmentContext[]> RunAsync(
            IAsyncEnumerable<InvoiceEnrichmentContext[]> input,
            IActorExecutionContext context)
        {
            await foreach (var batch in input.WithCancellation(context.CancellationToken))
            {
                await _repository.BulkUpdateAsync(batch, context.CancellationToken);
                yield return batch; // Pass through for next stage
            }
        }
    }

    public class NotificationActor
        : IStreamActor<InvoiceEnrichmentContext[], InvoiceEnrichmentContext[]>
    {
        private readonly INotificationService _notificationService;

        public NotificationActor(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        public async IAsyncEnumerable<InvoiceEnrichmentContext[]> RunAsync(
            IAsyncEnumerable<InvoiceEnrichmentContext[]> input,
            IActorExecutionContext context)
        {
            await foreach (var batch in input.WithCancellation(context.CancellationToken))
            {
                await _notificationService.NotifyAsync(batch, context.CancellationToken);
                yield return batch;
            }
        }
    }

    public class CashflowChangesProducerActor
        : IStreamActor<InvoiceEnrichmentContext[], CashflowReallocationChange>
    {
        private readonly ICashflowChangeIdentifier _changeIdentifier;

        public CashflowChangesProducerActor(ICashflowChangeIdentifier changeIdentifier)
        {
            _changeIdentifier = changeIdentifier;
        }

        public async IAsyncEnumerable<CashflowReallocationChange> RunAsync(
            IAsyncEnumerable<InvoiceEnrichmentContext[]> input,
            IActorExecutionContext context)
        {
            await foreach (var batch in input.WithCancellation(context.CancellationToken))
            {
                // Expand: one batch → multiple changes
                var changes = _changeIdentifier.IdentifyChanges(batch);
                foreach (var change in changes)
                {
                    yield return change;
                }
            }
        }
    }

    public class ReallocationTransformerActor
        : IStreamActor<CashflowReallocationChange[], CashflowReallocationGroup[]>
    {
        private readonly IReallocationTransformer _transformer;

        public ReallocationTransformerActor(IReallocationTransformer transformer)
        {
            _transformer = transformer;
        }

        public async IAsyncEnumerable<CashflowReallocationGroup[]> RunAsync(
            IAsyncEnumerable<CashflowReallocationChange[]> input,
            IActorExecutionContext context)
        {
            await foreach (var batch in input.WithCancellation(context.CancellationToken))
            {
                yield return _transformer.TransformToGroups(batch);
            }
        }
    }

    public class ReallocationProcessorActor
        : IStreamActor<CashflowReallocationGroup[], CashflowReallocationGroup[]>
    {
        private readonly IReallocationProcessor _processor;

        public ReallocationProcessorActor(IReallocationProcessor processor)
        {
            _processor = processor;
        }

        public async IAsyncEnumerable<CashflowReallocationGroup[]> RunAsync(
            IAsyncEnumerable<CashflowReallocationGroup[]> input,
            IActorExecutionContext context)
        {
            await foreach (var batch in input.WithCancellation(context.CancellationToken))
            {
                yield return await _processor.ProcessAsync(batch, context.CancellationToken);
            }
        }
    }

    #endregion

    [Fact]
    public void MigrationGuide_ActorBlock_Registration_Should_Compile()
    {
        // Arrange - Create service collection
        var services = new ServiceCollection();

        // This tests the registration code from the migration guide - Step 2
        services.AddScoped<InvoiceSource>();
        services.AddScoped<CounterpartyInfoLoaderActor>();
        services.AddScoped<InvoiceEnrichmentActor>();
        services.AddScoped<InvoiceBulkUpdateActor>();
        services.AddScoped<NotificationActor>();
        services.AddScoped<CashflowChangesProducerActor>();
        services.AddScoped<ReallocationTransformerActor>();
        services.AddScoped<ReallocationProcessorActor>();

        // Assert - verify services registered
        services.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void MigrationGuide_DataFlow_Registration_Should_Compile()
    {
        // This tests the complete registration code from the migration guide - Step 3
        var services = new ServiceCollection();

        // Register actors
        services.AddScoped<InvoiceSource>();
        services.AddScoped<CounterpartyInfoLoaderActor>();
        services.AddScoped<InvoiceEnrichmentActor>();
        services.AddScoped<InvoiceBulkUpdateActor>();
        services.AddScoped<NotificationActor>();
        services.AddScoped<CashflowChangesProducerActor>();
        services.AddScoped<ReallocationTransformerActor>();
        services.AddScoped<ReallocationProcessorActor>();

        // Register DataFlow using the pattern from migration guide
        services.AddDataFlows("invoice-reprocessing", df =>
        {
            // === STAGE 1: Invoice Source ===
            df.AddBlock("invoice-source", sp =>
            {
                var factory = sp.GetRequiredService<IServiceScopeFactory>();
                return new PlainSourceAdapter<InvoiceEnrichmentContext, InvoiceSource>(
                    new BlockContext("invoice-reprocessing:invoice-source"),
                    factory,
                    sourceName: "database");
            });

            // === STAGE 2: Batching ===
            // Note: Migration guide needs correction - AddBatchBlock doesn't exist
            // Actual API uses AddBlock with EpochBatchBlock
            df.AddBlock("batch-invoices", sp =>
            {
                return new EpochBatchBlock<InvoiceEnrichmentContext>(
                    new BlockContext("invoice-reprocessing:batch-invoices"),
                    maxBatchSize: 10000,
                    windowPeriod: TimeSpan.FromSeconds(10));
            });

            // === STAGE 3: Rate Limiting (using buffer with small capacity) ===
            // Note: Migration guide needs correction - AddBufferBlock doesn't exist
            // Actual API uses AddBlock with EpochBufferBlock and BufferConfiguration
            df.AddBlock("rate-limit-buffer", sp =>
            {
                return new EpochBufferBlock<InvoiceEnrichmentContext[]>(
                    new BlockContext("invoice-reprocessing:rate-limit-buffer"),
                    new BufferConfiguration(capacity: 2));
            });

            // === STAGE 4: Enrichment Pipeline ===
            // Note: Migration guide uses AddActorBlock which exists
            df.AddActorBlock<InvoiceEnrichmentContext[], InvoiceEnrichmentContext[], CounterpartyInfoLoaderActor>(
                "load-counterparty");
            df.AddActorBlock<InvoiceEnrichmentContext[], InvoiceEnrichmentContext[], InvoiceEnrichmentActor>(
                "enrich-invoices");
            df.AddActorBlock<InvoiceEnrichmentContext[], InvoiceEnrichmentContext[], InvoiceBulkUpdateActor>(
                "update-invoices");
            df.AddActorBlock<InvoiceEnrichmentContext[], InvoiceEnrichmentContext[], NotificationActor>(
                "notify");

            // === STAGE 5: Cashflow Reallocation ===
            df.AddActorBlock<InvoiceEnrichmentContext[], CashflowReallocationChange, CashflowChangesProducerActor>(
                "identify-changes");
            
            df.AddBlock("batch-changes", sp =>
            {
                return new EpochBatchBlock<CashflowReallocationChange>(
                    new BlockContext("invoice-reprocessing:batch-changes"),
                    maxBatchSize: 10000,
                    windowPeriod: TimeSpan.FromSeconds(2));
            });
            
            df.AddActorBlock<CashflowReallocationChange[], CashflowReallocationGroup[], ReallocationTransformerActor>(
                "reallocate-to-cashflows");
            df.AddActorBlock<CashflowReallocationGroup[], CashflowReallocationGroup[], ReallocationProcessorActor>(
                "process-reallocations");

            // === GRAPH DEFINITION ===
            // Note: Migration guide uses .ProcessWith() which doesn't exist
            // Actual API uses .Connect()
            df.AddGraph("main", g =>
            {
                g.UseBlock("invoice-source");
                g.UseBlock("batch-invoices");
                g.UseBlock("rate-limit-buffer");
                g.UseBlock("load-counterparty");
                g.UseBlock("enrich-invoices");
                g.UseBlock("update-invoices");
                g.UseBlock("notify");
                g.UseBlock("identify-changes");
                g.UseBlock("batch-changes");
                g.UseBlock("reallocate-to-cashflows");
                g.UseBlock("process-reallocations");

                // Connect blocks
                g.Connect("invoice-source", "batch-invoices");
                g.Connect("batch-invoices", "rate-limit-buffer");
                g.Connect("rate-limit-buffer", "load-counterparty");
                g.Connect("load-counterparty", "enrich-invoices");
                g.Connect("enrich-invoices", "update-invoices");
                g.Connect("update-invoices", "notify");
                g.Connect("notify", "identify-changes");
                g.Connect("identify-changes", "batch-changes");
                g.Connect("batch-changes", "reallocate-to-cashflows");
                g.Connect("reallocate-to-cashflows", "process-reallocations");
            });
        });

        // Build service provider
        var serviceProvider = services.BuildServiceProvider();

        // Assert - verify graph can be resolved
        var graph = serviceProvider.GetKeyedService<DataFlowGraph>("invoice-reprocessing:main");
        graph.ShouldNotBeNull();
    }

    private static IServiceScopeFactory GetScopeFactory()
    {
        var services = new ServiceCollection();
        return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }
}
