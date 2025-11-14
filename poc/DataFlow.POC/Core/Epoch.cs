namespace DataFlow.POC.Core;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Implementation of an epoch with its own DI scope.
/// Manages the lifecycle of the DI scope and provides service resolution.
/// </summary>
internal sealed class Epoch : IEpoch
{
    private readonly IServiceScope _scope;
    private bool _disposed;

    public EpochVector Vector { get; private set; }
    public IServiceProvider ServiceProvider => _scope.ServiceProvider;

    public Epoch(EpochVector vector, IServiceScope scope)
    {
        Vector = vector ?? throw new ArgumentNullException(nameof(vector));
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
    }

    /// <summary>
    /// Updates the epoch vector during subsumption when sources join.
    /// Internal to restrict mutation to coordinator implementation only.
    /// </summary>
    internal void UpdateVector(EpochVector newVector)
    {
        ArgumentNullException.ThrowIfNull(newVector);
        Vector = newVector;
    }

    public T GetService<T>() where T : notnull
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return ServiceProvider.GetRequiredService<T>();
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }

        _disposed = true;
        _scope.Dispose();
        return ValueTask.CompletedTask;
    }
}
