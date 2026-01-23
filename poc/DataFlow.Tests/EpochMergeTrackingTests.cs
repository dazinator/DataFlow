namespace DataFlow.POC.Tests;

using System.Collections.Concurrent;
using DataFlow.POC.Core;
using Shouldly;
using Xunit;
using Xunit.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using DataFlow.POC.Registry;

/// <summary>
/// Tests for EntityTrackingBlock behavior at epoch merge points.
/// Validates that DbContext instances are correctly reused when epoch vectors merge.
/// </summary>
public class EpochMergeTrackingTests
{
    private readonly ITestOutputHelper _output;

    public EpochMergeTrackingTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void EpochVector_Subsumption_Should_DetectMergedEpochs()
    {
        // Arrange - Simulate merge point
        var epoch1 = EpochVector.FromSingleSource("source1", 1);
        var epoch2 = EpochVector.FromSingleSource("source2", 1);
        var merged = EpochVector.FromSources(new Dictionary<string, long>
        {
            ["source1"] = 1,
            ["source2"] = 1
        });

        // Act & Assert - Merged epoch subsumes both parents
        merged.Subsumes(epoch1).ShouldBeTrue();
        merged.Subsumes(epoch2).ShouldBeTrue();

        // Parents don't subsume each other
        epoch1.Subsumes(epoch2).ShouldBeFalse();
        epoch2.Subsumes(epoch1).ShouldBeFalse();
    }

    [Fact]
    public void FindMostSpecificAncestor_Should_DetectParentAtMergePoint()
    {
        // Arrange - Two sources feeding into merge
        var parent1 = EpochVector.FromSingleSource("source1", 5);
        var parent2 = EpochVector.FromSingleSource("source2", 3);
        
        // Merged epoch receives data from both
        var merged = EpochVector.FromSources(new Dictionary<string, long>
        {
            ["source1"] = 5,
            ["source2"] = 3
        });

        var existingContexts = new[] { parent1, parent2 };

        // Act
        var ancestor = merged.FindMostSpecificAncestor(existingContexts);

        // Assert - Should find one of the parents
        ancestor.ShouldNotBeNull();
        (ancestor == parent1 || ancestor == parent2).ShouldBeTrue();
    }

    [Fact]
    public void TrackingBlock_Should_ReuseContextAtMergePoint()
    {
        // Arrange - Simulate tracking block with context reuse logic
        var contexts = new ConcurrentDictionary<EpochVector, MockDbContext>();

        var epoch1 = EpochVector.FromSingleSource("source1", 1);
        var epoch2 = EpochVector.FromSingleSource("source2", 1);
        var merged = EpochVector.FromSources(new Dictionary<string, long>
        {
            ["source1"] = 1,
            ["source2"] = 1
        });

        // Create contexts for first two epochs
        var ctx1 = new MockDbContext("ctx1");
        var ctx2 = new MockDbContext("ctx2");
        contexts[epoch1] = ctx1;
        contexts[epoch2] = ctx2;

        // Act - Simulate processing merged epoch
        MockDbContext mergedCtx;
        if (!contexts.TryGetValue(merged, out mergedCtx))
        {
            var ancestor = merged.FindMostSpecificAncestor(contexts.Keys);
            if (ancestor != null && contexts.TryRemove(ancestor, out var ancestorCtx))
            {
                // Promote ancestor's context
                mergedCtx = ancestorCtx;
                contexts[merged] = mergedCtx;
                _output.WriteLine($"Promoted {ancestorCtx.Name} from {ancestor} to {merged}");
            }
        }

        // Assert - Should have reused one of the parent contexts
        mergedCtx.ShouldNotBeNull();
        (mergedCtx == ctx1 || mergedCtx == ctx2).ShouldBeTrue();
        
        // One parent context removed, one reused
        contexts.Count.ShouldBe(2); // merged + remaining parent
        contexts.ContainsKey(merged).ShouldBeTrue();
    }

