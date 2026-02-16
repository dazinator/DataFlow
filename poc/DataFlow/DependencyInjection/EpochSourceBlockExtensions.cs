namespace DataFlow.POC.DependencyInjection;

using DataFlow.POC.Blocks;
using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for adding epoch source blocks to DataFlow.
/// </summary>
public static class EpochSourceBlockExtensions
{
    /// <summary>
    /// Register an EpochSourceBlock with type-safe API.
    /// All dependencies are automatically injected via constructor.
    /// Uses IBlockContext constructor injection for proper lifecycle management.
    /// 
    /// Source blocks produce data streams with epoch boundaries without requiring input.
    /// The coordinator and scope factory are automatically resolved from DI.
    /// </summary>
    /// <typeparam name="T">Output type (data items produced by the source)</typeparam>
    /// <typeparam name="TActor">Source actor type implementing ISourceActor&lt;T&gt;</typeparam>
    /// <param name="builder">The DataFlow builder</param>
    /// <param name="name">Unique name for this source block</param>
    /// <returns>This builder for chaining</returns>
    /// <example>
    /// <code>
    /// services.AddDataFlows("app", df =>
    /// {
    ///     // Simple way to add a source block
    ///     df.AddSourceBlock&lt;string, ConsoleInputSource&gt;("input");
    ///     
    ///     // Add processing blocks
    ///     df.AddActorBlock&lt;string, string, UppercaseActor&gt;("uppercase");
    ///     
    ///     // Define graph
    ///     df.AddGraph("main", g =>
    ///     {
    ///         g.UseBlock("input")
    ///          .UseBlock("uppercase")
    ///          .Connect("input", "uppercase");
    ///     });
    /// });
    /// </code>
    /// </example>
    public static DataFlowBuilder AddSourceBlock<T, TActor>(this DataFlowBuilder builder, string name)
        where TActor : ISourceActor<T>
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Source block name cannot be null or whitespace", nameof(name));
        }

        // Use the existing AddBlock method with a factory that constructs the EpochSourceBlock
        return builder.AddBlock(name, sp =>
        {
            var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
            var coordinator = sp.GetRequiredService<IEpochCoordinator>();
            var context = new BlockContext(name);
            
            return new EpochSourceBlock<T, TActor>(context, scopeFactory, coordinator);
        });
    }
}
