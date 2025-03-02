// ReSharper disable once CheckNamespace
namespace Uniun.DataFlow.Metrics;
using System.Diagnostics.Metrics;

/// <summary>
/// Provides access to the Meter instance for the DataFlow metrics.
/// </summary>
/// <remarks>This was designed to allow multi-tenant scenarios.
///  - the application can register <see cref="IMeterAccessor"/> to the "root" container , not in per tenant container.
///  - the application can add `DataFlowMetrics` to each tenant container - with different tags - for example, tenant id.
///  - this ensures there is a single Meter, and all metrics can be aggregated and scraped at the root level by a single endpoint.
///    whilst also ensuring that if data flow metrics are added per tenant container, they can be given tenant specific tags.
/// </remarks>
public class MeterAccessor: IMeterAccessor
{
    public Meter Meter { get; }

    public const string MeterName = "Uniun.DataFlow";

    public MeterAccessor(IMeterFactory meterFactory)
    {
        Meter = meterFactory.Create(MeterName);
    }
}




