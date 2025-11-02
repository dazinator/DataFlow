namespace DataFlow.POC.Tests;

using DataFlow.POC.Core;
using Shouldly;
using Xunit;

/// <summary>
/// Tests for EpochVector multi-source tracking and composition rules.
/// </summary>
public class EpochVectorTests
{
    [Fact]
    public void EpochVector_None_Should_HaveEmptySequences()
    {
        // Arrange & Act
        var none = EpochVector.None;

        // Assert
        none.Sequences.ShouldBeEmpty();
        none.GetSequence("any-source").ShouldBe(-1);
    }

    [Fact]
    public void FromSingleSource_Should_CreateVectorWithOneSource()
    {
        // Arrange & Act
        var vector = EpochVector.FromSingleSource("source1", 42);

        // Assert
        vector.GetSequence("source1").ShouldBe(42);
        vector.GetSequence("source2").ShouldBe(-1);
        vector.Sequences.Count.ShouldBe(1);
    }

    [Fact]
    public void FromSources_Should_CreateVectorWithMultipleSources()
    {
        // Arrange
        var sequences = new Dictionary<string, long>
        {
            ["source1"] = 10,
            ["source2"] = 20,
            ["source3"] = 15
        };

        // Act
        var vector = EpochVector.FromSources(sequences);

        // Assert
        vector.GetSequence("source1").ShouldBe(10);
        vector.GetSequence("source2").ShouldBe(20);
        vector.GetSequence("source3").ShouldBe(15);
        vector.Sequences.Count.ShouldBe(3);
    }

    [Fact]
    public void Merge_Should_UseElementWiseMaximum()
    {
        // Arrange
        var vector1 = EpochVector.FromSources(new Dictionary<string, long>
        {
            ["source1"] = 10,
            ["source2"] = 5
        });

        var vector2 = EpochVector.FromSources(new Dictionary<string, long>
        {
            ["source1"] = 8,
            ["source2"] = 12,
            ["source3"] = 3
        });

        // Act
        var merged = vector1.Merge(vector2);

        // Assert - Element-wise max
        merged.GetSequence("source1").ShouldBe(10); // max(10, 8)
        merged.GetSequence("source2").ShouldBe(12); // max(5, 12)
        merged.GetSequence("source3").ShouldBe(3);  // only in vector2
    }

    [Fact]
    public void Merge_Should_IncludeSourcesFromBothVectors()
    {
        // Arrange
        var vector1 = EpochVector.FromSingleSource("source1", 5);
        var vector2 = EpochVector.FromSingleSource("source2", 10);

        // Act
        var merged = vector1.Merge(vector2);

        // Assert
        merged.GetSequence("source1").ShouldBe(5);
        merged.GetSequence("source2").ShouldBe(10);
        merged.Sequences.Count.ShouldBe(2);
    }

    [Fact]
    public void IsLessThanOrEqual_Should_ReturnTrue_WhenAllSequencesAreLessOrEqual()
    {
        // Arrange
        var vector1 = EpochVector.FromSources(new Dictionary<string, long>
        {
            ["source1"] = 10,
            ["source2"] = 5
        });

        var vector2 = EpochVector.FromSources(new Dictionary<string, long>
        {
            ["source1"] = 10,
            ["source2"] = 8,
            ["source3"] = 20
        });

        // Act & Assert - vector1 <= vector2 for common sources
        vector1.IsLessThanOrEqual(vector2).ShouldBeTrue();
    }

    [Fact]
    public void IsLessThanOrEqual_Should_ReturnFalse_WhenAnySequenceIsGreater()
    {
        // Arrange
        var vector1 = EpochVector.FromSources(new Dictionary<string, long>
        {
            ["source1"] = 15,
            ["source2"] = 5
        });

        var vector2 = EpochVector.FromSources(new Dictionary<string, long>
        {
            ["source1"] = 10,
            ["source2"] = 8
        });

        // Act & Assert - vector1 > vector2 for source1
        vector1.IsLessThanOrEqual(vector2).ShouldBeFalse();
    }

