namespace DataFlow.POC.Core;

using System.Collections.Immutable;

/// <summary>
/// Represents a multi-source epoch vector for tracking progress across multiple input sources.
/// Each source has its own monotonically increasing sequence number.
/// </summary>
public sealed record EpochVector
{
    /// <summary>
    /// Empty epoch vector representing no progress from any source.
    /// </summary>
    public static readonly EpochVector None = new(ImmutableDictionary<string, long>.Empty);

    /// <summary>
    /// Source ID to sequence number mapping.
    /// </summary>
    public ImmutableDictionary<string, long> Sequences { get; }

    public EpochVector(ImmutableDictionary<string, long> sequences)
    {
        Sequences = sequences ?? throw new ArgumentNullException(nameof(sequences));
    }

    /// <summary>
    /// Creates an epoch vector from a single source sequence.
    /// </summary>
    public static EpochVector FromSingleSource(string sourceId, long sequence)
    {
        ArgumentNullException.ThrowIfNull(sourceId);
        return new EpochVector(ImmutableDictionary<string, long>.Empty.Add(sourceId, sequence));
    }

    /// <summary>
    /// Creates an epoch vector from multiple source sequences.
    /// </summary>
    public static EpochVector FromSources(Dictionary<string, long> sequences)
    {
        ArgumentNullException.ThrowIfNull(sequences);
        return new EpochVector(sequences.ToImmutableDictionary());
    }

    /// <summary>
    /// Gets the sequence number for a specific source, or -1 if not present.
    /// </summary>
    public long GetSequence(string sourceId)
    {
        return Sequences.TryGetValue(sourceId, out var seq) ? seq : -1;
    }

    /// <summary>
    /// Merges two epoch vectors using element-wise maximum (fan-in operation).
    /// </summary>
    public EpochVector Merge(EpochVector other)
    {
        ArgumentNullException.ThrowIfNull(other);

        var builder = Sequences.ToBuilder();
        
        foreach (var (sourceId, otherSeq) in other.Sequences)
        {
            if (builder.TryGetValue(sourceId, out var currentSeq))
            {
                builder[sourceId] = Math.Max(currentSeq, otherSeq);
            }
            else
            {
                builder[sourceId] = otherSeq;
            }
        }

        return new EpochVector(builder.ToImmutable());
    }

    /// <summary>
    /// Checks if this vector is less than or equal to another vector for all common sources.
    /// Used to determine if an epoch has been completed.
    /// </summary>
    public bool IsLessThanOrEqual(EpochVector other)
    {
        ArgumentNullException.ThrowIfNull(other);

        foreach (var (sourceId, thisSeq) in Sequences)
        {
            var otherSeq = other.GetSequence(sourceId);
            if (otherSeq < 0 || thisSeq > otherSeq)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Increments the sequence for a specific source.
    /// </summary>
    public EpochVector IncrementSource(string sourceId)
    {
        ArgumentNullException.ThrowIfNull(sourceId);
        var currentSeq = GetSequence(sourceId);
        var newSeq = currentSeq < 0 ? 1 : currentSeq + 1;
        return new EpochVector(Sequences.SetItem(sourceId, newSeq));
    }

    public override string ToString()
    {
        if (Sequences.IsEmpty)
        {
            return "EpochVector.None";
        }

        var pairs = Sequences
            .OrderBy(kvp => kvp.Key)
            .Select(kvp => $"{kvp.Key}={kvp.Value}");
        return $"EpochVector[{string.Join(", ", pairs)}]";
    }

    /// <summary>
    /// Implements value equality for EpochVector based on dictionary contents.
    /// </summary>
    public bool Equals(EpochVector? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (Sequences.Count != other.Sequences.Count)
        {
            return false;
        }

        foreach (var (key, value) in Sequences)
        {
            if (!other.Sequences.TryGetValue(key, out var otherValue) || value != otherValue)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Computes hash code based on dictionary contents.
    /// </summary>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        
        foreach (var (key, value) in Sequences.OrderBy(kvp => kvp.Key))
        {
            hash.Add(key);
            hash.Add(value);
        }

        return hash.ToHashCode();
    }
}
