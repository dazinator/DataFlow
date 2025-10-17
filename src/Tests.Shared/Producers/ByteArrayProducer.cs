namespace Tests.DataFlow.Utils.Producers;

using System.Runtime.CompilerServices;
using Uniun.DataFlow;

public class ByteArrayProducer : IStreamProducer<byte[]>
{
    private readonly int _itemCount;
    private readonly int _sizeInBytes;

    public ByteArrayProducer(int itemCount, int sizeInBytes)
    {
        _itemCount = itemCount;
        _sizeInBytes = sizeInBytes;
    }
    public async IAsyncEnumerable<byte[]> ProduceAsync(IDataFlowContext context,
        [EnumeratorCancellation] CancellationToken cancellation)
    {
        var enumerable = GenerateByteArrayStream(_itemCount, _sizeInBytes);
        foreach (var i in enumerable)
        {
            cancellation.ThrowIfCancellationRequested();
            yield return i;
        }
    }

    private static IEnumerable<byte[]> GenerateByteArrayStream(int itemCount, int blobSizeBytes)
    {
        for (var i = 0; i < itemCount; i++)
        {
            yield return new byte[blobSizeBytes];
        }
    }
}
