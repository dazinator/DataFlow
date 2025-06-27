namespace Uniun.DataFlow;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
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
        if(configure is not null)
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
            var builder = new DataFlowBuilder(sp);
            builder.Name = name;
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
            var config = factory?.Invoke(sp);
            if (config is null)
            {
                throw new Exception("Factory returned null configuration.");
            }
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
}
