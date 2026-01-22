namespace DataFlow.POC.Tests;

using System.Text.Json.Nodes;
using DataFlow.POC.Core;
using DataFlow.POC.Tests.TestHelpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// Tests for IParameterProvider functionality (Phase 2: Parameter Provider Pattern).
/// Validates that actors can access parameters in a decoupled manner without checking specific trigger context types.
/// </summary>
public class ParameterProviderTests
{
    [Fact]
    public void ParameterProvider_WithScheduledContext_ExtractsParameters()
    {
        // Arrange
        var triggerContext = new ScheduledTriggerContext
        {
            JobName = "DailyReport",
            TenantId = "tenant-123",
            ScheduledTime = DateTime.Parse("2026-01-22T10:00:00Z").ToUniversalTime(),
            Metadata = new Dictionary<string, string>
            {
                ["customParam"] = "customValue"
            }
        };

        var provider = new TriggerContextParameterProvider(triggerContext);

        // Act & Assert
        Assert.True(provider.TryGetParameter<string>("jobName", out var jobName));
        Assert.Equal("DailyReport", jobName);

        Assert.True(provider.TryGetParameter<string>("tenantId", out var tenantId));
        Assert.Equal("tenant-123", tenantId);

        Assert.True(provider.TryGetParameter<DateTime>("scheduledTime", out var scheduledTime));
        Assert.Equal(DateTime.Parse("2026-01-22T10:00:00Z").ToUniversalTime(), scheduledTime);

        Assert.True(provider.TryGetParameter<string>("customParam", out var customParam));
        Assert.Equal("customValue", customParam);
    }

    [Fact]
    public void ParameterProvider_WithMessageQueueContext_ExtractsParameters()
    {
        // Arrange
        var triggerContext = new MessageQueueTriggerContext
        {
            QueueName = "orders-queue",
            MessageId = "msg-456",
            CorrelationId = "corr-789",
            DeliveryCount = 2,
            MessageProperties = new Dictionary<string, string>
            {
                ["tenantId"] = "tenant-456",
                ["priority"] = "high"
            }
        };

        var provider = new TriggerContextParameterProvider(triggerContext);

        // Act & Assert
        Assert.True(provider.TryGetParameter<string>("queueName", out var queueName));
        Assert.Equal("orders-queue", queueName);

        Assert.True(provider.TryGetParameter<int>("deliveryCount", out var deliveryCount));
        Assert.Equal(2, deliveryCount);

        Assert.True(provider.TryGetParameter<string>("tenantId", out var tenantId));
        Assert.Equal("tenant-456", tenantId);

        Assert.True(provider.TryGetParameter<string>("priority", out var priority));
        Assert.Equal("high", priority);
    }

    [Fact]
    public void ParameterProvider_WithWebRequestContext_ExtractsParameters()
    {
        // Arrange
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

        var provider = new TriggerContextParameterProvider(triggerContext);

        // Act & Assert
        Assert.True(provider.TryGetParameter<string>("userId", out var userId));
        Assert.Equal("user-123", userId);

        Assert.True(provider.TryGetParameter<string>("tenantId", out var tenantId));
        Assert.Equal("tenant-789", tenantId);

        Assert.True(provider.TryGetParameter<string>("X-Correlation-Id", out var corrId));
        Assert.Equal("corr-123", corrId);
    }

    [Fact]
    public void ParameterProvider_WithJsonContext_ExtractsParameters()
    {
        // Arrange
        var triggerContext = new JsonTriggerContext
        {
            Data = new JsonObject
            {
                ["tenantId"] = "tenant-999",
                ["reportDate"] = JsonValue.Create(DateTime.Parse("2026-01-22")),
                ["pageSize"] = 50,
                ["enabled"] = true,
                ["metadata"] = new JsonObject
                {
                    ["source"] = "scheduler"
                }
            }
        };

        var provider = new TriggerContextParameterProvider(triggerContext);

        // Act & Assert
        Assert.True(provider.TryGetParameter<string>("tenantId", out var tenantId));
        Assert.Equal("tenant-999", tenantId);

        Assert.True(provider.TryGetParameter<int>("pageSize", out var pageSize));
        Assert.Equal(50, pageSize);

        Assert.True(provider.TryGetParameter<bool>("enabled", out var enabled));
        Assert.True(enabled);
    }

