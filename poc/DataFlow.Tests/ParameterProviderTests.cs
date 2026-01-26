namespace DataFlow.POC.Tests;

using System.Text.Json.Nodes;
using DataFlow.POC.Core;
using DataFlow.POC.Tests.TestHelpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// Tests for IParameterProvider functionality (Phase 2: Parameter Provider Pattern).
/// Validates that actors can access parameters in a decoupled manner from JSON trigger contexts.
/// </summary>
public class ParameterProviderTests
{
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
        var triggerContext = new JsonTriggerContext
        {
            Data = new JsonObject
            {
                ["jobName"] = "DailyReport"
            }
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
        var triggerContext = new JsonTriggerContext
        {
            Data = new JsonObject
            {
                ["jobName"] = "DailyReport"
            }
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
    public async Task Actor_UsesParameterProvider_WithScheduledJobData()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddSingleton<DecoupledActor>()
            .BuildServiceProvider();

        var triggerContext = new JsonTriggerContext
        {
            Data = new JsonObject
            {
                ["tenantId"] = "tenant-scheduled",
                ["jobName"] = "DailyReport"
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
        Assert.All(results, r => Assert.Contains("tenant-scheduled", r));
    }

    [Fact]
    public async Task Actor_UsesParameterProvider_WithMessageQueueData()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddSingleton<DecoupledActor>()
            .BuildServiceProvider();

        var triggerContext = new JsonTriggerContext
        {
            Data = new JsonObject
            {
                ["tenantId"] = "tenant-queue",
                ["queueName"] = "orders-queue",
                ["deliveryCount"] = 1
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

    [Fact]
    public async Task Actor_UsesParameterProvider_WithWebRequestData()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddSingleton<DecoupledActor>()
            .BuildServiceProvider();

        var triggerContext = new JsonTriggerContext
        {
            Data = new JsonObject
            {
                ["tenantId"] = "tenant-web",
                ["userId"] = "user-123",
                ["requestPath"] = "/api/reports"
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
        Assert.All(results, r => Assert.Contains("tenant-web", r));
    }

    [Fact]
    public void ParameterProvider_JsonFromString_ExtractsParameters()
    {
        // Arrange
        var json = @"{
            ""tenantId"": ""tenant-json-string"",
            ""customData"": ""value""
        }";

        var triggerContext = JsonTriggerContext.FromJson(json);
        var provider = new TriggerContextParameterProvider(triggerContext);

        // Act & Assert
        Assert.True(provider.TryGetParameter<string>("tenantId", out var tenantId));
        Assert.Equal("tenant-json-string", tenantId);

        Assert.True(provider.TryGetParameter<string>("customData", out var customData));
        Assert.Equal("value", customData);
    }

    [Fact]
    public void ParameterProvider_FromObject_ExtractsParameters()
    {
        // Arrange
        var jobData = new { JobName = "DailyReport", TenantId = "tenant-obj", Priority = 5 };
        var triggerContext = JsonTriggerContext.FromObject(jobData);
        var provider = new TriggerContextParameterProvider(triggerContext);

        // Act & Assert
        Assert.True(provider.TryGetParameter<string>("JobName", out var jobName));
        Assert.Equal("DailyReport", jobName);

        Assert.True(provider.TryGetParameter<string>("TenantId", out var tenantId));
        Assert.Equal("tenant-obj", tenantId);

        Assert.True(provider.TryGetParameter<int>("Priority", out var priority));
        Assert.Equal(5, priority);
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
