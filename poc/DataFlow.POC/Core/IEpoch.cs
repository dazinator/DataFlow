namespace DataFlow.POC.Core;

/// <summary>
/// Represents an epoch instance with its own DI scope.
/// Multiple sources/blocks accessing the same epoch will share the same DI scope
/// and get the same scoped service instances.
/// </summary>
public interface IEpoch : IAsyncDisposable
{
    /// <summary>
    /// The epoch vector identifying this epoch.
    /// </summary>
    EpochVector Vector { get; }

    /// <summary>
    /// Resolves a service from this epoch's DI scope.
    /// Multiple sources/blocks accessing the same epoch will get the same instance.
    /// </summary>
    T GetService<T>() where T : notnull;

    /// <summary>
    /// Gets the service provider for this epoch's scope.
    /// </summary>
    IServiceProvider ServiceProvider { get; }
}
