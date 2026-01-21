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
        CancellationToken cancellationToken = default)
    {
        return new ExecutionContext(
            serviceProvider ?? new ServiceCollection().BuildServiceProvider(),
            cancellationToken);
    }

    /// <summary>
    /// Creates a simple actor execution context for testing.
    /// 
    /// Usage:
    /// var context = TestContext.CreateActor();
    /// </summary>
    public static IActorExecutionContext CreateActor(
        CancellationToken cancellationToken = default)
    {
        return new SimpleActorExecutionContext(cancellationToken);
    }

    private class SimpleActorExecutionContext : IActorExecutionContext
    {
        public SimpleActorExecutionContext(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            InvocationId = Guid.NewGuid();
        }

        public CancellationToken CancellationToken { get; }
        public Guid InvocationId { get; }
        public IEpochCoordinator? EpochCoordinator => null;
        public void RequestRotation() { }
    }
}
