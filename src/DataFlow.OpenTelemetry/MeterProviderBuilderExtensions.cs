namespace Uniun.DataFlow.OpenTelemetry;

using global::OpenTelemetry.Metrics;
using Uniun.DataFlow.Metrics;

// Extension methods must be defined in a static class
public static class MeterProviderBuilderExtensions
{
    public static MeterProviderBuilder AddDataFlows(this MeterProviderBuilder builder)
    {
        return builder
            .AddMeter(MeterAccessor.MeterName);
        // You could also add other meters that your library uses
        // Or add specific instrumentation setup your library needs
    }
}
