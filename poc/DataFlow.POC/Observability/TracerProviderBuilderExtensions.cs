namespace DataFlow.POC.Observability;

using OpenTelemetry.Trace;

/// <summary>
/// Extension methods for configuring OpenTelemetry TracerProvider to collect DataFlow POC activities.
/// </summary>
public static class TracerProviderBuilderExtensions
{
    /// <summary>
    /// Adds DataFlow POC activity source to the TracerProvider.
    /// </summary>
    /// <param name="builder">The TracerProviderBuilder to configure.</param>
    /// <returns>The configured TracerProviderBuilder.</returns>
    public static TracerProviderBuilder AddDataFlowPOC(this TracerProviderBuilder builder)
    {
        return builder.AddSource("DataFlow.POC");
    }
}
