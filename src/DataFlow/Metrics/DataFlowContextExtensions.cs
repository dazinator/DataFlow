namespace Uniun.DataFlow.Metrics;
using System.Diagnostics;

public static class ActivityExtensions
{
    public static void AddTags(this Activity activity, TagList tags)
    {
        foreach (var tag in tags)
        {
            activity?.SetTag(tag.Key, tag.Value);
        }      
    }
}
