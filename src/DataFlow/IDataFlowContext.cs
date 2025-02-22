// ReSharper disable once CheckNamespace
namespace Uniun.DataFlow;

public interface IDataFlowContext
{
    CancellationToken CancellationToken { get; set; }

    IServiceProvider ServiceProvider { get; set; }
}

