namespace Tests.DataFlow.Utils.Processors;
public class TestProcessor<T> : IStreamProcessor<T>
{
    private readonly Action<T>? _onProcessItem;
    private readonly TimeSpan? _delay;
    private readonly Func<T, bool>? _shouldError;
    private readonly string _errorMessage;

    public TestProcessor(
        Action<T>? onProcessItem = null,
        TimeSpan? delay = null,
        Func<T, bool>? shouldError = null,
        string? errorMessage = null)
    {
        _onProcessItem = onProcessItem;
        _delay = delay;
        _shouldError = shouldError;
        _errorMessage = errorMessage ?? "Simulated error in processor";
    }

    public async Task ProcessAsync(IAsyncEnumerable<T> input, CancellationToken cancellationToken)
    {
        await foreach (var item in input)
        {
            if (_delay.HasValue)
            {
                await Task.Delay(_delay.Value, cancellationToken);
            }

            if (_shouldError?.Invoke(item) == true)
            {
                throw new InvalidOperationException(_errorMessage);
            }

            _onProcessItem?.Invoke(item);
        }
    }
}
