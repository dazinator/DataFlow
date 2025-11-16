namespace DataFlow.POC.Core;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Generic implementation of <see cref="IEpochOperation"/> that encapsulates service resolution
/// and callback invocation for a specific service type.
/// 
/// This class uses the "closure with type parameter" pattern to allow the generic helper method
/// to set up operations with encapsulated type logic that will resolve the service from the
/// epoch DI scope and invoke the caller's callback with the resolved instance.
/// </summary>
/// <typeparam name="TService">The type of service this operation requires.</typeparam>
internal sealed class EpochOperation<TService> : IEpochOperation
    where TService : notnull
{
    private readonly Func<TService, Task> _operation;
    private readonly CancellationToken _cancellationToken;

    public EpochOperation(
        Func<TService, Task> operation,
        CancellationToken cancellationToken)
    {
        _operation = operation ?? throw new ArgumentNullException(nameof(operation));
        _cancellationToken = cancellationToken;
    }

    public async Task ExecuteAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        // Check if cancelled before executing
        if (_cancellationToken.IsCancellationRequested || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        // Resolve the service from the epoch's DI scope
        var service = serviceProvider.GetRequiredService<TService>();

        // Invoke the caller's callback with the resolved service
        // Note: Exceptions are propagated to the processor for proper error handling
        await _operation(service).ConfigureAwait(false);
    }
}
