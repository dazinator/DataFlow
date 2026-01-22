namespace DataFlow.POC.Tests.TestHelpers;

using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Helper for creating test execution contexts.
/// Reduces boilerplate for creating actor execution contexts in unit tests.
/// </summary>
public static class TestContext
{
    /// <summary>
    /// Creates a simple execution context for testing.
    /// 
    /// Usage:
    /// var context = TestContext.Create();
    /// </summary>
    public static IExecutionContext CreateExecution(
        IServiceProvider? serviceProvider = null,
        CancellationToken cancellationToken = default,
        ITriggerContext? triggerContext = null)
    {
        return new ExecutionContext(
            serviceProvider ?? new ServiceCollection().BuildServiceProvider(),
            cancellationToken,
            Guid.NewGuid(),
            null,
            null,
            triggerContext);
    }

    /// <summary>
    /// Creates a simple actor execution context for testing.
    /// 
    /// Usage:
    /// var context = TestContext.CreateActor();
    /// </summary>
    public static IActorExecutionContext CreateActor(
        CancellationToken cancellationToken = default,
        ITriggerContext? triggerContext = null)
    {
        return new SimpleActorExecutionContext(cancellationToken, triggerContext);
    }

    private class SimpleActorExecutionContext : IActorExecutionContext
    {
        public SimpleActorExecutionContext(CancellationToken cancellationToken, ITriggerContext? triggerContext)
        {
            CancellationToken = cancellationToken;
            InvocationId = Guid.NewGuid();
            TriggerContext = triggerContext;
            Parameters = new TriggerContextParameterProvider(triggerContext);
        }

        public CancellationToken CancellationToken { get; }
        public Guid InvocationId { get; }
        public IEpochCoordinator? EpochCoordinator => null;
        public ITriggerContext? TriggerContext { get; }
        public IParameterProvider Parameters { get; }
        public void RequestRotation() { }
    }
}
