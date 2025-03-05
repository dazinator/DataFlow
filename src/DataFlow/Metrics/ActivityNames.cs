// ReSharper disable once CheckNamespace
namespace Uniun.DataFlow.Metrics;

using System.ComponentModel;

public static class ActivityNames
{
    internal const string FlowExecute = "DataFlow.Execute";
    internal const string BlockExecute = "Block.Execute";

    internal const string Block = "Block";
    internal const string Flow = "Flow";


    public static class TagNames
    {
        /// <summary>
        /// The invocation id of the flow.
        /// </summary>
        [Description("The invocation id of the flow")]
        public const string FlowInvocationId = "FlowInvocationId";
        /// <summary>
        /// The name of the flow.
        /// </summary>
        [Description("Time name of the flow")]
        public const string FlowName = "FlowName";
        /// <summary>
        /// The name of the block.
        /// </summary>
        [Description("Time name of the block")]
        public const string BlockName = "BlockName";


    }
}
