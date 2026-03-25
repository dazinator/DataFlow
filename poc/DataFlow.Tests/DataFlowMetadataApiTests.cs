namespace DataFlow.POC.Tests;

using DataFlow.Blazor.BlockTypes;
using DataFlow.Blazor.FlowMetadata;
using DataFlow.POC.Blocks;
using DataFlow.POC.DependencyInjection;
using DataFlow.POC.Core;
using DataFlow.POC.Tests.TestHelpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// Tests for the block and flow metadata APIs, including:
/// - Inline metadata registration via <c>AddDataFlows</c> configure callbacks
/// - <c>AddEpochBuffer</c> on the DI builder
/// - Flow-level display name via <c>DataFlowBuilder.DisplayName()</c>
/// - Multi-module metadata contributions
/// </summary>
public class DataFlowMetadataApiTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static IDataFlowBlockMetadataStore GetBlockStore(IServiceCollection services) =>
        services.BuildServiceProvider().GetRequiredService<IDataFlowBlockMetadataStore>();

    private static IDataFlowFlowMetadataStore GetFlowStore(IServiceCollection services) =>
        services.BuildServiceProvider().GetRequiredService<IDataFlowFlowMetadataStore>();

    // ── Inline metadata via AddDataFlows ────────────────────────────────────

    [Fact]
    public void AddDataFlows_WithInlineBlockMetadata_RegistersDisplayName()
    {
        var services = new ServiceCollection();

        services.AddDataFlows("global", df =>
        {
            df.AddActorBlock<int, string, TransformActor<int, string>>("transformer",
                meta => meta.DisplayName("My Transformer"));
        });

        var store = GetBlockStore(services);
        Assert.Equal("My Transformer", store.GetDisplayName("global:transformer"));
    }

    [Fact]
    public void AddDataFlows_MultipleModules_EachContributesBlockMetadata()
    {
        var services = new ServiceCollection();

        services.AddDataFlows("module-a", df =>
        {
            df.AddActorBlock<int, string, TransformActor<int, string>>("worker",
                meta => meta.DisplayName("A Worker"));
        });

        services.AddDataFlows("module-b", df =>
        {
            df.AddActorBlock<int, string, TransformActor<int, string>>("worker",
                meta => meta.DisplayName("B Worker"));
        });

        var store = GetBlockStore(services);
        Assert.Equal("A Worker", store.GetDisplayName("module-a:worker"));
        Assert.Equal("B Worker", store.GetDisplayName("module-b:worker"));
    }

    [Fact]
    public void AddDataFlows_WithTypeLabel_RegistersTypeLabel()
    {
        var services = new ServiceCollection();

        services.AddDataFlows("global", df =>
        {
            df.AddActorBlock<int, string, TransformActor<int, string>>("transformer",
                meta => meta.DisplayName("Transformer").TypeLabel("Custom Type"));
        });

        var store = GetBlockStore(services);
        Assert.Equal("Transformer", store.GetDisplayName("global:transformer"));
        Assert.Equal("Custom Type", store.GetTypeLabel("global:transformer"));
    }

    // ── AddEpochBuffer on DataFlowBuilder ────────────────────────────────────

    [Fact]
    public void AddEpochBuffer_RegistersBlock_InServiceCollection()
    {
        var services = new ServiceCollection();

        services.AddDataFlows("global", df =>
        {
            df.AddEpochBuffer<int>("my-buffer", capacity: 50);
        });

        var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();
        var block = scope.ServiceProvider.GetKeyedService<IBlock>("global:my-buffer");
        Assert.NotNull(block);
        Assert.IsType<EpochBufferBlock<int>>(block);
    }

    [Fact]
    public void AddEpochBuffer_WithMetadataCallback_RegistersDisplayName()
    {
        var services = new ServiceCollection();

        services.AddDataFlows("global", df =>
        {
            df.AddEpochBuffer<int>("erp-buffer", capacity: 100,
                meta => meta.DisplayName("ERP Buffer"));
        });

        var store = GetBlockStore(services);
        Assert.Equal("ERP Buffer", store.GetDisplayName("global:erp-buffer"));
    }

    [Fact]
    public void AddEpochBuffer_ZeroCapacity_ThrowsArgumentOutOfRangeException()
    {
        var services = new ServiceCollection();

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            services.AddDataFlows("global", df =>
            {
                df.AddEpochBuffer<int>("buffer", capacity: 0);
            }));

        Assert.Equal("capacity", ex.ParamName);
    }

    [Fact]
    public void AddEpochBuffer_NegativeCapacity_ThrowsArgumentOutOfRangeException()
    {
        var services = new ServiceCollection();

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            services.AddDataFlows("global", df =>
            {
                df.AddEpochBuffer<int>("buffer", capacity: -1);
            }));

        Assert.Equal("capacity", ex.ParamName);
    }

    [Fact]
    public void AddEpochBuffer_AppliesNamespacePrefix()
    {
        var services = new ServiceCollection();

        services.AddDataFlows("my-module", df =>
        {
            df.AddEpochBuffer<string>("queue", capacity: 10);
        });

        var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();
        var block = scope.ServiceProvider.GetKeyedService<IBlock>("my-module:queue");
        Assert.NotNull(block);
    }

    // ── Flow-level display name ──────────────────────────────────────────────

    [Fact]
    public void AddDataFlows_WithDisplayName_RegistersFlowDisplayName()
    {
        var services = new ServiceCollection();

        services.AddDataFlows("journal-v2", df =>
        {
            df.DisplayName("Journal Processing Flow");
        });

        var store = GetFlowStore(services);
        Assert.Equal("Journal Processing Flow", store.GetDisplayName("journal-v2"));
    }

    [Fact]
    public void AddDataFlows_MultipleModulesWithDisplayNames_EachRegistersIndependently()
    {
        var services = new ServiceCollection();

        services.AddDataFlows("module-a", df =>
        {
            df.DisplayName("Module A Flow");
        });

        services.AddDataFlows("module-b", df =>
        {
            df.DisplayName("Module B Flow");
        });

        var store = GetFlowStore(services);
        Assert.Equal("Module A Flow", store.GetDisplayName("module-a"));
        Assert.Equal("Module B Flow", store.GetDisplayName("module-b"));
    }

    [Fact]
    public void AddDataFlows_WithoutDisplayName_FlowDisplayNameIsNull()
    {
        // When no module sets a display name, IDataFlowFlowMetadataStore is not registered,
        // so resolving it returns null — no display name entry exists.
        var services = new ServiceCollection();

        services.AddDataFlows("no-name", df =>
        {
            // No DisplayName set
        });

        var sp = services.BuildServiceProvider();
        var store = sp.GetService<IDataFlowFlowMetadataStore>();
        // Store is not registered when no flow has a display name
        Assert.True(store is null || store.GetDisplayName("no-name") is null);
    }

    [Fact]
    public void AddDataFlows_WithoutDisplayName_FlowMetadataStoreNotRegistered()
    {
        // If no module sets a display name, IDataFlowFlowMetadataStore should NOT be registered.
        var services = new ServiceCollection();

        services.AddDataFlows("no-name", df => { });

        var sp = services.BuildServiceProvider();
        var store = sp.GetService<IDataFlowFlowMetadataStore>();
        Assert.Null(store);
    }

    [Fact]
    public void DataFlowBuilder_DisplayName_IsChainable()
    {
        var services = new ServiceCollection();

        // DisplayName should return the builder for chaining
        services.AddDataFlows("chained", df =>
        {
            df.DisplayName("Chained Flow")
              .AddActorBlock<int, string, TransformActor<int, string>>("step",
                  meta => meta.DisplayName("My Step"));
        });

        var blockStore = GetBlockStore(services);
        Assert.Equal("My Step", blockStore.GetDisplayName("chained:step"));

        var flowStore = GetFlowStore(services);
        Assert.Equal("Chained Flow", flowStore.GetDisplayName("chained"));
    }

    // ── IDataFlowBlockMetadataStore singleton contract ───────────────────────

    [Fact]
    public void AddDataFlows_MultipleModulesWithMetadata_RegistersExactlyOneSingleton()
    {
        var services = new ServiceCollection();

        services.AddDataFlows("module-a", df =>
        {
            df.AddActorBlock<int, string, TransformActor<int, string>>("worker",
                meta => meta.DisplayName("A Worker"));
        });
        services.AddDataFlows("module-b", df =>
        {
            df.AddActorBlock<int, string, TransformActor<int, string>>("worker",
                meta => meta.DisplayName("B Worker"));
        });

        var count = services.Count(d => d.ServiceType == typeof(IDataFlowBlockMetadataStore));
        Assert.Equal(1, count);
    }
}
