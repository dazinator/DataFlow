// ReSharper disable once CheckNamespace
namespace Uniun.DataFlow;

using Uniun.DataFlow.Builder;

public interface IDataFlowConfiguration
{
    void Configure(DataFlowBuilder builder);
}
