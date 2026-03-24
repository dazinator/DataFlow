// ReSharper disable once CheckNamespace
namespace Uniun.DataFlow.Blocks;

/// <summary>
/// Interface for source blocks that need to know how many downstream targets will connect before
/// they start emitting items. Implementing this allows source blocks to wait for all expected
/// targets to register via <see cref="GetAsyncEnumerable"/> before processing begins,
/// avoiding race conditions where items are broadcast before all consumers are ready.
/// </summary>
public interface IExpectsDownstreamTargets
{
    /// <summary>
    /// Notifies this source block that one additional downstream target will connect.
    /// Call this when wiring a target block's source to this block (i.e., in SetSource).
    /// </summary>
    void RegisterExpectedTarget();
}
