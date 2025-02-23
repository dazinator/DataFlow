namespace Uniun.DataFlow;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

public static class DataFlowRegistrationExtensions
{
    public static IServiceCollection AddDataFlows(
        this IServiceCollection services,
        int maxConcurrentFlows)
    {
        services.AddSingleton(new DataFlowThrottler(maxConcurrentFlows));
        // Register open generic executor once
        services.AddTransient(typeof(FlowExecutor<>));
        return services;
    }

    public static IServiceCollection AddDataFlow<TConfig>(
        this IServiceCollection services,
        Action<BlockOptions>? configureOptions = null)
        where TConfig : class, IDataFlowConfiguration, new()
    {
        services.AddTransient(sp =>
        {
            var builder = new DataFlowBuilder(sp);
            var config = new TConfig();
            config.Configure(builder);
            return new DataFlow<TConfig>(builder.Build());
        });

        return services;
    }


    public static IServiceCollection AddDataFlow<TConfig>(
        this IServiceCollection services,
        Func<IServiceProvider, IDataFlowConfiguration> factory,
        Action<BlockOptions>? configureOptions = null)
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
