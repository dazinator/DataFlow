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
/// All tests use JsonTriggerContext as it's the only built-in trigger context type.
/// </summary>
public class TriggerContextTests
{
    [Fact]
    public async Task TriggerContext_ScheduledJobData_AvailableInActor()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddSingleton<ScheduledJobActor>()
            .BuildServiceProvider();

        var triggerContext = new JsonTriggerContext
        {
            Data = new JsonObject
            {
                ["jobName"] = "DailyReport",
                ["tenantId"] = "tenant-123",
                ["scheduledTime"] = JsonValue.Create(DateTime.UtcNow)
            }
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
    public async Task TriggerContext_MessageQueueData_RetryLogicWorks()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddSingleton<MessageQueueActor>()
            .BuildServiceProvider();

        var triggerContext = new JsonTriggerContext
        {
            Data = new JsonObject
            {
                ["queueName"] = "test-queue",
                ["messageId"] = "msg-456",
                ["deliveryCount"] = 2,
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
    public async Task TriggerContext_WebRequestData_PropertiesAccessible()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddSingleton<WebRequestActor>()
            .BuildServiceProvider();

        var triggerContext = new JsonTriggerContext
        {
            Data = new JsonObject
            {
                ["userId"] = "user-123",
                ["tenantId"] = "tenant-789",
                ["requestPath"] = "/api/reports",
                ["requestMethod"] = "POST"
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
            // Use Parameter Provider (recommended approach)
            var tenantId = context.Parameters.GetParameter("tenantId", "default");
            var jobName = context.Parameters.GetParameter("jobName", "unknown");

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
            // Use Parameter Provider (recommended approach)
            var tenantId = context.Parameters.GetParameter("tenantId", "default");
            var deliveryCount = context.Parameters.GetParameter("deliveryCount", 0);

            await foreach (var item in input)
            {
                yield return $"tenant:{tenantId}:delivery:{deliveryCount}:item:{item}";
            }
        }
    }

    private class NullSafeActor : IStreamActor<int, string>
    {
        public async IAsyncEnumerable<string> RunAsync(
            IAsyncEnumerable<int> input,
            IActorExecutionContext context)
        {
            // Null trigger context handled gracefully
            var tenantId = context.Parameters.GetParameter("tenantId", "default");

            await foreach (var item in input)
            {
                yield return $"tenant:{tenantId}:item:{item}";
            }
        }
    }

    private class WebRequestActor : IStreamActor<int, string>
    {
        public async IAsyncEnumerable<string> RunAsync(
            IAsyncEnumerable<int> input,
            IActorExecutionContext context)
        {
            // Use Parameter Provider (recommended approach)
            var userId = context.Parameters.GetParameter("userId", "unknown");
            var tenantId = context.Parameters.GetParameter("tenantId", "default");

            await foreach (var item in input)
            {
                yield return $"user:{userId}:tenant:{tenantId}:item:{item}";
            }
        }
    }

    private class JsonDynamicActor : IStreamActor<int, string>
    {
        public async IAsyncEnumerable<string> RunAsync(
            IAsyncEnumerable<int> input,
            IActorExecutionContext context)
        {
            // Use Parameter Provider (recommended approach)
            var tenantId = context.Parameters.GetParameter("tenantId", "default");
            var customProperty = context.Parameters.GetParameter("customProperty", 0);

            await foreach (var item in input)
            {
                yield return $"tenant:{tenantId}:customProperty:{customProperty}:item:{item}";
            }
        }
    }

    #endregion
}
