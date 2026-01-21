namespace DataFlow.POC.Tests;

using System.Linq;
using System.Text.Json.Nodes;
using DataFlow.POC.Core;
using DataFlow.POC.Tests.TestHelpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// Tests for trigger context passing functionality.
/// Validates that trigger context is properly propagated through the execution pipeline.
/// </summary>
public class TriggerContextTests
{
    [Fact]
    public async Task TriggerContext_ScheduledJob_AvailableInActor()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddSingleton<ScheduledJobActor>()
            .BuildServiceProvider();

        var triggerContext = new ScheduledTriggerContext
        {
            JobName = "DailyReport",
            TenantId = "tenant-123",
            ScheduledTime = DateTime.UtcNow
        };

        var executionContext = TestContext.CreateExecution(
            services,
            CancellationToken.None,
            triggerContext);

        var actor = services.GetRequiredService<ScheduledJobActor>();

        // Act
        var results = new List<string>();
        await foreach (var result in actor.RunAsync(
            TestStreams.FromArray(1, 2, 3),
            TestContext.CreateActor(triggerContext: triggerContext)))
        {
            results.Add(result);
        }

        // Assert
        Assert.Equal(3, results.Count);
        Assert.All(results, r => Assert.StartsWith("tenant-123", r));
        Assert.All(results, r => Assert.Contains("DailyReport", r));
    }

    [Fact]
    public async Task TriggerContext_MessageQueue_RetryLogicWorks()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddSingleton<MessageQueueActor>()
            .BuildServiceProvider();

        var triggerContext = new MessageQueueTriggerContext
        {
            QueueName = "test-queue",
            MessageId = "msg-456",
            DeliveryCount = 2,
            MessageProperties = new Dictionary<string, string>
            {
                ["tenantId"] = "tenant-456"
            }
        };

        var executionContext = TestContext.CreateExecution(
            services,
            CancellationToken.None,
            triggerContext);

        var actor = services.GetRequiredService<MessageQueueActor>();

        // Act
        var results = new List<string>();
        await foreach (var result in actor.RunAsync(
            TestStreams.FromArray(1, 2),
            TestContext.CreateActor(triggerContext: triggerContext)))
        {
            results.Add(result);
        }

        // Assert
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Contains("delivery:2", r));
        Assert.All(results, r => Assert.Contains("tenant-456", r));
    }

    [Fact]
    public async Task TriggerContext_Null_ActorHandlesGracefully()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddSingleton<NullSafeActor>()
            .BuildServiceProvider();

        var executionContext = TestContext.CreateExecution(
            services,
            CancellationToken.None,
            triggerContext: null);

        var actor = services.GetRequiredService<NullSafeActor>();

        // Act
        var results = new List<string>();
        await foreach (var result in actor.RunAsync(
            TestStreams.FromArray(1, 2, 3),
            TestContext.CreateActor(triggerContext: null)))
        {
            results.Add(result);
        }

        // Assert
        Assert.Equal(3, results.Count);
        Assert.All(results, r => Assert.Contains("default", r));
    }

    [Fact]
    public async Task TriggerContext_WebRequest_PropertiesAccessible()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddSingleton<WebRequestActor>()
            .BuildServiceProvider();

        var triggerContext = new WebRequestTriggerContext
        {
            UserId = "user-123",
            TenantId = "tenant-789",
            RequestPath = "/api/reports",
            RequestMethod = "POST",
            RequestHeaders = new Dictionary<string, string>
            {
                ["X-Correlation-Id"] = "corr-123"
            }
        };

        var executionContext = TestContext.CreateExecution(
            services,
            CancellationToken.None,
            triggerContext);

        var actor = services.GetRequiredService<WebRequestActor>();

        // Act
        var results = new List<string>();
        await foreach (var result in actor.RunAsync(
            TestStreams.FromArray(1, 2),
            TestContext.CreateActor(triggerContext: triggerContext)))
        {
            results.Add(result);
        }

        // Assert
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Contains("user-123", r));
        Assert.All(results, r => Assert.Contains("tenant-789", r));
    }

    [Fact]
    public async Task TriggerContext_JsonDynamic_PropertiesAccessible()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddSingleton<JsonDynamicActor>()
            .BuildServiceProvider();

        var triggerContext = new JsonTriggerContext
        {
            Data = new JsonObject
            {
                ["tenantId"] = "tenant-999",
                ["customProperty"] = 42,
                ["metadata"] = new JsonObject
                {
                    ["source"] = "scheduler",
                    ["priority"] = "high"
                }
            }
        };

        var executionContext = TestContext.CreateExecution(
            services,
            CancellationToken.None,
            triggerContext);

        var actor = services.GetRequiredService<JsonDynamicActor>();

        // Act
        var results = new List<string>();
        await foreach (var result in actor.RunAsync(
            TestStreams.FromArray(1, 2),
            TestContext.CreateActor(triggerContext: triggerContext)))
        {
            results.Add(result);
        }

        // Assert
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Contains("tenant-999", r));
        Assert.All(results, r => Assert.Contains("customProperty:42", r));
    }

    [Fact]
    public async Task TriggerContext_JsonFromString_ParsesCorrectly()
    {
        // Arrange
        var json = @"{
            ""tenantId"": ""tenant-json-123"",
            ""customData"": ""value""
        }";

        var triggerContext = JsonTriggerContext.FromJson(json);

        // Assert
        Assert.NotNull(triggerContext.Data);
        Assert.Equal("tenant-json-123", triggerContext.Data["tenantId"]?.GetValue<string>());
        Assert.Equal("value", triggerContext.Data["customData"]?.GetValue<string>());
    }

    #region Test Actors

    private class ScheduledJobActor : IStreamActor<int, string>
    {
        public async IAsyncEnumerable<string> RunAsync(
            IAsyncEnumerable<int> input,
            IActorExecutionContext context)
        {
            string tenantId = "default";
            string jobName = "unknown";

            if (context.TriggerContext is ScheduledTriggerContext scheduled)
            {
                tenantId = scheduled.TenantId ?? "default";
                jobName = scheduled.JobName ?? "unknown";
            }

            await foreach (var item in input)
            {
                yield return $"{tenantId}:{jobName}:{item}";
            }
        }
    }

    private class MessageQueueActor : IStreamActor<int, string>
    {
        public async IAsyncEnumerable<string> RunAsync(
            IAsyncEnumerable<int> input,
            IActorExecutionContext context)
        {
            string tenantId = "default";
            int deliveryCount = 0;

            if (context.TriggerContext is MessageQueueTriggerContext queue)
            {
                tenantId = queue.MessageProperties?.GetValueOrDefault("tenantId") ?? "default";
                deliveryCount = queue.DeliveryCount;
            }

            await foreach (var item in input)
            {
                yield return $"{tenantId}:delivery:{deliveryCount}:item:{item}";
            }
        }
    }

    private class NullSafeActor : IStreamActor<int, string>
    {
        public async IAsyncEnumerable<string> RunAsync(
            IAsyncEnumerable<int> input,
            IActorExecutionContext context)
        {
            string tenantId = "default";

            // Gracefully handle null trigger context
            if (context.TriggerContext is ScheduledTriggerContext scheduled && scheduled.TenantId != null)
            {
                tenantId = scheduled.TenantId;
            }

            await foreach (var item in input)
            {
                yield return $"{tenantId}:item:{item}";
            }
        }
    }

    private class WebRequestActor : IStreamActor<int, string>
    {
        public async IAsyncEnumerable<string> RunAsync(
            IAsyncEnumerable<int> input,
            IActorExecutionContext context)
        {
            string userId = "anonymous";
            string tenantId = "default";

            if (context.TriggerContext is WebRequestTriggerContext web)
            {
                userId = web.UserId ?? "anonymous";
                tenantId = web.TenantId ?? "default";
            }

            await foreach (var item in input)
            {
                yield return $"{userId}:{tenantId}:item:{item}";
            }
        }
    }

    private class JsonDynamicActor : IStreamActor<int, string>
    {
        public async IAsyncEnumerable<string> RunAsync(
            IAsyncEnumerable<int> input,
            IActorExecutionContext context)
        {
            string tenantId = "default";
            string customProperty = "none";

            if (context.TriggerContext is JsonTriggerContext json)
            {
                tenantId = json.Data?["tenantId"]?.GetValue<string>() ?? "default";
                customProperty = json.Data?["customProperty"]?.GetValue<int>().ToString() ?? "none";
            }

            await foreach (var item in input)
            {
                yield return $"{tenantId}:customProperty:{customProperty}:item:{item}";
            }
        }
    }

    #endregion
}
