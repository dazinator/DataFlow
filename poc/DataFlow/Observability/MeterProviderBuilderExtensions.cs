namespace DataFlow.POC.Observability;

using OpenTelemetry.Metrics;

/// <summary>
/// Extension methods for configuring OpenTelemetry MeterProvider to collect DataFlow metrics.
/// </summary>
public static class MeterProviderBuilderExtensions
{
    /// <summary>
    /// Adds DataFlow meters to the MeterProvider.
    /// </summary>
    /// <param name="builder">The MeterProviderBuilder to configure.</param>
    /// <returns>The configured MeterProviderBuilder.</returns>
    public static MeterProviderBuilder AddDataFlow(this MeterProviderBuilder builder)
    {
        return builder.AddMeter(MeterAccessor.MeterName);
    }
}
