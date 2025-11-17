namespace DataFlow.POC.Core;

using Microsoft.Extensions.DependencyInjection;
using DataFlow.POC.Checkpointing;

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
    private readonly Func<TService, Task>? _operation;
    private readonly Func<TService, IEpochOperationContext, Task>? _operationWithContext;
    private readonly CancellationToken _cancellationToken;

    public EpochOperation(
        Func<TService, Task> operation,
        CancellationToken cancellationToken)
    {
        _operation = operation ?? throw new ArgumentNullException(nameof(operation));
        _operationWithContext = null;
        _cancellationToken = cancellationToken;
    }

    public EpochOperation(
        Func<TService, IEpochOperationContext, Task> operation,
        CancellationToken cancellationToken)
    {
        _operation = null;
        _operationWithContext = operation ?? throw new ArgumentNullException(nameof(operation));
        _cancellationToken = cancellationToken;
    }

    public async Task ExecuteAsync(IServiceProvider serviceProvider, IEpochOperationContext context, CancellationToken cancellationToken)
    {
        // Check if cancelled before executing
        if (_cancellationToken.IsCancellationRequested || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        // Resolve the service from the epoch's DI scope
        var service = serviceProvider.GetRequiredService<TService>();

        // Invoke the appropriate callback based on which constructor was used
        if (_operation != null)
        {
            // Old signature: just service
            await _operation(service).ConfigureAwait(false);
        }
        else if (_operationWithContext != null)
        {
            // New signature: service and context
            await _operationWithContext(service, context).ConfigureAwait(false);
        }
    }
}
