namespace Tests.DataFlow;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public class ParallelActivityTest
{
    private readonly ITestOutputHelper _output;

    public ParallelActivityTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task Parallel_ForEachAsync_CompletesBefore_AllTasksComplete()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging(builder => builder.AddXUnit(_output));
        var serviceProvider = serviceCollection.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<ParallelActivityTest>>();

        var taskCompletionSource = new TaskCompletionSource();
        var taskStarted = new List<int>();
        var taskCompleted = new List<int>();
        var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = 3 };

        // Act
        logger.LogInformation("Starting parallel execution...");
        var parallelTask = Parallel.ForEachAsync(
            Enumerable.Range(0, 10),
            parallelOptions,
            async (i, ct) =>
            {
                lock (taskStarted)
                {
                    taskStarted.Add(i);
                    logger.LogInformation("Task {Index} started", i);
                }

                // Simulate long-running task
                var delay = i switch
                {
                    < 3 => TimeSpan.FromMilliseconds(100),
                    < 7 => TimeSpan.FromMilliseconds(500),
                    _ => TimeSpan.FromSeconds(2) // Make last tasks take much longer
                };

                await Task.Delay(delay, ct);

                lock (taskCompleted)
                {
                    taskCompleted.Add(i);
                    logger.LogInformation("Task {Index} completed after {Delay}ms", i, delay.TotalMilliseconds);
                }
            });

        // Log when the Parallel.ForEachAsync returns
        var completionTask = Task.Run(async () =>
        {
            await parallelTask;
            logger.LogInformation("Parallel.ForEachAsync completed");

            // Wait a bit more to see if more tasks complete after ForEachAsync returns
            await Task.Delay(3000);

            // Log final state
            lock (taskStarted)
            {
                logger.LogInformation("Tasks started: {Count} - {Tasks}",
                    taskStarted.Count, string.Join(", ", taskStarted.OrderBy(x => x)));
            }

            lock (taskCompleted)
            {
                logger.LogInformation("Tasks completed: {Count} - {Tasks}",
                    taskCompleted.Count, string.Join(", ", taskCompleted.OrderBy(x => x)));
            }

            taskCompletionSource.SetResult();
        });

        // Wait for everything to complete
        await taskCompletionSource.Task;

        // Assert
        Assert.Equal(10, taskStarted.Count);
        Assert.Equal(10, taskCompleted.Count);
    }

    [Fact]
    public async Task ExecuteParallelActivities_WithDelayedCompletion()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging(builder => builder.AddXUnit(_output));
        var serviceProvider = serviceCollection.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<ParallelActivityTest>>();

        // Create a mock DataFlowContext
        var context = new DataFlowContext
        {
            ServiceProvider = serviceProvider,
            CancellationToken = CancellationToken.None
        };

        var taskStarted = new List<int>();
        var taskCompleted = new List<int>();

        // Create a simple block base to test ExecuteParallelActivities
        var testBlock = new TestBlockBase(logger, "TestBlock");

        // Act
        logger.LogInformation("Starting ExecuteParallelActivities...");
        var startTime = DateTime.UtcNow;

        // This simulates the method from BlockBase that we suspect is completing early
        await testBlock.TestExecuteParallelActivities(context, 5, async (index, ctx) =>
        {
            lock (taskStarted)
            {
                taskStarted.Add(index);
                logger.LogInformation("Activity {Index} started at {Elapsed}ms",
                    index, (DateTime.UtcNow - startTime).TotalMilliseconds);
            }

            // Simulate varying completion times
            var delay = index switch
            {
                0 => TimeSpan.FromMilliseconds(100),
                1 => TimeSpan.FromMilliseconds(200),
                2 => TimeSpan.FromMilliseconds(500),
                3 => TimeSpan.FromSeconds(1),
                _ => TimeSpan.FromSeconds(2)
            };

            await Task.Delay(delay, ctx.CancellationToken);

            lock (taskCompleted)
            {
                taskCompleted.Add(index);
                logger.LogInformation("Activity {Index} completed at {Elapsed}ms after {Delay}ms",
                    index, (DateTime.UtcNow - startTime).TotalMilliseconds, delay.TotalMilliseconds);
            }
        });

        var methodCompleteTime = DateTime.UtcNow;
        logger.LogInformation("ExecuteParallelActivities returned after {Elapsed}ms",
            (methodCompleteTime - startTime).TotalMilliseconds);

        // Wait a bit more to see if anything happens after the method returns
        await Task.Delay(3000);

        // Log final state
        lock (taskStarted)
        {
            logger.LogInformation("Activities started: {Count} - {Tasks}",
                taskStarted.Count, string.Join(", ", taskStarted.OrderBy(x => x)));
        }

        lock (taskCompleted)
        {
            logger.LogInformation("Activities completed: {Count} - {Tasks}",
                taskCompleted.Count, string.Join(", ", taskCompleted.OrderBy(x => x)));
        }

        // Assert
        Assert.Equal(5, taskStarted.Count);
        Assert.Equal(5, taskCompleted.Count);

        // The key assertion - did the method return before all tasks completed?
        var methodCompletionDelta = methodCompleteTime - startTime;
        var expectedCompletionTime = TimeSpan.FromSeconds(2); // Longest task

        logger.LogInformation("Method completion time: {Time}ms, Expected minimum: {Expected}ms",
            methodCompletionDelta.TotalMilliseconds, expectedCompletionTime.TotalMilliseconds);

        // If the method returns in significantly less time than the longest activity,
        // we have evidence that ExecuteParallelActivities doesn't wait for all activities to finish
        Assert.True(methodCompletionDelta >= expectedCompletionTime,
            $"ExecuteParallelActivities returned in {methodCompletionDelta.TotalMilliseconds}ms, " +
            $"which is less than the longest activity duration of {expectedCompletionTime.TotalMilliseconds}ms");
    }

    // Test implementation of BlockBase
    private class TestBlockBase
    {
        private readonly ILogger _logger;
        public string Name { get; }

        public TestBlockBase(ILogger logger, string name)
        {
            _logger = logger;
            Name = name;
        }

        public async Task TestExecuteParallelActivities(
            IDataFlowContext context,
            int howMany,
            Func<int, IDataFlowContext, Task> activity)
        {
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = 3,
                CancellationToken = context.CancellationToken
            };

            _logger.LogInformation("TestExecuteParallelActivities beginning with {Count} activities", howMany);

            try
            {
                await Parallel.ForEachAsync(
                    Enumerable.Range(0, howMany),
                    parallelOptions,
                    async (index, ct) =>
                    {
                        try
                        {
                            _logger.LogDebug("Starting parallel activity {activityId} in block {blockName}",
                                index, Name);

                            await using var scope = context.ServiceProvider.CreateAsyncScope();
                            var branchContext = new DataFlowContext()
                            {
                                CancellationToken = context.CancellationToken,
                                ServiceProvider = scope.ServiceProvider
                            };

                            await activity(index, branchContext);
                        }
                        finally
                        {
                            _logger.LogDebug("Completed parallel activity {activityId} in block {blockName}",
                                index, Name);
                        }
                    });

                _logger.LogInformation("Parallel.ForEachAsync within TestExecuteParallelActivities has returned");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in TestExecuteParallelActivities");
                throw;
            }
        }
    }
}
