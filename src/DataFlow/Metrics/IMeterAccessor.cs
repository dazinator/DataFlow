// ReSharper disable once CheckNamespace
namespace Uniun.DataFlow.Metrics;
using System.Diagnostics.Metrics;

public interface IMeterAccessor
{
    Meter Meter { get; }
}




