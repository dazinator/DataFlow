// =============================================================================
// Research Prototype: DataFrame Block Tests
// =============================================================================
// NOTE: This is REFERENCE CODE only. It lives in /research/ and is NOT part
// of the compiled solution. It documents what the tests should look like.
//
// Test framework: xUnit + Shouldly (matching existing test patterns)
// =============================================================================

using System.Threading.Channels;
using Microsoft.Data.Analysis;
using Shouldly;
using Xunit;
using DataFlow.POC.Core;
using DataFlow.Analytics.Blocks.Transform;
using DataFlow.Analytics.Blocks.Sink;
using DataFlow.Analytics.Abstractions;

namespace DataFlow.Analytics.Tests;

// ---------------------------------------------------------------------------
// Test helpers
// ---------------------------------------------------------------------------

internal static class TestDataFrames
{
    /// <summary>Creates a test DataFrame with Amount and Category columns.</summary>
    public static DataFrame CreateSalesFrame(int rows = 10)
    {
        var amounts = new PrimitiveDataFrameColumn<int>("Amount",
            Enumerable.Range(1, rows).Select(i => i * 10));
        var categories = new StringDataFrameColumn("Category",
            Enumerable.Range(0, rows).Select(i => i % 2 == 0 ? "A" : "B"));
        var ids = new PrimitiveDataFrameColumn<int>("RowId",
            Enumerable.Range(1, rows));
        return new DataFrame(ids, amounts, categories);
    }

    public static async IAsyncEnumerable<T> FromItems<T>(params T[] items)
    {
        foreach (var item in items)
            yield return item;
        await Task.CompletedTask;
    }
}

// Simple IParquetSinkRepository using a MemoryStream for testing
internal sealed class InMemoryParquetSinkRepository : IParquetSinkRepository
{
    private readonly Dictionary<string, MemoryStream> _streams = new();

    public Task<Stream> OpenWriteAsync(string sinkKey, CancellationToken cancellationToken = default)
    {
        var ms = new MemoryStream();
        _streams[sinkKey] = ms;
        return Task.FromResult<Stream>(ms);
    }

    public Task CommitAsync(string sinkKey, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public byte[] GetBytes(string sinkKey) => _streams[sinkKey].ToArray();
}

// Simple execution context for testing
internal static class TestExecutionContext
{
    public static IExecutionContext Create() => new ExecutionContext(
        (IServiceScopeFactory?)null,
        CancellationToken.None,
        Guid.NewGuid(),
        null, null, null);
}

// ---------------------------------------------------------------------------
// DataFrameFilterBlock tests
// ---------------------------------------------------------------------------

public class DataFrameFilterBlockTests
{
    [Fact]
    public async Task FilterBlock_FiltersRowsByPredicate()
    {
        // Arrange
        var frame = TestDataFrames.CreateSalesFrame(rows: 10);  // Amounts: 10, 20, ..., 100
        var block = new DataFrameFilterBlock(
            new BlockContext("filter"),
            df => df["Amount"].ElementwiseGreaterThan(50));

        var context = TestExecutionContext.Create();

        // Act
        var results = new List<DataFrame>();
        await foreach (var result in block.ExecuteAsync(
            TestDataFrames.FromItems(frame), context))
            results.Add(result);

        // Assert
        results.Count.ShouldBe(1);
        results[0].Rows.Count.ShouldBe(5);   // Amounts 60, 70, 80, 90, 100
    }

    [Fact]
    public async Task FilterBlock_DoesNotMutateInput()
    {
        // Arrange
        var frame = TestDataFrames.CreateSalesFrame(rows: 10);
        var originalRowCount = frame.Rows.Count;
        var block = new DataFrameFilterBlock(
            new BlockContext("filter"),
            df => df["Amount"].ElementwiseGreaterThan(50));

        var context = TestExecutionContext.Create();

        // Act
        var results = new List<DataFrame>();
        await foreach (var result in block.ExecuteAsync(
            TestDataFrames.FromItems(frame), context))
            results.Add(result);

        // Assert: original frame unchanged
        frame.Rows.Count.ShouldBe(originalRowCount);
        results[0].Rows.Count.ShouldBeLessThan(originalRowCount);
    }

