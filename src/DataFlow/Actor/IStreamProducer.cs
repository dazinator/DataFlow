namespace Uniun.DataFlow.Actor;

public interface IStreamProducer<TOutput>
{
    // Task ExecuteAsync(ChannelWriter<T> writer, CancellationToken cancellation);
    IAsyncEnumerable<TOutput> ProduceAsync(CancellationToken cancellation);
}
