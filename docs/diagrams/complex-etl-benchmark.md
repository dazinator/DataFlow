# Complex ETL Benchmark DataFlow

This diagram represents the sophisticated ETL pipeline used for benchmarking the DataFlow library.
It demonstrates real-world scenarios including:

- Data ingestion and validation
- Enrichment with external data lookups
- Category-based routing (TypeA, TypeB, TypeC)
- Multiple processing paths:
  - **TypeA**: Individual record processing
  - **TypeB**: Batch aggregation
  - **TypeC**: Category-based storage

## Pipeline Diagram

```mermaid
flowchart LR
    %% DataFlow: ComplexEtlBenchmark

    data_source(["data-source<br/>→ RawRecord"])
    validator[/"validator<br/>RawRecord → ValidatedRecord"/]
    enricher[/"enricher<br/>ValidatedRecord → EnrichedRecord"/]
    broadcast[/"broadcast<br/>EnrichedRecord → EnrichedRecord"/]
    metrics_collector["metrics-collector<br/>EnrichedRecord →"]
    audit_logger["audit-logger<br/>EnrichedRecord →"]
    router["router<br/>EnrichedRecord →"]

    data_source -->|RawRecord| validator
    validator -->|ValidatedRecord| enricher
    enricher -->|EnrichedRecord| broadcast
    broadcast -->|EnrichedRecord| metrics_collector
    broadcast -->|EnrichedRecord| audit_logger
    broadcast -->|EnrichedRecord| router

    %% 'broadcast' broadcasts to multiple targets

    %% Routes for 'router':
    subgraph router_route_TypeA ["Route: TypeA"]
        direction LR
        %% Route 'TypeA' blocks not shown
        %% (Pass IServiceProvider to ToMermaidDiagram to render route details)
    end

    router -.->|EnrichedRecord<br/>'TypeA'| router_route_TypeA
    subgraph router_route_TypeB ["Route: TypeB"]
        direction LR
        %% Route 'TypeB' blocks not shown
        %% (Pass IServiceProvider to ToMermaidDiagram to render route details)
    end

    router -.->|EnrichedRecord<br/>'TypeB'| router_route_TypeB
    subgraph router_route_TypeC ["Route: TypeC"]
        direction LR
        %% Route 'TypeC' blocks not shown
        %% (Pass IServiceProvider to ToMermaidDiagram to render route details)
    end

    router -.->|EnrichedRecord<br/>'TypeC'| router_route_TypeC

```

## Dataflow Configuration

- **Record Count**: Configurable (default: 10,000)
- **Max Concurrency**: 4 workers per block
- **Batch Size**: 100 records
- **Routing**: 3 categories evenly distributed

## Usage

This dataflow is used in:
- `ComplexEtlBenchmark` - BenchmarkDotNet performance benchmarks
- Performance profiling with dotnet-trace and dotnet-counters
- OpenTelemetry metrics collection

See `src/Benchmarks/Shared/ComplexEtlDataFlow.cs` for the complete implementation.
