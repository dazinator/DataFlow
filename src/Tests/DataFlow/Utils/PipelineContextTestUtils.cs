namespace Tests.DataFlow.Utils;

using System;

// Builder classes

public static class DataFlowContextTestUtils
{

    public static IDataFlowContext GetContext(string name, Guid invocationId, IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        var context = new DataFlowContext()
        {
            CancellationToken = cancellationToken,
            ServiceProvider = serviceProvider
        };
        //null,
        ////pipelineInfo: new PipelineRegsitrationInfo(){Name = name},
        //invocationId,
        //pipelineId: Guid.NewGuid(),
        //branchName: string.Empty,
        //serviceProvider: serviceProvider,
        //cancellationToken: cancellationToken);

        return context;
    }
}
