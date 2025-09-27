using System.Diagnostics.Metrics;
using System.Globalization;
using Microsoft.Extensions.Logging;

public class MemoryCsvSampler : IDisposable
{
    private readonly MeterListener _listener;
    private readonly StreamWriter _writer;
    private readonly DateTime _startTime;
    private readonly ILogger _logger;
    private readonly Timer _timer;

    public MemoryCsvSampler(string outputPath, ILogger logger)
    {
        _writer = new StreamWriter(outputPath, append: false);
        _writer.WriteLine("Timestamp,HeapSizeBytes");

        _startTime = DateTime.UtcNow;
        _logger = logger;

        _listener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                logger.LogInformation("Instrument published: {MeterName} - {InstrumentName}", instrument.Meter.Name, instrument.Name);
                if (instrument.Meter.Name == "System.Runtime" && instrument.Name == "gc.heap.size")
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            }
        };
        _listener.SetMeasurementEventCallback<long>((instrument, value, tags, state) =>
        {
            var timestamp = (DateTime.UtcNow - _startTime).TotalSeconds.ToString("F3", CultureInfo.InvariantCulture);
            _writer.WriteLine($"{timestamp},{value}");
        });
        _listener.Start();
        _listener.RecordObservableInstruments();
        // Call RecordObservableInstruments every second
        _timer = new Timer(_ => _listener.RecordObservableInstruments(), null, 0, 1000);
    }

    public void Dispose()
    {
        _timer.Dispose();
        _listener.Dispose();
        _writer.Dispose();
    }
}
