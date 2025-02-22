// ReSharper disable once CheckNamespace
namespace Uniun.DataFlow;

public interface IDataFlow
{
    Task ExecuteAsync(IDataFlowContext context);
}
