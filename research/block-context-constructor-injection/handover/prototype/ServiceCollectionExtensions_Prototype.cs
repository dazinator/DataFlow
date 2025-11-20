// Prototype: ServiceCollectionExtensions with improved registration methods
// This demonstrates how the AddActorBlock method changes to pass context via constructor

namespace DataFlow.POC.DependencyInjection;

using DataFlow.POC.Core;
using DataFlow.POC.Blocks;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// PROTOTYPE: Enhanced DataFlowBuilder with improved block registration methods.
/// Each typed helper method passes IBlockContext via constructor instead of SetContext.
/// </summary>
public static class DataFlowBuilder_Prototype_Extensions
{
    /// <summary>
    /// PROTOTYPE: Register an ActorBlock with context passed via constructor.
    /// This eliminates the need for SetContext post-construction.
    /// </summary>
    public static DataFlowBuilder AddActorBlock_Prototype<TIn, TOut, TActor>(
        this DataFlowBuilder builder,
        string name)
        where TActor : IStreamActor<TIn, TOut>
    {
        // This would be implemented in the actual DataFlowBuilder class
        // For prototype, we show the registration logic
        
        var services = GetServiceCollection(builder); // Helper to access _services
        var fullKey = builder.Namespace + ":" + name;

        services.AddKeyedScoped<IBlock>(fullKey, (sp, key) =>
        {
            // Step 1: Create context from key
            var blockName = key as string ?? throw new InvalidOperationException("Block key must be a string");
            var context = new BlockContext(blockName);
            
            // Step 2: Resolve other dependencies
            var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
            
            // Step 3: Construct block with ALL dependencies via constructor
            return new ActorBlock_Prototype<TIn, TOut, TActor>(context, scopeFactory);
        });

        return builder;
    }

    /// <summary>
    /// PROTOTYPE: Register a ProducerBlock with context passed via constructor.
    /// </summary>
    public static DataFlowBuilder AddProducerBlock_Prototype<T>(
        this DataFlowBuilder builder,
        string name,
        Func<IExecutionContext, IAsyncEnumerable<T>> producer)
    {
        var services = GetServiceCollection(builder);
        var fullKey = builder.Namespace + ":" + name;

        services.AddKeyedScoped<IBlock>(fullKey, (sp, key) =>
        {
            var blockName = key as string ?? throw new InvalidOperationException("Block key must be a string");
            var context = new BlockContext(blockName);
            
            // ProducerBlock constructor would be: ProducerBlock(IBlockContext context, Func<...> producer)
            return new ProducerBlock_Prototype<T>(context, producer);
        });

        return builder;
    }

    /// <summary>
    /// PROTOTYPE: Register a TransformBlock with context passed via constructor.
    /// </summary>
    public static DataFlowBuilder AddTransformBlock_Prototype<TIn, TOut>(
        this DataFlowBuilder builder,
        string name,
        Func<TIn, TOut> transform)
    {
        var services = GetServiceCollection(builder);
        var fullKey = builder.Namespace + ":" + name;

        services.AddKeyedScoped<IBlock>(fullKey, (sp, key) =>
        {
            var blockName = key as string ?? throw new InvalidOperationException("Block key must be a string");
            var context = new BlockContext(blockName);
            
            // TransformBlock constructor would be: TransformBlock(IBlockContext context, Func<TIn, TOut> transform)
            return new TransformBlock_Prototype<TIn, TOut>(context, transform);
        });

        return builder;
    }

    // Helper method to access internal _services field
    // In actual implementation, these methods would be part of DataFlowBuilder class
    private static IServiceCollection GetServiceCollection(DataFlowBuilder builder)
    {
        // This is a placeholder - in real code, these would be methods inside DataFlowBuilder
        // that have direct access to _services field
        throw new NotImplementedException("This is a prototype - actual implementation would be inside DataFlowBuilder");
    }
}

// Placeholder block types for prototype
public class ProducerBlock_Prototype<T> : BlockBase_Prototype<object, T>
{
    private readonly Func<IExecutionContext, IAsyncEnumerable<T>> _producer;

    public ProducerBlock_Prototype(
        IBlockContext context,
        Func<IExecutionContext, IAsyncEnumerable<T>> producer)
        : base(context)
    {
        _producer = producer ?? throw new ArgumentNullException(nameof(producer));
    }

    public override async IAsyncEnumerable<T> ExecuteAsync(
        IAsyncEnumerable<object> input,
        IExecutionContext context)
    {
        await foreach (var item in _producer(context).WithCancellation(context.CancellationToken))
        {
            yield return item;
        }
    }
}

public class TransformBlock_Prototype<TIn, TOut> : BlockBase_Prototype<TIn, TOut>
{
    private readonly Func<TIn, TOut> _transform;

    public TransformBlock_Prototype(
        IBlockContext context,
        Func<TIn, TOut> transform)
        : base(context)
    {
        _transform = transform ?? throw new ArgumentNullException(nameof(transform));
    }

    public override async IAsyncEnumerable<TOut> ExecuteAsync(
        IAsyncEnumerable<TIn> input,
        IExecutionContext context)
    {
        await foreach (var item in input.WithCancellation(context.CancellationToken))
        {
            yield return _transform(item);
        }
    }
}
