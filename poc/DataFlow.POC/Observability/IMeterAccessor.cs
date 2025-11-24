namespace DataFlow.POC.Observability;

using System.Diagnostics.Metrics;

public interface IMeterAccessor
{
    Meter? Meter { get; }
}
