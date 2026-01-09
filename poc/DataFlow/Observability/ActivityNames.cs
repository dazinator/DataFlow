namespace DataFlow.POC.Observability;

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
        [Description("The name of the flow")]
        public const string FlowName = "FlowName";

        /// <summary>
        /// The name of the block.
        /// </summary>
        [Description("The name of the block")]
        public const string BlockName = "BlockName";

        /// <summary>
        /// Indicates if the execution was cancelled.
        /// </summary>
        [Description("Indicates if the execution was cancelled")]
        public const string Cancelled = "Cancelled";

        /// <summary>
        /// The type of error that occurred.
        /// </summary>
        [Description("The type of error that occurred")]
        public const string ErrorType = "error.type";
    }
}
