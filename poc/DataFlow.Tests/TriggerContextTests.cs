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
    [Theory]
    [InlineData("tenant-123", "param1", "value1", "tenant-123", "value1")]
    [InlineData("tenant-456", "deliveryCount", 2, "tenant-456", "2")]
    [InlineData("tenant-789", "userId", "user-123", "tenant-789", "user-123")]
    public async Task TriggerContext_WithJsonData_ParametersAccessibleInActor(
        string tenantId, 
        string paramName, 
        object paramValue,
        string expectedTenant,
        string expectedParamValue)
    {
        // Arrange
        var services = new ServiceCollection()
            .AddSingleton<GenericActor>()
            .BuildServiceProvider();

        var triggerContext = new JsonTriggerContext
        {
            Data = new JsonObject
            {
                ["tenantId"] = tenantId,
                [paramName] = JsonValue.Create(paramValue)
            }
        };

        var actor = services.GetRequiredService<GenericActor>();

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
        Assert.All(results, r => Assert.Contains(expectedTenant, r));
        Assert.All(results, r => Assert.Contains(expectedParamValue, r));
    }

    [Fact]
    public async Task TriggerContext_Null_ActorHandlesGracefully()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddSingleton<GenericActor>()
            .BuildServiceProvider();

        var actor = services.GetRequiredService<GenericActor>();

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

    private class GenericActor : IStreamActor<int, string>
    {
        public async IAsyncEnumerable<string> RunAsync(
            IAsyncEnumerable<int> input,
            IActorExecutionContext context)
        {
            // Use Parameter Provider to extract parameters
            var tenantId = context.Parameters.GetParameter("tenantId", "default");
            
            // Get all parameters by trying common ones that tests might provide
            var param1 = context.Parameters.GetParameter<string>("param1", "");
            var deliveryCount = context.Parameters.GetParameter<int>("deliveryCount", 0);
            var userId = context.Parameters.GetParameter<string>("userId", "");
            var jobName = context.Parameters.GetParameter<string>("jobName", "");
            var priority = context.Parameters.GetParameter<string>("priority", "");
            
            // Build parameter summary including only non-default values
            var parts = new List<string> { $"tenantId:{tenantId}" };
            if (!string.IsNullOrEmpty(param1)) parts.Add($"param1:{param1}");
            if (deliveryCount > 0) parts.Add($"deliveryCount:{deliveryCount}");
            if (!string.IsNullOrEmpty(userId)) parts.Add($"userId:{userId}");
            if (!string.IsNullOrEmpty(jobName)) parts.Add($"jobName:{jobName}");
            if (!string.IsNullOrEmpty(priority)) parts.Add($"priority:{priority}");
            
            var paramsSummary = string.Join(",", parts);

            await foreach (var item in input)
            {
                yield return $"tenant:{tenantId}:params:{paramsSummary}:item:{item}";
            }
        }
    }

    #endregion
}
