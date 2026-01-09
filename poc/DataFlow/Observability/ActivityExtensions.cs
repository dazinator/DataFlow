namespace DataFlow.POC.Observability;

using System.Diagnostics;

public static class ActivityExtensions
{
    public static void AddTags(this Activity activity, TagList tags)
    {
        foreach (var tag in tags)
        {
            activity.AddTag(tag.Key, tag.Value);
        }
    }
}
