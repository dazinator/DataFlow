namespace Uniun.DataFlow.Builder.Graph;

using Uniun.DataFlow.Blocks.Broadcast;

/// <summary>
/// A builder for configuring a broadcast block with per-target settings.
/// Provides a fluent API to configure specific targets with custom clone functions.
/// </summary>
/// <typeparam name="T">The type of items being broadcast</typeparam>
public class StructuredBroadcastBlockBuilder<T>
{
    private readonly StructuredPropagatorBlockBuilder<T, T> _propagatorBuilder;
    private readonly Dictionary<string, Func<T, T>?> _targetConfigurations;

    internal StructuredBroadcastBlockBuilder(
        StructuredPropagatorBlockBuilder<T, T> propagatorBuilder,
        Dictionary<string, Func<T, T>?> targetConfigurations)
    {
        _propagatorBuilder = propagatorBuilder;
        _targetConfigurations = targetConfigurations;
    }

    /// <summary>
    /// Configures a specific target block by name with a custom clone function.
    /// This allows different targets to have different cloning strategies.
    /// </summary>
    /// <param name="targetName">The name of the target block that will connect to this broadcast</param>
    /// <param name="cloneFunc">The clone function for this target, or null to not clone for this target</param>
    /// <returns>This builder for chaining</returns>
    public StructuredBroadcastBlockBuilder<T> WithTarget(string targetName, Func<T, T>? cloneFunc)
    {
        _targetConfigurations[targetName] = cloneFunc;
        return this;
    }

    /// <summary>
    /// Specifies the source block for this broadcast block.
    /// </summary>
    /// <param name="sourceBlockName">The name of the source block</param>
    /// <returns>This builder for chaining</returns>
    public StructuredBroadcastBlockBuilder<T> ReceiveFrom(string sourceBlockName)
    {
        _propagatorBuilder.ReceiveFrom(sourceBlockName);
        return this;
    }
}
