namespace Uniun.DataFlow.Blocks.BatchBlock;
using System;
using Uniun.DataFlow.Blocks;

public class BatchBlockOptions : BlockOptions
{
    public int MaxBatchSize { get; set; } = 100;
    public TimeSpan WindowPeriod { get; set; } = TimeSpan.FromSeconds(1);
}
