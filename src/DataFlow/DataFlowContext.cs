// ReSharper disable once CheckNamespace
namespace Uniun.DataFlow;

using System;
using System.Diagnostics;

public class DataFlowContext : IDataFlowContext
{
    public DataFlowContext()
    {

    }

    public Guid InvocationId { get; set; }
    public string Name { get; set; }
    public IServiceProvider ServiceProvider { get; set; }
    public CancellationToken CancellationToken { get; set; }
    // public IDictionary<string, string> Dimensions => new Dictionary<string, string>();
    
    // Method to add dimensions during setup
    //public IDataFlowContext AddDimension(string key, string value)
    //{
    //    Dimensions[key] = value;
    //    return this;
    //}

    //// Method to add multiple dimensions
    //public IDataFlowContext AddDimensions(IDictionary<string, string> dimensions)
    //{
    //    foreach (var pair in dimensions)
    //    {
    //        Dimensions[pair.Key] = pair.Value;
    //    }
    //    return this;
    //}
}