    [Fact]
    public async Task FilterBlock_YieldsEmptyFrame_WhenNoRowsMatch()
    {
        // Arrange
        var frame = TestDataFrames.CreateSalesFrame(rows: 5);  // Amounts: 10, 20, 30, 40, 50
        var block = new DataFrameFilterBlock(
            new BlockContext("filter"),
            df => df["Amount"].ElementwiseGreaterThan(999));

        var context = TestExecutionContext.Create();

        // Act
        var results = new List<DataFrame>();
        await foreach (var result in block.ExecuteAsync(
            TestDataFrames.FromItems(frame), context))
            results.Add(result);

        // Assert
        results.Count.ShouldBe(1);
        results[0].Rows.Count.ShouldBe(0);
    }
}

// ---------------------------------------------------------------------------
// DataFrameSelectBlock tests
// ---------------------------------------------------------------------------

public class DataFrameSelectBlockTests
{
    [Fact]
    public async Task SelectBlock_ReturnsOnlySelectedColumns()
    {
        // Arrange
        var frame = TestDataFrames.CreateSalesFrame(rows: 5);
        var block = new DataFrameSelectBlock(new BlockContext("select"), "Amount", "Category");

        var context = TestExecutionContext.Create();

        // Act
        var results = new List<DataFrame>();
        await foreach (var result in block.ExecuteAsync(
            TestDataFrames.FromItems(frame), context))
            results.Add(result);

        // Assert
        results.Count.ShouldBe(1);
        results[0].Columns.Count.ShouldBe(2);
        results[0]["Amount"].ShouldNotBeNull();
        results[0]["Category"].ShouldNotBeNull();
        results[0].Columns.Any(c => c.Name == "RowId").ShouldBeFalse();
    }

    [Fact]
    public async Task SelectBlock_PreservesRowCount()
    {
        // Arrange
        var frame = TestDataFrames.CreateSalesFrame(rows: 7);
        var block = new DataFrameSelectBlock(new BlockContext("select"), "Amount");

        var context = TestExecutionContext.Create();

        // Act
        var results = new List<DataFrame>();
        await foreach (var result in block.ExecuteAsync(
            TestDataFrames.FromItems(frame), context))
            results.Add(result);

        // Assert
        results[0].Rows.Count.ShouldBe(7);
    }
}

// ---------------------------------------------------------------------------
// DataFramePartitionBlock tests
// ---------------------------------------------------------------------------

public class DataFramePartitionBlockTests
{
    [Fact]
    public async Task PartitionBlock_YieldsCorrectNumberOfPartitions()
    {
        // Arrange
        var frame = TestDataFrames.CreateSalesFrame(rows: 12);
        var block = new DataFramePartitionBlock(new BlockContext("partition"), partitions: 4);

        var context = TestExecutionContext.Create();

        // Act
        var results = new List<DataFrame>();
        await foreach (var result in block.ExecuteAsync(
            TestDataFrames.FromItems(frame), context))
            results.Add(result);

        // Assert
        results.Count.ShouldBe(4);
    }

    [Fact]
    public async Task PartitionBlock_TotalRowCountPreserved()
    {
        // Arrange
        var frame = TestDataFrames.CreateSalesFrame(rows: 10);
        var block = new DataFramePartitionBlock(new BlockContext("partition"), partitions: 3);

        var context = TestExecutionContext.Create();

        // Act
        var results = new List<DataFrame>();
        await foreach (var result in block.ExecuteAsync(
            TestDataFrames.FromItems(frame), context))
            results.Add(result);

        // Assert: all rows are present across all partitions
        results.Sum(r => (int)r.Rows.Count).ShouldBe(10);
    }

    [Fact]
    public async Task PartitionBlock_PartitionsAreNonOverlapping()
    {
        // Arrange
        var frame = TestDataFrames.CreateSalesFrame(rows: 9);
        var block = new DataFramePartitionBlock(new BlockContext("partition"), partitions: 3);

        var context = TestExecutionContext.Create();

        // Act
        var results = new List<DataFrame>();
        await foreach (var result in block.ExecuteAsync(
            TestDataFrames.FromItems(frame), context))
            results.Add(result);

        // Assert: all row IDs appear exactly once across all partitions
        var allRowIds = results
            .SelectMany(r => Enumerable.Range(0, (int)r.Rows.Count)
                .Select(i => (int?)r["RowId"][i]))
            .ToList();

        allRowIds.Distinct().Count().ShouldBe(allRowIds.Count);
        allRowIds.Count.ShouldBe(9);
    }
}

// ---------------------------------------------------------------------------
// DataFrameAccumulatorBlock tests
// ---------------------------------------------------------------------------

public class DataFrameAccumulatorBlockTests
{
    [Fact]
    public async Task AccumulatorBlock_YieldsWhenTargetRowCountReached()
    {
        // Arrange: send 3 frames of 4 rows each (12 total), target = 8
        var frames = Enumerable.Range(0, 3).Select(_ => TestDataFrames.CreateSalesFrame(4)).ToArray();
        var block = new DataFrameAccumulatorBlock(new BlockContext("accumulator"), targetRowCount: 8);

        var context = TestExecutionContext.Create();

        // Act
        var results = new List<DataFrame>();
        await foreach (var result in block.ExecuteAsync(
            TestDataFrames.FromItems(frames), context))
            results.Add(result);

        // Assert: one yield when 8 rows accumulated, remainder in second yield
        results.Count.ShouldBe(2);
        results[0].Rows.Count.ShouldBe(8);
        results[1].Rows.Count.ShouldBe(4);
    }