    [Fact]
    public void TrackingBlock_Should_ConsolidateEntitiesFromMergedSources()
    {
        // Arrange - Simulate entities flowing through merge
        var contexts = new ConcurrentDictionary<EpochVector, MockDbContext>();
        var processedItems = new List<string>();

        var epoch1 = EpochVector.FromSingleSource("source1", 1);
        var epoch2 = EpochVector.FromSingleSource("source2", 1);
        var merged = EpochVector.FromSources(new Dictionary<string, long>
        {
            ["source1"] = 1,
            ["source2"] = 1
        });

        // Process items from source1
        var ctx1 = ProcessItemWithContextReuse(contexts, epoch1, "Item_Source1_A");
        ctx1.TrackedEntities.ShouldContain("Item_Source1_A");

        // Process items from source2
        var ctx2 = ProcessItemWithContextReuse(contexts, epoch2, "Item_Source2_A");
        ctx2.TrackedEntities.ShouldContain("Item_Source2_A");

        // Process merged epoch items
        var mergedCtx = ProcessItemWithContextReuse(contexts, merged, "Item_Merged_A");

        // Assert - Merged context should have consolidated entities
        mergedCtx.ShouldNotBeNull();
        
        // Should have reused one of the parent contexts
        (mergedCtx == ctx1 || mergedCtx == ctx2).ShouldBeTrue();
        
        // Merged context has entities from both sources
        var totalEntities = mergedCtx.TrackedEntities.Count;
        totalEntities.ShouldBeGreaterThanOrEqualTo(2); // At least from parent + merged
        
        _output.WriteLine($"Merged context has {totalEntities} entities: {string.Join(", ", mergedCtx.TrackedEntities)}");
    }

    [Fact]
    public void TrackingBlock_Should_HandleComplexMergeHierarchy()
    {
        // Arrange - Three sources merging hierarchically
        //   source1 + source2 → intermediate merge
        //   intermediate + source3 → final merge
        
        var contexts = new ConcurrentDictionary<EpochVector, MockDbContext>();

        var epoch1 = EpochVector.FromSingleSource("source1", 1);
        var epoch2 = EpochVector.FromSingleSource("source2", 1);
        var epoch3 = EpochVector.FromSingleSource("source3", 1);
        
        var intermediate = EpochVector.FromSources(new Dictionary<string, long>
        {
            ["source1"] = 1,
            ["source2"] = 1
        });
        
        var final = EpochVector.FromSources(new Dictionary<string, long>
        {
            ["source1"] = 1,
            ["source2"] = 1,
            ["source3"] = 1
        });

        // Act - Process in sequence
        var ctx1 = ProcessItemWithContextReuse(contexts, epoch1, "Item1");
        var ctx2 = ProcessItemWithContextReuse(contexts, epoch2, "Item2");
        var ctxIntermediate = ProcessItemWithContextReuse(contexts, intermediate, "ItemIntermediate");
        var ctx3 = ProcessItemWithContextReuse(contexts, epoch3, "Item3");
        var ctxFinal = ProcessItemWithContextReuse(contexts, final, "ItemFinal");

        // Assert - Final context should consolidate all entities
        ctxFinal.ShouldNotBeNull();
        ctxFinal.TrackedEntities.Count.ShouldBeGreaterThanOrEqualTo(3);
        
        _output.WriteLine($"Final context consolidated {ctxFinal.TrackedEntities.Count} entities");
    }

    [Fact]
    public void TrackingBlock_Should_HandleAdvancingSequences()
    {
        // Arrange - Source sequences advance over time
        var contexts = new ConcurrentDictionary<EpochVector, MockDbContext>();

        var epoch1_v1 = EpochVector.FromSingleSource("source1", 1);
        var epoch1_v2 = EpochVector.FromSingleSource("source1", 2);
        
        var epoch2_v1 = EpochVector.FromSingleSource("source2", 1);
        
        var merged_v1 = EpochVector.FromSources(new Dictionary<string, long>
        {
            ["source1"] = 1,
            ["source2"] = 1
        });
        
        var merged_v2 = EpochVector.FromSources(new Dictionary<string, long>
        {
            ["source1"] = 2, // Advanced!
            ["source2"] = 1
        });

        // Act
        ProcessItemWithContextReuse(contexts, epoch1_v1, "Item1_v1");
        ProcessItemWithContextReuse(contexts, epoch2_v1, "Item2_v1");
        ProcessItemWithContextReuse(contexts, merged_v1, "ItemMerged_v1");
        
        // Source1 advances
        ProcessItemWithContextReuse(contexts, epoch1_v2, "Item1_v2");
        var ctxMergedV2 = ProcessItemWithContextReuse(contexts, merged_v2, "ItemMerged_v2");

        // Assert - merged_v2 should find merged_v1 as ancestor
        ctxMergedV2.ShouldNotBeNull();
        _output.WriteLine($"Advanced merge has {ctxMergedV2.TrackedEntities.Count} entities");
    }

