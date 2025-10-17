namespace Tests.DataFlow;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;
using Uniun.DataFlow;
using Uniun.DataFlow.Blocks.InputChannel;
using Xunit;
using Xunit.Abstractions;

/// <summary>
/// Tests for InputChannelBlock to ensure it handles various edge cases correctly
/// </summary>
[UnitTest]
public class InputChannelBlockTests
{
    private readonly ITestOutputHelper _output;
    private readonly ServiceCollection _services;

    public InputChannelBlockTests(ITestOutputHelper output)
    {
        _output = output;
        _services = new ServiceCollection();
        AddDefaultServices();
    }

    private void AddDefaultServices()
    {
        _services.AddLogging(builder => builder.AddXUnit(_output));
        _services.AddDataFlows();
        _services.AddDataFlowMetrics();
    }

    private IDataFlowContext CreateContext(string name, IServiceProvider provider, CancellationToken ct = default)
    {
        return DataFlowContextTestUtils.GetContext(name, Guid.NewGuid(), provider, ct);
    }

    [Fact]
    public async Task Complete_DoesNotThrow_WhenCalledBeforeCoreExecuteAsync()
    {
        // Arrange
        var sp = _services.BuildServiceProvider();
        var logger = sp.GetRequiredService<ILogger<InputChannelBlock<int>>>();
        var channelFactory = sp.GetRequiredService<IBoundedChannelFactory>();

        var block = new InputChannelBlock<int>("test-block", logger, channelFactory);

        // Act - Call Complete() before CoreExecuteAsync() has started
        // This should NOT throw a NullReferenceException
        Should.NotThrow(() => block.Complete());
    }

    [Fact]
    public async Task Complete_AllowsExecutionToFinishImmediately_WhenCalledBeforeCoreExecuteAsync()
    {
        // Arrange
        var sp = _services.BuildServiceProvider();
        var logger = sp.GetRequiredService<ILogger<InputChannelBlock<int>>>();
        var channelFactory = sp.GetRequiredService<IBoundedChannelFactory>();

        var block = new InputChannelBlock<int>("test-block", logger, channelFactory);
        var context = CreateContext("test", sp);

        // Act - Call Complete() before CoreExecuteAsync()
        block.Complete();

        // Now execute the block - it should complete immediately since Complete() was already called
        var executeTask = block.ExecuteAsync(context);

        // Assert - Execution should complete quickly without hanging
        var completedTask = await Task.WhenAny(executeTask, Task.Delay(5000));
        completedTask.ShouldBe(executeTask, "Block should complete immediately when Complete() was called before execution");

        await executeTask; // Should not throw
    }

    [Fact]
    public async Task Complete_AllowsNormalExecution_WhenCalledDuringCoreExecuteAsync()
    {
        // Arrange
        var sp = _services.BuildServiceProvider();
        var logger = sp.GetRequiredService<ILogger<InputChannelBlock<int>>>();
        var channelFactory = sp.GetRequiredService<IBoundedChannelFactory>();

        var block = new InputChannelBlock<int>("test-block", logger, channelFactory);
        var context = CreateContext("test", sp);

        // Act - Start execution, then call Complete()
        var executeTask = block.ExecuteAsync(context);

        // Give CoreExecuteAsync time to start
        await Task.Delay(100);

        // Now call Complete()
        block.Complete();

        // Assert - Execution should complete normally
        var completedTask = await Task.WhenAny(executeTask, Task.Delay(5000));
        completedTask.ShouldBe(executeTask, "Block should complete after Complete() is called during execution");

        await executeTask; // Should not throw
    }

    [Fact]
    public async Task WriteAsync_AndComplete_WorksCorrectly()
    {
        // Arrange
        var sp = _services.BuildServiceProvider();
        var logger = sp.GetRequiredService<ILogger<InputChannelBlock<int>>>();
        var channelFactory = sp.GetRequiredService<IBoundedChannelFactory>();

        var block = new InputChannelBlock<int>("test-block", logger, channelFactory);
        var context = CreateContext("test", sp);

        var receivedItems = new List<int>();

        // Act - Start execution in background
        var executeTask = Task.Run(async () => await block.ExecuteAsync(context));

        // Give CoreExecuteAsync time to start
        await Task.Delay(100);

        // Write some items
        await block.WriteAsync(1);
        await block.WriteAsync(2);
        await block.WriteAsync(3);

        // Read items from the block
        var readTask = Task.Run(async () =>
        {
            await foreach (var item in block.GetAsyncEnumerable(null!, context.CancellationToken))
            {
                receivedItems.Add(item);
            }
        });

        // Complete the block
        block.Complete();

        // Wait for everything to finish
        await Task.WhenAll(executeTask, readTask);

        // Assert
        receivedItems.ShouldBe(new[] { 1, 2, 3 });
    }
}