    [Fact]
    public void IsLessThanOrEqual_Should_ReturnFalse_WhenOtherMissingSource()
    {
        // Arrange
        var vector1 = EpochVector.FromSingleSource("source1", 10);
        var vector2 = EpochVector.FromSingleSource("source2", 20);

        // Act & Assert - vector1 has source1 but vector2 doesn't
        vector1.IsLessThanOrEqual(vector2).ShouldBeFalse();
    }

    [Fact]
    public void IncrementSource_Should_IncrementExistingSource()
    {
        // Arrange
        var vector = EpochVector.FromSingleSource("source1", 10);

        // Act
        var incremented = vector.IncrementSource("source1");

        // Assert
        incremented.GetSequence("source1").ShouldBe(11);
        
        // Original should be unchanged (immutable)
        vector.GetSequence("source1").ShouldBe(10);
    }

    [Fact]
    public void IncrementSource_Should_AddNewSourceStartingAt1()
    {
        // Arrange
        var vector = EpochVector.FromSingleSource("source1", 10);

        // Act
        var incremented = vector.IncrementSource("source2");

        // Assert
        incremented.GetSequence("source1").ShouldBe(10);
        incremented.GetSequence("source2").ShouldBe(1);
    }

    [Fact]
    public void EpochVector_Should_BeImmutable()
    {
        // Arrange
        var original = EpochVector.FromSingleSource("source1", 10);

        // Act
        var modified1 = original.IncrementSource("source1");
        var modified2 = original.Merge(EpochVector.FromSingleSource("source2", 5));

        // Assert - original unchanged
        original.GetSequence("source1").ShouldBe(10);
        original.GetSequence("source2").ShouldBe(-1);
        original.Sequences.Count.ShouldBe(1);

        // Modified versions are different
        modified1.GetSequence("source1").ShouldBe(11);
        modified2.Sequences.Count.ShouldBe(2);
    }

    [Fact]
    public void ToString_Should_FormatNoneCorrectly()
    {
        // Arrange
        var none = EpochVector.None;

        // Act
        var str = none.ToString();

        // Assert
        str.ShouldBe("EpochVector.None");
    }

    [Fact]
    public void ToString_Should_FormatSingleSourceCorrectly()
    {
        // Arrange
        var vector = EpochVector.FromSingleSource("source1", 42);

        // Act
        var str = vector.ToString();

        // Assert
        str.ShouldBe("EpochVector[source1=42]");
    }

    [Fact]
    public void ToString_Should_FormatMultipleSourcesInOrder()
    {
        // Arrange
        var vector = EpochVector.FromSources(new Dictionary<string, long>
        {
            ["sourceC"] = 30,
            ["sourceA"] = 10,
            ["sourceB"] = 20
        });

        // Act
        var str = vector.ToString();

        // Assert - Should be alphabetically ordered
        str.ShouldBe("EpochVector[sourceA=10, sourceB=20, sourceC=30]");
    }

    [Fact]
    public void EpochVector_Equality_Should_WorkCorrectly()
    {
        // Arrange
        var vector1 = EpochVector.FromSources(new Dictionary<string, long>
        {
            ["source1"] = 10,
            ["source2"] = 20
        });

        var vector2 = EpochVector.FromSources(new Dictionary<string, long>
        {
            ["source1"] = 10,
            ["source2"] = 20
        });

        var vector3 = EpochVector.FromSources(new Dictionary<string, long>
        {
            ["source1"] = 10,
            ["source2"] = 21
        });

        // Act & Assert
        vector1.Equals(vector2).ShouldBeTrue();
        (vector1 == vector2).ShouldBeTrue();
        vector1.Equals(vector3).ShouldBeFalse();
        (vector1 == vector3).ShouldBeFalse();
    }
}
