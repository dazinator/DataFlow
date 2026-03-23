namespace DataFlow.POC.Tests;

using DataFlow.POC.Core;
using Shouldly;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using DataFlow.POC.Registry;

/// <summary>
/// OBSOLETE — these tests cover the IDataEnvelope / CheckpointBarrier / Heartbeat types
/// that formed a library-level control plane by multiplexing control signals with data
/// in the same channel. This approach is superseded by the epoch stream model, which
/// handles barriers natively. Envelope-style workflows are an application-level concern.
/// Code retained for reference.
/// </summary>
public class EnvelopeTests
{
    [Fact(Skip = "Envelope control plane superseded by epoch streams — see class summary")]
    public void DataItem_Should_Wrap_Value_Correctly()
    {
        // Arrange & Act
        var dataItem = new DataItem<int>(42);

        // Assert
        dataItem.Value.ShouldBe(42);
        dataItem.ShouldBeAssignableTo<IDataEnvelope>();
    }

    [Fact(Skip = "Envelope control plane superseded by epoch streams — see class summary")]
    public void ToEnvelope_Should_Wrap_Value()
    {
        // Arrange & Act
        var envelope = 42.ToEnvelope();

        // Assert
        envelope.ShouldBeOfType<DataItem<int>>();
        ((DataItem<int>)envelope).Value.ShouldBe(42);
    }

    [Fact(Skip = "Envelope control plane superseded by epoch streams — see class summary")]
    public void IsDataItem_Should_Return_True_For_DataItem()
    {
        // Arrange
        var envelope = new DataItem<string>("test");

        // Act & Assert
        envelope.IsDataItem().ShouldBeTrue();
        envelope.IsControlSignal().ShouldBeFalse();
    }

    [Fact(Skip = "Envelope control plane superseded by epoch streams — see class summary")]
    public void IsControlSignal_Should_Return_True_For_Control_Signals()
    {
        // Arrange
        var checkpoint = new CheckpointBarrier(Guid.NewGuid(), DateTime.UtcNow);
        var heartbeat = new Heartbeat(DateTime.UtcNow);

        // Act & Assert
        checkpoint.IsControlSignal().ShouldBeTrue();
        checkpoint.IsDataItem().ShouldBeFalse();
        
        heartbeat.IsControlSignal().ShouldBeTrue();
        heartbeat.IsDataItem().ShouldBeFalse();
    }

    [Fact(Skip = "Envelope control plane superseded by epoch streams — see class summary")]
    public void GetValue_Should_Extract_Value_From_DataItem()
    {
        // Arrange
        IDataEnvelope envelope = new DataItem<string>("hello");

        // Act
        var value = envelope.GetValue<string>();

        // Assert
        value.ShouldBe("hello");
    }

