// ============================================================
// GraphRegistrationPrototype.cs
// Prototype: DI Registration example for Approach C migration
//
// This is REFERENCE CODE for the implementation team.
// ============================================================

using Microsoft.Extensions.DependencyInjection;

namespace JournalProcessing.Prototype;

/// <summary>
/// Example of how to register the journal processing flow with the POC DataFlow library.
///
/// This replaces the legacy flow with AddPersistentRouter. The key changes:
/// 1. No per-system blocks — a single ErpPostingActor handles all systems.
/// 2. INamedErpSystemHandler implementations registered for each supported ERP type.
/// 3. UnroutedJournalHandler registered for fallback.
/// 4. Graph topology is simpler: linear with no routing fan-out.
/// </summary>
public static class JournalProcessingRegistrationExample
{
    public static IServiceCollection AddJournalProcessingFlow(
        this IServiceCollection services)
    {
        // Register ERP handlers — one per supported ERP INTEGRATION TYPE
        // (NOT one per tenant — tenants use IOptionsSnapshot for per-tenant config)
        services.AddScoped<INamedErpSystemHandler, SapBtpErpHandler>();
        // When Oracle support is added:
        // services.AddScoped<INamedErpSystemHandler, OracleErpHandler>();

        // Register fallback handler
        services.AddScoped<UnroutedJournalHandler>();

        // Register the dispatcher actor
        services.AddScoped<ErpPostingActor>();

        // Register the dataflow graph
        // Note: The exact API depends on the POC version being used.
        // This is a conceptual sketch — adapt to actual DI registration API.
        services.AddDataFlows("journal-processing", df =>
        {
            // Source
            df.AddSourceBlock<Journal, JournalProcessingProducer>("producer");

            // Batch
            df.AddBatchBlock<Journal>("journal-batcher",
                maxBatchSize: 200,
                windowPeriod: TimeSpan.FromSeconds(2));

            // Routing transformer — emits one SystemJournalProcessingItems per distinct system
            df.AddActorBlock<Journal[], SystemJournalProcessingItems,
                JournalRoutingTransformer>("routing-transformer");

            // ERP posting — single block handles all system types
            // MaxConcurrency=3 reproduces the legacy SAP sender concurrency
            df.AddActorBlock<SystemJournalProcessingItems, JournalPostingResult,
                ErpPostingActor>("erp-poster", maxConcurrency: 3);

            // TMS status update path (shared by all ERP types including unrouted)
            df.AddBatchBlock<JournalPostingResult>("tms-batcher",
                maxBatchSize: 200,
                windowPeriod: TimeSpan.FromSeconds(2));
            df.AddActorBlock<JournalPostingResult[], Unit,
                TmsJournalStatusSender>("tms-sender", maxConcurrency: 3);

            // Graph topology — linear pipeline, no routing fan-out
            df.AddGraph("journal-flow", g =>
            {
                g.UseBlock("producer")
                 .BatchWith("journal-batcher")
                 .ProcessWith("routing-transformer")
                 .ProcessWith("erp-poster")         // <-- replaces PersistentRouter + N sub-flows
                 .BatchWith("tms-batcher")
                 .ProcessWith("tms-sender");
            });
        });

        return services;
    }

    // ============================================================
    // Variant: If per-ERP-type concurrency isolation is needed
    // ============================================================

    /// <summary>
    /// Variant registration where different ERP types need different concurrency settings.
    /// Uses a thin static routing layer on ERP TYPE (not tenant instance).
    /// This is an A/C hybrid — Approach A routing by type, Approach C handler within each route.
    /// </summary>
    public static IServiceCollection AddJournalProcessingFlowWithTypeRouting(
        this IServiceCollection services)
    {
        services.AddScoped<INamedErpSystemHandler, SapBtpErpHandler>();
        services.AddScoped<UnroutedJournalHandler>();

        // Separate dispatcher instances for each ERP type route
        services.AddScoped<SapErpDispatcher>();
        services.AddScoped<UnroutedDispatcher>();

        services.AddDataFlows("journal-processing-typed", df =>
        {
            df.AddSourceBlock<Journal, JournalProcessingProducer>("producer");
            df.AddBatchBlock<Journal>("journal-batcher", 200, TimeSpan.FromSeconds(2));
            df.AddActorBlock<Journal[], SystemJournalProcessingItems,
                JournalRoutingTransformer>("routing-transformer");

            // One block per ERP TYPE with appropriate concurrency
            df.AddActorBlock<SystemJournalProcessingItems, JournalPostingResult,
                SapErpDispatcher>("sap-poster", maxConcurrency: 3);
            df.AddActorBlock<SystemJournalProcessingItems, JournalPostingResult,
                UnroutedDispatcher>("unrouted-poster", maxConcurrency: 1);

            // Shared TMS path (merge all routes via buffer or merge block)
            df.AddBatchBlock<JournalPostingResult>("tms-batcher", 200, TimeSpan.FromSeconds(2));
            df.AddActorBlock<JournalPostingResult[], Unit,
                TmsJournalStatusSender>("tms-sender", maxConcurrency: 3);

            df.AddGraph("journal-flow-typed", g =>
            {
                // Route by ERP TYPE (static, small set)
                g.UseBlock("routing-transformer")
                 .RouteBy(item => item.System?.Name is "SAP" or "SapS4Hana" ? "sap" : "unrouted")
                 .To("sap-poster",      key => key == "sap")
                 .To("unrouted-poster", key => key == "unrouted");

                // Merge both routes into shared TMS path
                // (using a merge/buffer block — exact API depends on POC version)
                g.MergeFrom(new[] { "sap-poster", "unrouted-poster" })
                 .Into("tms-batcher")
                 .ProcessWith("tms-sender");
            });
        });

        return services;
    }
}

// ============================================================
// Placeholder types (for compilation reference only)
// ============================================================

public sealed class JournalProcessingProducer { }    // Implements ISourceActor<Journal>
public sealed class JournalRoutingTransformer { }    // Implements IActor<Journal[], SystemJournalProcessingItems>
public sealed class TmsJournalStatusSender { }       // Implements IActor<JournalPostingResult[], Unit>
public sealed class SapErpDispatcher { }             // Wraps SapBtpErpHandler
public sealed class UnroutedDispatcher { }           // Wraps UnroutedJournalHandler
public sealed record Unit;

// Placeholder DI extension method shapes — actual API in the POC
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDataFlows(this IServiceCollection services,
        string name, Action<IDataFlowBuilder> configure)
        => services; // Placeholder
}

public interface IDataFlowBuilder
{
    void AddSourceBlock<TOut, TActor>(string name);
    void AddBatchBlock<T>(string name, int maxBatchSize, TimeSpan windowPeriod);
    void AddActorBlock<TIn, TOut, TActor>(string name, int maxConcurrency = 1);
    void AddGraph(string name, Action<IGraphBuilder> configure);
}

public interface IGraphBuilder
{
    IGraphBuilder UseBlock(string name);
    IGraphBuilder BatchWith(string name);
    IGraphBuilder ProcessWith(string name);
    IGraphBuilder RouteBy(Func<SystemJournalProcessingItems, string> selector);
    IGraphBuilder To(string blockName, Func<string, bool> predicate);
    IGraphBuilder MergeFrom(string[] blockNames);
    IGraphBuilder Into(string blockName);
    IGraphBuilder CompeteWith(string[] blockNames);
}
