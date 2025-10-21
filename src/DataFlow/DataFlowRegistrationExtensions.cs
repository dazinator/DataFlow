namespace Uniun.DataFlow;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Uniun.DataFlow.Builder.Graph;
using Uniun.DataFlow.Metrics;

public static class DataFlowRegistrationExtensions
{

    /// <summary>
    /// Registers the default implementation of <see cref="IMeterAccessor"/> with the DI container.
    /// This provides the `Meter` instance for the DataFlow metrics. The meter is name "Uniun.DataFlow" by default.
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    /// <remarks>In multitenant scenarios, you can register this in the "root" container to have a single metrics pipeline at root level to collect all dataflow metrics which may be added in seperate container (e.g per tenant) instances.
    /// </remarks>
    public static IServiceCollection AddDataFlowMetrics(this IServiceCollection services)
    {
        services.AddMetrics();
        services.TryAddSingleton<IMeterAccessor, MeterAccessor>();
        return services;
    }


    public static IServiceCollection AddDataFlows(
        this IServiceCollection services,
        Action<DataFlowsOptions>? configure = null)
    {
        var optionsBuilder = services.AddOptionsWithValidateOnStart<DataFlowsOptions>();
        if (configure is not null)
        {
            optionsBuilder.Configure(configure);
        }

        services.TryAddSingleton<DataFlowThrottler>();
        services.TryAddTransient(typeof(FlowExecutor<>));

        services.TryAddSingleton<IBoundedChannelFactory, MonitoredChannelFactory>();
        // Register open generic executor once      

        services.TryAddSingleton<IDataFlowMetrics, DataFlowMetrics>();
        return services;
    }

    public static IServiceCollection AddDataFlow<TConfig>(
        this IServiceCollection services,
        string name)
        where TConfig : class, IDataFlowConfiguration, new()
    {
        services.AddTransient(sp =>
        {
            var builder = new DataFlowBuilder(sp)
            {
                Name = name
            };
            var config = new TConfig();
            config.Configure(builder);
            return new DataFlow<TConfig>(builder.Build());
        });

        return services;
    }


    public static IServiceCollection AddDataFlow<TConfig>(
        this IServiceCollection services,
        Func<IServiceProvider, IDataFlowConfiguration> factory)
        where TConfig : class, IDataFlowConfiguration
    {
        services.AddTransient(sp =>
        {
            var builder = new DataFlowBuilder(sp);
            var config = (factory?.Invoke(sp)) ?? throw new Exception("Factory returned null configuration.");
            config.Configure(builder);
            return new DataFlow<TConfig>(builder.Build());
        });

        return services;
    }

    // Add method that supports IOptionsMonitor for reloadable configuration
    public static IServiceCollection AddDataFlowWithOptions<TConfig>(
        this IServiceCollection services)
        where TConfig : class, IDataFlowConfiguration, new()
    {
        services.AddTransient(sp =>
        {
            var builder = new DataFlowBuilder(sp);
            var options = sp.GetRequiredService<IOptionsMonitor<TConfig>>();
            var config = options.CurrentValue;
            config.Configure(builder);
            return new DataFlow<TConfig>(builder.Build());
        });

        return services;
    }

    /// <summary>
    /// Registers a named structured dataflow with keyed services.
    /// The dataflow instance can be resolved later using the same key.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="name">The unique name/key for this dataflow.</param>
    /// <param name="configure">A callback to configure the dataflow using the structured builder.</param>
    /// <param name="lifetime">The service lifetime for the dataflow (default is Transient).</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddKeyedDataFlow(
        this IServiceCollection services,
        string name,
        Action<Uniun.DataFlow.Builder.Graph.IStructuredDataFlowBuilder> configure,
        ServiceLifetime lifetime = ServiceLifetime.Transient)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name cannot be null or whitespace.", nameof(name));
        }

        if (configure == null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        // Register the dataflow instance with the specified key and lifetime
        var descriptor = new ServiceDescriptor(
            typeof(IDataFlow),
            name,
            (sp, key) =>
            {
                var builder = new Builder.Graph.StructuredDataFlowBuilder(sp, name);
                configure(builder);
                // Build async but wait synchronously (required by DI container)
                return builder.Build().GetAwaiter().GetResult();
            },
            lifetime);

        services.Add(descriptor);

        return services;
    }

    /// <summary>
    /// Registers a named structured dataflow as a transient service.
    /// A new instance is created each time it is resolved.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="name">The unique name/key for this dataflow.</param>
    /// <param name="configure">A callback to configure the dataflow using the structured builder.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTransientDataFlow(
        this IServiceCollection services,
        string name,
        Action<Uniun.DataFlow.Builder.Graph.IStructuredDataFlowBuilder> configure)
    {
        return services.AddKeyedDataFlow(name, configure, ServiceLifetime.Transient);
    }

    /// <summary>
    /// Registers a named structured dataflow as a scoped service.
    /// One instance is created per scope (e.g., per HTTP request).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="name">The unique name/key for this dataflow.</param>
    /// <param name="configure">A callback to configure the dataflow using the structured builder.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddScopedDataFlow(
        this IServiceCollection services,
        string name,
        Action<Uniun.DataFlow.Builder.Graph.IStructuredDataFlowBuilder> configure)
    {
        return services.AddKeyedDataFlow(name, configure, ServiceLifetime.Scoped);
    }

    /// <summary>
    /// Registers a named structured dataflow as a singleton service.
    /// One instance is created and shared for the lifetime of the application.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="name">The unique name/key for this dataflow.</param>
    /// <param name="configure">A callback to configure the dataflow using the structured builder.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSingletonDataFlow(
        this IServiceCollection services,
        string name,
        Action<Uniun.DataFlow.Builder.Graph.IStructuredDataFlowBuilder> configure)
    {
        return services.AddKeyedDataFlow(name, configure, ServiceLifetime.Singleton);
    }

    /// <summary>
    /// Resolves a keyed dataflow by name.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="name">The name/key of the dataflow to resolve.</param>
    /// <returns>The dataflow instance.</returns>
    public static IDataFlow GetDataFlow(
        this IServiceProvider serviceProvider,
        string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name cannot be null or whitespace.", nameof(name));
        }

        return serviceProvider.GetRequiredKeyedService<IDataFlow>(name);
    }

    /// <summary>
    /// Renders a Mermaid diagram for a keyed dataflow.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="name">The name/key of the dataflow to render.</param>
    /// <returns>A Mermaid diagram representation of the dataflow.</returns>
    public static string RenderDataFlowMermaidDiagram(
        this IServiceProvider serviceProvider,
        string name)
    {
        var dataflow = serviceProvider.GetDataFlow(name);
        if (dataflow.Graph == null)
        {
            throw new InvalidOperationException($"Dataflow '{name}' does not have a graph available.");
        }
        
        return dataflow.Graph.ToMermaidDiagram(serviceProvider: serviceProvider);
    }    
}