    [Fact(Skip = "Envelope control plane superseded by epoch streams — see class summary")]
    public void GetValue_Should_Throw_For_Wrong_Type()
    {
        // Arrange
        IDataEnvelope envelope = new DataItem<int>(42);

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => envelope.GetValue<string>());
    }

    [Fact(Skip = "Envelope control plane superseded by epoch streams — see class summary")]
    public void GetValue_Should_Throw_For_Control_Signal()
    {
        // Arrange
        IDataEnvelope envelope = new CheckpointBarrier(Guid.NewGuid(), DateTime.UtcNow);

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => envelope.GetValue<int>());
    }

    [Fact(Skip = "Envelope control plane superseded by epoch streams — see class summary")]
    public void TryGetValue_Should_Return_True_For_Correct_Type()
    {
        // Arrange
        IDataEnvelope envelope = new DataItem<int>(42);

        // Act
        var success = envelope.TryGetValue<int>(out var value);

        // Assert
        success.ShouldBeTrue();
        value.ShouldBe(42);
    }

    [Fact(Skip = "Envelope control plane superseded by epoch streams — see class summary")]
    public void TryGetValue_Should_Return_False_For_Wrong_Type()
    {
        // Arrange
        IDataEnvelope envelope = new DataItem<int>(42);

        // Act
        var success = envelope.TryGetValue<string>(out var value);

        // Assert
        success.ShouldBeFalse();
        value.ShouldBeNull();
    }

    [Fact(Skip = "Envelope control plane superseded by epoch streams — see class summary")]
    public void TryGetValue_Should_Return_False_For_Control_Signal()
    {
        // Arrange
        IDataEnvelope envelope = new CheckpointBarrier(Guid.NewGuid(), DateTime.UtcNow);

        // Act
        var success = envelope.TryGetValue<int>(out var value);

        // Assert
        success.ShouldBeFalse();
        value.ShouldBe(default(int));
    }

    [Fact(Skip = "Envelope control plane superseded by epoch streams — see class summary")]
    public void CheckpointBarrier_Should_Have_Correct_Properties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var createdAt = DateTime.UtcNow;

        // Act
        var barrier = new CheckpointBarrier(id, createdAt);

        // Assert
        barrier.Id.ShouldBe(id);
        barrier.CreatedAt.ShouldBe(createdAt);
    }

    [Fact(Skip = "Envelope control plane superseded by epoch streams — see class summary")]
    public void Heartbeat_Should_Have_Correct_Properties()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;

        // Act
        var heartbeat = new Heartbeat(timestamp);

        // Assert
        heartbeat.Timestamp.ShouldBe(timestamp);
    }

    [Fact(Skip = "Envelope control plane superseded by epoch streams — see class summary")]
    public async Task WrapInEnvelopes_Should_Wrap_Plain_Stream()
    {
        // Arrange
        var source = AsyncEnumerable();
        var envelopes = new List<IDataEnvelope>();

        // Act
        await foreach (var envelope in EnvelopeAdapter.WrapInEnvelopes(source))
        {
            envelopes.Add(envelope);
        }

        // Assert
        envelopes.Count.ShouldBe(3);
        envelopes[0].ShouldBeOfType<DataItem<int>>();
        envelopes[0].GetValue<int>().ShouldBe(1);
        envelopes[1].GetValue<int>().ShouldBe(2);
        envelopes[2].GetValue<int>().ShouldBe(3);

        static async IAsyncEnumerable<int> AsyncEnumerable()
        {
            yield return 1;
            yield return 2;
            yield return 3;
            await Task.CompletedTask;
        }
    }

    [Fact(Skip = "Envelope control plane superseded by epoch streams — see class summary")]
    public async Task UnwrapEnvelopes_Should_Extract_Values()
    {
        // Arrange
        var source = EnvelopeStream();
        var values = new List<int>();

        // Act
        await foreach (var value in EnvelopeAdapter.UnwrapEnvelopes<int>(source))
        {
            values.Add(value);
        }

        // Assert
        values.Count.ShouldBe(2);
        values[0].ShouldBe(1);
        values[1].ShouldBe(3);

        static async IAsyncEnumerable<IDataEnvelope> EnvelopeStream()
        {
            yield return new DataItem<int>(1);
            yield return new CheckpointBarrier(Guid.NewGuid(), DateTime.UtcNow);
            yield return new DataItem<int>(3);
            await Task.CompletedTask;
        }
    }

    [Fact(Skip = "Envelope control plane superseded by epoch streams — see class summary")]
    public async Task FilterDataItems_Should_Filter_Only_Data()
    {
        // Arrange
        var source = MixedStream();
        var dataItems = new List<IDataEnvelope>();

        // Act
        await foreach (var envelope in EnvelopeAdapter.FilterDataItems(source))
        {
            dataItems.Add(envelope);
        }

        // Assert
        dataItems.Count.ShouldBe(2);
        dataItems.All(e => e.IsDataItem()).ShouldBeTrue();
        dataItems[0].GetValue<int>().ShouldBe(1);
        dataItems[1].GetValue<int>().ShouldBe(2);

        static async IAsyncEnumerable<IDataEnvelope> MixedStream()
        {
            yield return new DataItem<int>(1);
            yield return new CheckpointBarrier(Guid.NewGuid(), DateTime.UtcNow);
            yield return new DataItem<int>(2);
            yield return new Heartbeat(DateTime.UtcNow);
            await Task.CompletedTask;
        }
    }

    [Fact(Skip = "Envelope control plane superseded by epoch streams — see class summary")]
    public async Task FilterControlSignals_Should_Filter_Only_Control_Signals()
    {
        // Arrange
        var source = MixedStream();
        var controlSignals = new List<IDataEnvelope>();

        // Act
        await foreach (var envelope in EnvelopeAdapter.FilterControlSignals(source))
        {
            controlSignals.Add(envelope);
        }

        // Assert
        controlSignals.Count.ShouldBe(2);
        controlSignals.All(e => e.IsControlSignal()).ShouldBeTrue();
        controlSignals[0].ShouldBeOfType<CheckpointBarrier>();
        controlSignals[1].ShouldBeOfType<Heartbeat>();

        static async IAsyncEnumerable<IDataEnvelope> MixedStream()
        {
            yield return new DataItem<int>(1);
            yield return new CheckpointBarrier(Guid.NewGuid(), DateTime.UtcNow);
            yield return new DataItem<int>(2);
            yield return new Heartbeat(DateTime.UtcNow);
            await Task.CompletedTask;
        }
    }

    [Fact(Skip = "Envelope control plane superseded by epoch streams — see class summary")]
    public async Task Envelope_Roundtrip_Should_Preserve_Values()
    {
        // Arrange
        var original = new[] { 1, 2, 3, 4, 5 }.ToAsyncEnumerable();

        // Act
        var wrapped = EnvelopeAdapter.WrapInEnvelopes(original);
        var unwrapped = EnvelopeAdapter.UnwrapEnvelopes<int>(wrapped);
        var result = await unwrapped.ToListAsync();

        // Assert
        result.Count.ShouldBe(5);
        result.ShouldBe(new[] { 1, 2, 3, 4, 5 });
    }
}

// Helper extension for testing
internal static class AsyncEnumerableExtensions
{
    public static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IEnumerable<T> source)
    {
        foreach (var item in source)
        {
            yield return item;
        }
        await Task.CompletedTask;
    }

    public static async Task<List<T>> ToListAsync<T>(this IAsyncEnumerable<T> source)
    {
        var result = new List<T>();
        await foreach (var item in source)
        {
            result.Add(item);
        }
        return result;
    }
}
