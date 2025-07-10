// ReSharper disable once CheckNamespace
namespace Uniun.DataFlow;

public interface IDataFlow
{
    string Name { get; set; }
    Task ExecuteAsync(IDataFlowContext context);
}
