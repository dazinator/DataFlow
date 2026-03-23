namespace DataFlow.POC.Tests.TestHelpers;

using DataFlow.POC.Checkpointing;
using DataFlow.POC.Core;
using DataFlow.POC.Observability;
using Microsoft.Extensions.DependencyInjection;
using DataFlow.POC.Registry;

/// <summary>
/// Base test execution context that provides default implementations for new properties.
/// Existing tests can inherit from this to avoid breaking changes.
/// </summary>
public class TestExecutionContextBase : IExecutionContext
{
    public IServiceScopeFactory? ScopeFactory { get; set; } = null;
    public CancellationToken CancellationToken { get; set; }
    public Guid InvocationId { get; set; } = Guid.NewGuid();
    public ICheckpoint? RecoveryCheckpoint { get; set; }
    public string? FlowName { get; set; }
    public IDataFlowMetrics? Metrics { get; set; }
    public string? CurrentBlockName { get; set; }
    public ITriggerContext? TriggerContext { get; set; }
    public IParameterProvider Parameters => new TriggerContextParameterProvider(TriggerContext);
    public string? TriggerParamsJson { get; set; }
}
