namespace DataFlow.Blazor.Models;

/// <summary>
/// Registry of custom Blazor component types to use for rendering block detail views.
/// Register entries at application startup via:
///   builder.Services.Configure&lt;BlockDetailViewOptions&gt;(opts =>
///       opts.Register("MyBlockType", typeof(MyBlockDetailView)));
/// The registered component must accept a [Parameter] BlockState BlockState property.
/// </summary>
public class BlockDetailViewOptions
{
    private readonly Dictionary<string, Type> _registry = new();

    public void Register(string blockType, Type componentType) =>
        _registry[blockType] = componentType;

    public Type? TryGet(string blockType) =>
        _registry.TryGetValue(blockType, out var t) ? t : null;
}