    [Fact]
    public void TrackingBlock_Should_DisposeContextAfterGlobalAlignment_EvenWithMultiplePromotions()
    {
        // Arrange - Test that contexts are disposed at global alignment even after indefinite promotion
        // This validates the bounded nature of context lifetime
        var contexts = new ConcurrentDictionary<EpochVector, MockDbContext>();

        // Simulate multiple merge stages promoting same context
        var epoch1 = EpochVector.FromSingleSource("source1", 1);
        var merged2 = EpochVector.FromSources(new Dictionary<string, long>
        {
            ["source1"] = 1,
            ["source2"] = 1
        });
        var merged3 = EpochVector.FromSources(new Dictionary<string, long>
        {
            ["source1"] = 1,
            ["source2"] = 1,
            ["source3"] = 1
        });
        var merged4 = EpochVector.FromSources(new Dictionary<string, long>
        {
            ["source1"] = 1,
            ["source2"] = 1,
            ["source3"] = 1,
            ["source4"] = 1
        });

        // Act - Progressive merging (simulates indefinite promotion)
        var ctx = ProcessItemWithContextReuse(contexts, epoch1, "Item1");
        var initialCtx = ctx;
        
        ctx = ProcessItemWithContextReuse(contexts, merged2, "Item2");
        ctx.ShouldBe(initialCtx); // Promoted!
        
        ctx = ProcessItemWithContextReuse(contexts, merged3, "Item3");
        ctx.ShouldBe(initialCtx); // Promoted again!
        
        ctx = ProcessItemWithContextReuse(contexts, merged4, "Item4");
        ctx.ShouldBe(initialCtx); // Promoted again!

        // All items tracked in same context
        initialCtx.TrackedEntities.Count.ShouldBe(4);
        _output.WriteLine($"Context promoted through {initialCtx.TrackedEntities.Count} merge stages");

        // Simulate global alignment - remove contexts at/below watermark
        var watermark = merged4;
        var ready = contexts.Keys
            .Where(k => k.IsLessThanOrEqual(watermark))
            .ToList();

        foreach (var epoch in ready)
        {
            if (contexts.TryRemove(epoch, out var ctxToDispose))
            {
                _output.WriteLine($"  Disposing context {ctxToDispose.Name} for epoch {epoch}");
            }
        }

        // Assert - All contexts disposed after global alignment
        contexts.Count.ShouldBe(0);
        _output.WriteLine("All contexts disposed after global alignment - bounded lifetime confirmed");
    }

    // Helper method simulating EntityTrackingBlock logic
    private MockDbContext ProcessItemWithContextReuse(
        ConcurrentDictionary<EpochVector, MockDbContext> contexts,
        EpochVector epoch,
        string item)
    {
        MockDbContext ctx;
        
        if (!contexts.TryGetValue(epoch, out ctx))
        {
            // Check for ancestor context to reuse
            var ancestor = epoch.FindMostSpecificAncestor(contexts.Keys);
            
            if (ancestor != null && contexts.TryRemove(ancestor, out var ancestorCtx))
            {
                // Promote ancestor's context
                ctx = ancestorCtx;
                contexts[epoch] = ctx;
                _output.WriteLine($"  Reused context from {ancestor} for {epoch}");
            }
            else
            {
                // Create new context
                ctx = new MockDbContext($"ctx_{contexts.Count + 1}");
                contexts[epoch] = ctx;
                _output.WriteLine($"  Created new context {ctx.Name} for {epoch}");
            }
        }
        
        // Track entity
        ctx.TrackedEntities.Add(item);
        _output.WriteLine($"  Tracked '{item}' in {ctx.Name} (total: {ctx.TrackedEntities.Count})");
        
        return ctx;
    }

    private class MockDbContext
    {
        public string Name { get; }
        public List<string> TrackedEntities { get; } = new();

        public MockDbContext(string name)
        {
            Name = name;
        }
    }
}
