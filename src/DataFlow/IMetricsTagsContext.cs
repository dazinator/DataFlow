namespace Uniun.DataFlow;

using System.Collections.Generic;

public interface IMetricsTagsContext
{
    KeyValuePair<string, object?>[]? FlowLevelCompletionTags { get; }
    string Name { get; }
    KeyValuePair<string, object?>[] FlowWideTags { get; }

}
