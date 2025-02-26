// ReSharper disable once CheckNamespace
namespace Uniun.DataFlow;
public interface IDataFlowContext
{
    Guid InvocationId { get; set; }

    CancellationToken CancellationToken { get; set; }

    IServiceProvider ServiceProvider { get; set; }

    /// <summary>
    /// Additional metrics dimensions to be added to the DataFlow metrics
    /// </summary>
    IDictionary<string, string> Dimensions { get; }

    public IDataFlowContext AddDimension(string key, string value)
    {
        Dimensions[key] = value;
        return this;
    }

    // Method to add multiple dimensions
    public IDataFlowContext AddDimensions(IDictionary<string, string> dimensions)
    {
        foreach (var pair in dimensions)
        {
            Dimensions[pair.Key] = pair.Value;
        }
        return this;
    }
}
   