    [Fact]
    public async Task AccumulatorBlock_YieldsRemainder_WhenStreamEndsBeforeTarget()
    {
        // Arrange: 5 rows, target = 100
        var frame = TestDataFrames.CreateSalesFrame(rows: 5);
        var block = new DataFrameAccumulatorBlock(new BlockContext("accumulator"), targetRowCount: 100);

        var context = TestExecutionContext.Create();

        // Act
        var results = new List<DataFrame>();
        await foreach (var result in block.ExecuteAsync(
            TestDataFrames.FromItems(frame), context))
            results.Add(result);

        // Assert: yields remaining 5 rows at end of stream
        results.Count.ShouldBe(1);
        results[0].Rows.Count.ShouldBe(5);
    }
}

// ---------------------------------------------------------------------------
// DataFrameDeduplicateBlock tests
// ---------------------------------------------------------------------------

public class DataFrameDeduplicateBlockTests
{
    [Fact]
    public async Task DeduplicateBlock_RemovesDuplicateRows()
    {
        // Arrange: DataFrame with duplicate RowIds (simulating broadcast + fan-in)
        var amounts = new PrimitiveDataFrameColumn<int>("Amount", new[] { 10, 20, 10 });
        var ids = new PrimitiveDataFrameColumn<int>("RowId", new[] { 1, 2, 1 }); // row 1 duplicated
        var frame = new DataFrame(ids, amounts);

        var block = new DataFrameDeduplicateBlock(new BlockContext("dedup"), "RowId");
        var context = TestExecutionContext.Create();

        // Act
        var results = new List<DataFrame>();
        await foreach (var result in block.ExecuteAsync(
            TestDataFrames.FromItems(frame), context))
            results.Add(result);

        // Assert
        results[0].Rows.Count.ShouldBe(2);   // RowId 1 and 2 only (first occurrence kept)
    }

    [Fact]
    public async Task DeduplicateBlock_NoDuplicatesPassesThrough()
    {
        // Arrange: no duplicates
        var frame = TestDataFrames.CreateSalesFrame(rows: 5);
        var block = new DataFrameDeduplicateBlock(new BlockContext("dedup"), "RowId");
        var context = TestExecutionContext.Create();

        // Act
        var results = new List<DataFrame>();
        await foreach (var result in block.ExecuteAsync(
            TestDataFrames.FromItems(frame), context))
            results.Add(result);

        // Assert: all rows preserved
        results[0].Rows.Count.ShouldBe(5);
    }
}

// ---------------------------------------------------------------------------
// DataFrameGroupByBlock tests
// ---------------------------------------------------------------------------

public class DataFrameGroupByBlockTests
{
    [Fact]
    public async Task GroupByBlock_AggregatesCorrectly()
    {
        // Arrange: 6 rows, Category A or B alternating, Amount 10..60
        var frame = TestDataFrames.CreateSalesFrame(rows: 6);
        var block = new DataFrameGroupByBlock(
            new BlockContext("groupby"),
            keyColumn: "Category",
            aggregate: g => g.Sum("Amount"));

        var context = TestExecutionContext.Create();

        // Act
        var results = new List<DataFrame>();
        await foreach (var result in block.ExecuteAsync(
            TestDataFrames.FromItems(frame), context))
            results.Add(result);

        // Assert: two groups (A and B)
        results.Count.ShouldBe(1);
        results[0].Rows.Count.ShouldBe(2);  // Category A and B
    }
}

// ---------------------------------------------------------------------------
// Broadcast clone safety test
// ---------------------------------------------------------------------------

public class BroadcastCloneSafetyTests
{
    [Fact]
    public void DataFrameClone_IsDeepCopy()
    {
        // Demonstrates that Clone() produces independent copies (mutation isolation)
        var original = TestDataFrames.CreateSalesFrame(rows: 5);
        var clone = original.Clone();

        // Mutate the clone
        clone["Amount"][0] = 9999;

        // Original must be unchanged
        ((int?)original["Amount"][0]).ShouldBe(10);    // first Amount in test data is 10
        ((int?)clone["Amount"][0]).ShouldBe(9999);
    }

    [Fact]
    public void DataFrameClone_HasSameShape()
    {
        var original = TestDataFrames.CreateSalesFrame(rows: 5);
        var clone = original.Clone();

        clone.Rows.Count.ShouldBe(original.Rows.Count);
        clone.Columns.Count.ShouldBe(original.Columns.Count);
    }
}
