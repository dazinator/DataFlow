// ReSharper disable once CheckNamespace
namespace Uniun.DataFlow;

using System.Diagnostics;

public interface IDataFlowContext
{
    Guid InvocationId { get; set; }
    CancellationToken CancellationToken { get; set; }
    IServiceProvider ServiceProvider { get; set; }
    string Name { get; set; }

    ///// <summary>
    ///// Additional tags to include in the DataFlow metrics for this execution.
    ///// </summary>
    // public TagList CustomTags { get; set; }

    ///// <summary>
    ///// Additional metrics dimensions to be added to the DataFlow metrics
    ///// </summary>
    //IDictionary<string, string> Dimensions { get; }

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


