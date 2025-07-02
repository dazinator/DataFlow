namespace Uniun.DataFlow;

using System.Collections.Generic;

public interface IMetricsTagsContext
{
    KeyValuePair<string, object?>[] CompletionTags { get; }
    string Name { get; }
    KeyValuePair<string, object?>[] Tags { get; } 
   
}
