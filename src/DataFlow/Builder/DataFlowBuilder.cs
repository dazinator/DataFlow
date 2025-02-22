// ReSharper disable once CheckNamespace
namespace Uniun.DataFlow.Builder;

public class DataFlowBuilder : IDataFlowBuilder
{
    public DataFlowBuilder(IServiceProvider serviceProvider)
    {
        Blocks = new();
        ServiceProvider = serviceProvider;
    }

    public Dictionary<string, IBlock> Blocks { get; }

    public IServiceProvider ServiceProvider { get; }

    public DataFlow Build()
    {
        return new DataFlow(Blocks.Values.ToList());
    }
}

// Extension methods for DI registration
