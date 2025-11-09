namespace Tests.Shared;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Uniun.DataFlow;
using Xunit.Abstractions;

/// <summary>
/// Base class for DataFlow tests that provides standard test setup including
/// dependency injection, logging, and DataFlow services.
/// </summary>
public abstract class DataFlowTestBase
{
    /// <summary>
    /// Gets the test output helper for writing test output.
    /// </summary>
    protected ITestOutputHelper Output { get; }

    /// <summary>
    /// Gets the service collection for configuring test dependencies.
    /// </summary>
    protected ServiceCollection Services { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DataFlowTestBase"/> class.
    /// </summary>
    /// <param name="output">The test output helper.</param>
    protected DataFlowTestBase(ITestOutputHelper output)
    {
        Output = output;
        Services = new ServiceCollection();
        AddDefaultServices();
    }

    /// <summary>
    /// Adds default services to the service collection including logging, metrics, and DataFlow services.
    /// Override this method to customize the service configuration for specific test classes.
    /// </summary>
    protected virtual void AddDefaultServices()
    {
        Services.AddLogging(builder => builder.AddXUnit(Output));
        Services.AddDataFlowMetrics();
        Services.AddDataFlows();
    }
}