    [Fact]
    public void ParameterProvider_GetRequiredParameter_ThrowsWhenMissing()
    {
        // Arrange
        var triggerContext = new ScheduledTriggerContext
        {
            JobName = "DailyReport"
        };

        var provider = new TriggerContextParameterProvider(triggerContext);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            provider.GetRequiredParameter<string>("missingParam"));

        Assert.Contains("missingParam", exception.Message);
        Assert.Contains("not found", exception.Message);
    }

    [Fact]
    public void ParameterProvider_GetParameter_ReturnsDefaultWhenMissing()
    {
        // Arrange
        var triggerContext = new ScheduledTriggerContext
        {
            JobName = "DailyReport"
        };

        var provider = new TriggerContextParameterProvider(triggerContext);

        // Act
        var result = provider.GetParameter("missingParam", "defaultValue");

        // Assert
        Assert.Equal("defaultValue", result);
    }

    [Fact]
    public void ParameterProvider_WithNullContext_ReturnsFalse()
    {
        // Arrange
        var provider = new TriggerContextParameterProvider(null);

        // Act & Assert
        Assert.False(provider.TryGetParameter<string>("anyParam", out var value));
        Assert.Null(value);
    }

    [Fact]
    public async Task Actor_UsesParameterProvider_Decoupled()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddSingleton<DecoupledActor>()
            .BuildServiceProvider();

        var triggerContext = new ScheduledTriggerContext
        {
            TenantId = "tenant-decoupled"
        };

        var context = TestContext.CreateActor(triggerContext: triggerContext);
        var actor = services.GetRequiredService<DecoupledActor>();

        // Act
        var results = new List<string>();
        await foreach (var result in actor.RunAsync(TestStreams.FromArray(1, 2, 3), context))
        {
            results.Add(result);
        }

        // Assert
        Assert.Equal(3, results.Count);
        Assert.All(results, r => Assert.Contains("tenant-decoupled", r));
    }

    [Fact]
    public async Task Actor_UsesParameterProvider_WithJsonContext()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddSingleton<DecoupledActor>()
            .BuildServiceProvider();

        var triggerContext = new JsonTriggerContext
        {
            Data = new JsonObject
            {
                ["tenantId"] = "tenant-json"
            }
        };

        var context = TestContext.CreateActor(triggerContext: triggerContext);
        var actor = services.GetRequiredService<DecoupledActor>();

        // Act
        var results = new List<string>();
        await foreach (var result in actor.RunAsync(TestStreams.FromArray(1, 2, 3), context))
        {
            results.Add(result);
        }

        // Assert
        Assert.Equal(3, results.Count);
        Assert.All(results, r => Assert.Contains("tenant-json", r));
    }

    [Fact]
    public async Task Actor_UsesParameterProvider_WithMessageQueueContext()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddSingleton<DecoupledActor>()
            .BuildServiceProvider();

        var triggerContext = new MessageQueueTriggerContext
        {
            MessageProperties = new Dictionary<string, string>
            {
                ["tenantId"] = "tenant-queue"
            }
        };

        var context = TestContext.CreateActor(triggerContext: triggerContext);
        var actor = services.GetRequiredService<DecoupledActor>();

        // Act
        var results = new List<string>();
        await foreach (var result in actor.RunAsync(TestStreams.FromArray(1, 2, 3), context))
        {
            results.Add(result);
        }

        // Assert
        Assert.Equal(3, results.Count);
        Assert.All(results, r => Assert.Contains("tenant-queue", r));
    }

    #region Test Actors

    /// <summary>
    /// Actor that uses parameter provider instead of checking specific trigger context types.
    /// This demonstrates the decoupling benefit of Phase 2.
    /// </summary>
    private class DecoupledActor : IStreamActor<int, string>
    {
        public async IAsyncEnumerable<string> RunAsync(
            IAsyncEnumerable<int> input,
            IActorExecutionContext context)
        {
            // Decoupled - works with ANY trigger context that provides "tenantId"
            var tenantId = context.Parameters.GetParameter("tenantId", "default");

            await foreach (var item in input)
            {
                yield return $"tenant:{tenantId}:item:{item}";
            }
        }
    }

    #endregion
}
