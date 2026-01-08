namespace DataFlow.POC.Observability;

using System.Diagnostics.Metrics;

/// <summary>
/// Provides access to the Meter instance for the DataFlow POC metrics.
/// </summary>
public class MeterAccessor : IMeterAccessor
{
    public Meter? Meter { get; }

    public const string MeterName = "DataFlow.POC";

    public MeterAccessor(IMeterFactory meterFactory)
    {
        Meter = meterFactory.Create(MeterName);
    }
}
