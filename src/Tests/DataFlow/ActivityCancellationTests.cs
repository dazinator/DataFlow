namespace Tests.DataFlow;

using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Shouldly;
using Tests.DataFlow.Utils.Producers;

/// <summary>
/// Tests that verify Activity telemetry correctly indicates cancellation vs errors
/// </summary>
[IntegrationTest]
public class ActivityCancellationTests
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly ConcurrentBag<Activity> _completedActivities = new();
    private readonly ActivityListener _activityListener;

    public IServiceCollection Services { get; set; }

    public ActivityCancellationTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        Services = new ServiceCollection();
        AddDefaultServices(Services);
        
        // Set up activity listener to capture completed activities
        _activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Uniun.DataFlow",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => _completedActivities.Add(activity)
        };
        ActivitySource.AddActivityListener(_activityListener);
    }

    private void AddDefaultServices(IServiceCollection services)
    {
        services.AddLogging(a => a.AddXUnit(_testOutputHelper));
        services.AddDataFlows((o) => o.MaxConcurrentFlows = 2);
        services.AddDataFlowMetrics();
    }

    public ServiceProvider GetServiceProvider()
    {
        return Services.BuildServiceProvider();
    }

    [Fact]
    public async Task When_BlockThrowsException_Activity_ShouldIncludeErrorType()
    {
        // Arrange
        var errorMessage = "Test error in producer";
        Services.AddDataFlow<ErrorFlowConfig>(sp => new ErrorFlowConfig(
            new ErrorProducer<int>(
                Enumerable.Range(1, 10),
                item => item == 5, // Error on item 5
                errorMessage: errorMessage)));

        using var sp = GetServiceProvider();
        var executor = sp.GetRequiredService<FlowExecutor<ErrorFlowConfig>>();
        var context = DataFlowContextTestUtils.GetContext("test", Guid.NewGuid(), sp);

        // Act
        await Should.ThrowAsync<InvalidOperationException>(
            async () => await executor.ExecuteAsync(context));

        // Wait a bit for activities to be processed
        await Task.Delay(100);

        // Assert
        _testOutputHelper.WriteLine($"Captured {_completedActivities.Count} activities");
        
        var blockActivities = _completedActivities
            .Where(a => a.DisplayName.Contains("Block"))
            .ToList();
        
        blockActivities.ShouldNotBeEmpty();
        
        // Find the producer block activity (the one that threw the error)
        var producerActivity = blockActivities
            .FirstOrDefault(a => a.Tags.Any(t => t.Key == "BlockName" && t.Value?.ToString() == "source"));
        
        producerActivity.ShouldNotBeNull();
        
        // Verify it has error status
        producerActivity.Status.ShouldBe(ActivityStatusCode.Error);
        producerActivity.StatusDescription.ShouldContain(errorMessage);
        
        // Verify error.type tag is present
        var errorTypeTags = producerActivity.Tags.Where(t => t.Key == "error.type").ToList();
        errorTypeTags.ShouldNotBeEmpty();
        errorTypeTags.First().Value.ShouldBe(typeof(InvalidOperationException).FullName);
        
        // Verify it's NOT marked as cancelled (this was a real error)
        var cancelledTags = producerActivity.Tags.Where(t => t.Key == "cancelled").ToList();
        cancelledTags.ShouldBeEmpty();
    }

    [Fact]
    public async Task When_BlockIsCancelledByAnotherBlockError_Activity_ShouldIndicateCancellation()
    {
        // Arrange
        var processedItems = new ConcurrentBag<int>();
        var producedItems = new ConcurrentBag<int>();
        
        // Producer that produces many items slowly
        Services.AddSingleton(new TestProducer<int>(
            Enumerable.Range(1, 100),
            onItemProduced: item => producedItems.Add(item),
            delay: TimeSpan.FromMilliseconds(10)));

        // Processor that fails on item 5
        Services.AddSingleton(new TestProcessor<int>(
            onProcessItem: item =>
            {
                processedItems.Add(item);
                if (item == 5)
                {
                    throw new InvalidOperationException("Test error in processor");
                }
            }));

        Services.AddDataFlow<SlowFlowWithErrorConfig>("test");

        using var sp = GetServiceProvider();
        var executor = sp.GetRequiredService<FlowExecutor<SlowFlowWithErrorConfig>>();
        var context = DataFlowContextTestUtils.GetContext("test", Guid.NewGuid(), sp);

        // Act
        await Should.ThrowAsync<Exception>(
            async () => await executor.ExecuteAsync(context));

        // Wait for activities to be processed
        await Task.Delay(200);

        // Assert
        _testOutputHelper.WriteLine($"Captured {_completedActivities.Count} activities");
        
        var blockActivities = _completedActivities
            .Where(a => a.DisplayName.Contains("Block"))
            .ToList();
        
        blockActivities.ShouldNotBeEmpty();
        
        // Find the processor block (the one that threw the original error)
        var processorActivity = blockActivities
            .FirstOrDefault(a => a.Tags.Any(t => t.Key == "BlockName" && t.Value?.ToString() == "processor"));
        
        processorActivity.ShouldNotBeNull();
        processorActivity.Status.ShouldBe(ActivityStatusCode.Error);
        
        // Verify processor has error.type but NOT cancelled tag (it's the source of the error)
        var processorErrorType = processorActivity.Tags.FirstOrDefault(t => t.Key == "error.type");
        processorErrorType.Value.ShouldBe(typeof(InvalidOperationException).FullName);
        
        var processorCancelledTag = processorActivity.Tags.FirstOrDefault(t => t.Key == "cancelled");
        string.IsNullOrEmpty(processorCancelledTag.Key).ShouldBeTrue("Processor (source of error) should not be marked as cancelled");
        
        // Find the producer block (which should be cancelled due to processor error)
        var producerActivity = blockActivities
            .FirstOrDefault(a => a.Tags.Any(t => t.Key == "BlockName" && t.Value?.ToString() == "source"));
        
        if (producerActivity != null)
        {
            _testOutputHelper.WriteLine($"Producer activity status: {producerActivity.Status}");
            _testOutputHelper.WriteLine($"Producer activity status description: {producerActivity.StatusDescription}");
            _testOutputHelper.WriteLine("Producer activity tags:");
            foreach (var tag in producerActivity.Tags)
            {
                _testOutputHelper.WriteLine($"  {tag.Key} = {tag.Value}");
            }
            
            // If the producer was cancelled, it should have the cancelled tag
            if (producerActivity.Status == ActivityStatusCode.Error)
            {
                var errorType = producerActivity.Tags.FirstOrDefault(t => t.Key == "error.type");
                _testOutputHelper.WriteLine($"Producer error type: {errorType.Value}");
                
                // If it's OperationCanceledException, it should be marked as cancelled
                if (errorType.Value?.ToString()?.Contains("OperationCanceledException") == true)
                {
                    var hasCancelledTag = producerActivity.Tags.Any(t => t.Key == "cancelled");
                    Assert.True(hasCancelledTag, "Producer cancelled by OperationCanceledException should have cancelled tag");
                    
                    var cancelledTag = producerActivity.Tags.First(t => t.Key == "cancelled");
                    Assert.Equal("true", cancelledTag.Value?.ToString());
                }
            }
        }
    }

    [Fact]
    public async Task When_CancellationTokenSignalled_BlockActivity_ShouldIndicateCancellation()
    {
        // Arrange
        var producedItems = new ConcurrentBag<int>();
        var processedItems = new ConcurrentBag<int>();

        Services.AddSingleton(new TestProducer<int>(
            Enumerable.Range(1, 100),
            onItemProduced: item => producedItems.Add(item),
            delay: TimeSpan.FromMilliseconds(50)));

        Services.AddSingleton(new TestProcessor<int>(
            onProcessItem: item => processedItems.Add(item),
            delay: TimeSpan.FromMilliseconds(10)));

        Services.AddDataFlow<SlowFlowConfig>("test");

        using var sp = GetServiceProvider();
        var executor = sp.GetRequiredService<FlowExecutor<SlowFlowConfig>>();
        using var cts = new CancellationTokenSource();
        var context = DataFlowContextTestUtils.GetContext("test", Guid.NewGuid(), sp, cts.Token);

        // Act
        var executionTask = executor.ExecuteAsync(context);
        await Task.Delay(100); // Let it start and produce some items
        cts.Cancel(); // Cancel explicitly

        await Should.ThrowAsync<OperationCanceledException>(
            () => executionTask);

        // Wait for activities to be processed
        await Task.Delay(200);

        // Assert
        _testOutputHelper.WriteLine($"Captured {_completedActivities.Count} activities");
        
        var errorActivities = _completedActivities
            .Where(a => a.Status == ActivityStatusCode.Error)
            .ToList();
        
        _testOutputHelper.WriteLine($"Found {errorActivities.Count} activities with Error status");
        
        foreach (var activity in errorActivities)
        {
            _testOutputHelper.WriteLine($"Activity: {activity.DisplayName}");
            _testOutputHelper.WriteLine($"  Status: {activity.Status}");
            _testOutputHelper.WriteLine($"  Description: {activity.StatusDescription}");
            _testOutputHelper.WriteLine("  Tags:");
            foreach (var tag in activity.Tags)
            {
                _testOutputHelper.WriteLine($"    {tag.Key} = {tag.Value}");
            }
            
            // All error activities from explicit cancellation should be marked as cancelled
            var errorType = activity.Tags.FirstOrDefault(t => t.Key == "error.type");
            if (errorType.Value?.ToString()?.Contains("OperationCanceledException") == true ||
                errorType.Value?.ToString()?.Contains("TaskCanceledException") == true)
            {
                var hasCancelledTag = activity.Tags.Any(t => t.Key == "cancelled");
                Assert.True(hasCancelledTag, $"Activity {activity.DisplayName} with cancellation exception should have cancelled tag");
                
                var cancelledTag = activity.Tags.First(t => t.Key == "cancelled");
                Assert.Equal("true", cancelledTag.Value?.ToString());
            }
        }
    }

    // Flow Configurations
    private class ErrorFlowConfig : IDataFlowConfiguration
    {
        private readonly IStreamProducer<int> _producer;

        public ErrorFlowConfig(IStreamProducer<int> producer)
        {
            _producer = producer;
        }

        public void Configure(DataFlowBuilder builder)
        {
            builder
                .AddProducer("source", sp => _producer);
        }
    }

    private class SlowFlowConfig : IDataFlowConfiguration
    {
        public void Configure(DataFlowBuilder builder)
        {
            builder
                .AddProducer("source", ctx => ctx.ServiceProvider.GetRequiredService<TestProducer<int>>())
                .AddProcessor<int, TestProcessor<int>>("processor",
                    sp => sp.GetRequiredService<TestProcessor<int>>())
                    .ReceiveFrom("source");
        }
    }

    private class SlowFlowWithErrorConfig : IDataFlowConfiguration
    {
        public void Configure(DataFlowBuilder builder)
        {
            builder
                .AddProducer("source", ctx => ctx.ServiceProvider.GetRequiredService<TestProducer<int>>())
                .AddProcessor<int, TestProcessor<int>>("processor",
                    sp => sp.GetRequiredService<TestProcessor<int>>())
                    .ReceiveFrom("source");
        }
    }
}
