namespace DataFlow.POC.Observability;

using System.Diagnostics.Metrics;

/// <summary>
/// Provides access to the Meter instance for DataFlow POC metrics.
/// </summary>
public interface IMeterAccessor
{
    /// <summary>
    /// Gets the Meter instance used for creating metrics instruments.
    /// May be null if metrics collection is not configured.
    /// </summary>
    Meter? Meter { get; }
}
