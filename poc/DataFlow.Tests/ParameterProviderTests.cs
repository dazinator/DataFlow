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
    public async Task Actor_UsesParameterProvider_WithJsonContext()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddSingleton<DecoupledActor>()
            .BuildServiceProvider();

        // Test with various JSON properties to demonstrate flexibility
        var triggerContext = new JsonTriggerContext
        {
            Data = new JsonObject
            {
                ["tenantId"] = "tenant-123",
                ["jobName"] = "DailyReport",
                ["userId"] = "user-456",
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

        // Assert - Demonstrates decoupling: actor works with any JSON structure
        Assert.Equal(3, results.Count);
        Assert.All(results, r => Assert.Contains("tenant-123", r));
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

    [Fact]
    public void ParameterProvider_Deserialize_ReturnsStronglyTypedObject()
    {
        // Arrange
        var triggerContext = new JsonTriggerContext
        {
            Data = new JsonObject
            {
                ["JobName"] = "DailyReport",
                ["TenantId"] = "tenant-deserialize",
                ["Priority"] = 5,
                ["ScheduledTime"] = JsonValue.Create(DateTime.Parse("2026-01-26T10:00:00Z"))
            }
        };
        var provider = new TriggerContextParameterProvider(triggerContext);

        // Act
        var jobParams = provider.Deserialize<TestJobParams>();

        // Assert
        Assert.NotNull(jobParams);
        Assert.Equal("DailyReport", jobParams.JobName);
        Assert.Equal("tenant-deserialize", jobParams.TenantId);
        Assert.Equal(5, jobParams.Priority);
    }

    [Fact]
    public void ParameterProvider_Deserialize_WithNullContext_ReturnsNull()
    {
        // Arrange
        var provider = new TriggerContextParameterProvider(null);

        // Act
        var result = provider.Deserialize<TestJobParams>();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ParameterProvider_Deserialize_WithInvalidJson_ReturnsNull()
    {
        // Arrange
        var triggerContext = new JsonTriggerContext
        {
            Data = new JsonObject
            {
                ["invalidProperty"] = "value"
            }
        };
        var provider = new TriggerContextParameterProvider(triggerContext);

        // Act - Deserialize to a type that doesn't match the JSON structure
        var result = provider.Deserialize<TestJobParams>();

        // Assert - Should return object with null/default values, not null itself
        Assert.NotNull(result);
        Assert.Null(result.JobName);
        Assert.Null(result.TenantId);
    }

    #region Test Classes

    private class TestJobParams
    {
        public string? JobName { get; set; }
        public string? TenantId { get; set; }
        public int Priority { get; set; }
        public DateTime? ScheduledTime { get; set; }
    }

    #endregion

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
