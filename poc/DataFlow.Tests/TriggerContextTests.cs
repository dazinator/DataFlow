namespace DataFlow.POC.Tests;

using DataFlow.POC.Core;
using DataFlow.POC.Blocks;
using DataFlow.POC.Tests.TestHelpers;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.CompilerServices;
using Xunit;

/// <summary>
/// Tests for trigger context passing through dataflow execution.
/// Validates that trigger context is available to actors and propagates correctly.
/// </summary>
public class TriggerContextTests
{
    /// <summary>
    /// Actor that accesses trigger context to add tenant ID to processed items.
    /// </summary>
    private class TenantAwareActor : IStreamActor<int, string>
    {
        public async IAsyncEnumerable<string> RunAsync(
            IAsyncEnumerable<int> input,
            IActorExecutionContext context)
        {
            var scheduledContext = context.TriggerContext as ScheduledTriggerContext;
            var tenantId = scheduledContext?.TenantId ?? "unknown";

            await foreach (var item in input)
            {
                yield return $"Tenant:{tenantId}:Item:{item}";
            }
        }
    }

    /// <summary>
    /// Actor that uses message queue trigger context for retry logic.
    /// </summary>
    private class RetryAwareActor : IStreamActor<int, string>
    {
        public async IAsyncEnumerable<string> RunAsync(
            IAsyncEnumerable<int> input,
            IActorExecutionContext context)
        {
            var queueContext = context.TriggerContext as MessageQueueTriggerContext;
            var deliveryCount = queueContext?.DeliveryCount ?? 0;
            var isRetry = deliveryCount > 1;

            await foreach (var item in input)
            {
                yield return isRetry 
                    ? $"RETRY({deliveryCount}):{item}"
                    : $"FIRST:{item}";
            }
        }
    }

    [Fact]
    public async Task TriggerContext_ScheduledJob_AvailableInActor()
    {
        // Arrange
        var triggerContext = new ScheduledTriggerContext
        {
            JobName = "DailyReport",
            TenantId = "tenant-123",
            ScheduledTime = DateTime.UtcNow
        };

        var services = new ServiceCollection().BuildServiceProvider();
        var context = new ExecutionContext(
            services,
            CancellationToken.None,
            Guid.NewGuid(),
            recoveryCheckpoint: null,
            metrics: null,
            triggerContext);

        var actorContext = new ActorExecutionContext();
        actorContext.Reset(
            context.CancellationToken,
            context.InvocationId,
            () => { },
            epochCoordinator: null,
            context.TriggerContext);

        var actor = new TenantAwareActor();
        var input = CreateAsyncEnumerable(1, 2, 3);

        // Act
        var results = await TestStreams.CollectAsync(actor.RunAsync(input, actorContext));

        // Assert
        Assert.Equal(3, results.Count);
        Assert.All(results, r => Assert.StartsWith("Tenant:tenant-123:", r));
        Assert.Contains("Tenant:tenant-123:Item:1", results);
        Assert.Contains("Tenant:tenant-123:Item:2", results);
        Assert.Contains("Tenant:tenant-123:Item:3", results);
    }

    [Fact]
    public async Task TriggerContext_MessageQueue_RetryLogicWorks()
    {
        // Arrange - First attempt
        var triggerContext = new MessageQueueTriggerContext
        {
            MessageId = "msg-123",
            QueueName = "orders-queue",
            DeliveryCount = 1
        };

        var services = new ServiceCollection().BuildServiceProvider();
        var context = new ExecutionContext(
            services,
            CancellationToken.None,
            Guid.NewGuid(),
            recoveryCheckpoint: null,
            metrics: null,
            triggerContext);

        var actorContext = new ActorExecutionContext();
        actorContext.Reset(
            context.CancellationToken,
            context.InvocationId,
            () => { },
            epochCoordinator: null,
            context.TriggerContext);

        var actor = new RetryAwareActor();
        var input = CreateAsyncEnumerable(1, 2);

        // Act
        var results = await TestStreams.CollectAsync(actor.RunAsync(input, actorContext));

        // Assert - First attempt
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.StartsWith("FIRST:", r));

        // Arrange - Retry (delivery count = 2)
        var retryTriggerContext = new MessageQueueTriggerContext
        {
            MessageId = "msg-123",
            QueueName = "orders-queue",
            DeliveryCount = 2
        };

        var retryContext = new ExecutionContext(
            services,
            CancellationToken.None,
            Guid.NewGuid(),
            recoveryCheckpoint: null,
            metrics: null,
            retryTriggerContext);

        actorContext.Reset(
            retryContext.CancellationToken,
            retryContext.InvocationId,
            () => { },
            epochCoordinator: null,
            retryContext.TriggerContext);

        // Act - Retry
        var retryResults = await TestStreams.CollectAsync(actor.RunAsync(input, actorContext));

        // Assert - Retry
        Assert.Equal(2, retryResults.Count);
        Assert.All(retryResults, r => Assert.StartsWith("RETRY(2):", r));
    }

    [Fact]
    public async Task TriggerContext_Null_ActorHandlesGracefully()
    {
        // Arrange - No trigger context
        var services = new ServiceCollection().BuildServiceProvider();
        var context = new ExecutionContext(
            services,
            CancellationToken.None);

        var actorContext = new ActorExecutionContext();
        actorContext.Reset(
            context.CancellationToken,
            context.InvocationId,
            () => { },
            epochCoordinator: null,
            context.TriggerContext);

        var actor = new TenantAwareActor();
        var input = CreateAsyncEnumerable(1, 2);

        // Act
        var results = await TestStreams.CollectAsync(actor.RunAsync(input, actorContext));

        // Assert - Falls back to "unknown"
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.StartsWith("Tenant:unknown:", r));
    }

    [Fact]
    public async Task TriggerContext_WebRequest_PropertiesAccessible()
    {
        // Arrange
        var triggerContext = new WebRequestTriggerContext
        {
            RequestId = "req-123",
            Path = "/api/data",
            Method = "POST",
            UserId = "user-456",
            Headers = new Dictionary<string, string>
            {
                ["X-Correlation-Id"] = "corr-789"
            }
        };

        var services = new ServiceCollection().BuildServiceProvider();
        var context = new ExecutionContext(
            services,
            CancellationToken.None,
            Guid.NewGuid(),
            recoveryCheckpoint: null,
            metrics: null,
            triggerContext);

        // Assert
        Assert.NotNull(context.TriggerContext);
        var webContext = context.TriggerContext as WebRequestTriggerContext;
        Assert.NotNull(webContext);
        Assert.Equal("req-123", webContext.RequestId);
        Assert.Equal("/api/data", webContext.Path);
        Assert.Equal("POST", webContext.Method);
        Assert.Equal("user-456", webContext.UserId);
        Assert.NotNull(webContext.Headers);
        Assert.Equal("corr-789", webContext.Headers["X-Correlation-Id"]);
    }

    private static async IAsyncEnumerable<int> CreateAsyncEnumerable(params int[] values)
    {
        foreach (var value in values)
        {
            await Task.Yield();
            yield return value;
        }
    }
}
