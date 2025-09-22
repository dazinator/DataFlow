namespace Tests.DataFlow;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Logging;
using Uniun.DataFlow.Metrics;


public class MonitoredChannelTests
{
    private readonly ITestOutputHelper _testOutputHelper;

    public MonitoredChannelTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        Services = new ServiceCollection();
        AddDefaultServices();       
    }

    private void AddDefaultServices()
    {
        Services.AddLogging(a => a.AddXUnit(Output));
        Services.AddDataFlowMetrics();
        Services.AddDataFlows();
        // Services.AddSingleton<IDataFlowMetrics, DataFlowMetrics>();
    }

    [IntegrationTest]
    [Fact]
    public async Task Should_Capture_ChannelBufferUtilisation()
    {
        // Arrange
        Guid invocationId = Guid.NewGuid();
        var testContext = new TestDataFlowContext()
        {
            InvocationId = invocationId,
            Name = "test-flow"
        };

        var channelCapacity = 100;
        var testBlockName = "test-block";

        var services = CreateServiceProvider();
        var meterFactory = services.GetRequiredService<IMeterFactory>();
        var collector = new MetricCollector<int>(meterFactory, MeterAccessor.MeterName, DataFlowMetrics.InstrumentNames.ChannelBufferUtilizationMetricName);

        var metrics = services.GetRequiredService<IDataFlowMetrics>();
        var factory = new MonitoredChannelFactory(metrics);
        var channel = factory.CreateMonitoredChannel<string>(testBlockName, channelCapacity);

        Output.WriteLine("1. Channel created");

        using var monitoringLease = channel.StartMonitoring(testContext);
        Output.WriteLine("2. Monitoring started, lease obtained");

        // Debug: Check if the channel can provide a snapshot
        var initialSnapshot = channel.GetMetricSnapshot();
        Output.WriteLine($"3. Initial snapshot: {(initialSnapshot != null ? $"Count={initialSnapshot.CurrentCount}, Capacity={initialSnapshot.Capacity}" : "NULL")}");

        // Act - Write some items to the channel
        var itemsToWrite = 25; // 25% utilization
        for (var i = 0; i < itemsToWrite; i++)
        {
            await channel.Writer.WriteAsync($"Item-{i}");
        }
        Output.WriteLine($"4. Written {itemsToWrite} items to channel");

        // Debug: Check snapshot after writing
        var afterWriteSnapshot = channel.GetMetricSnapshot();
        Output.WriteLine($"5. After write snapshot: {(afterWriteSnapshot != null ? $"Count={afterWriteSnapshot.CurrentCount}, Capacity={afterWriteSnapshot.Capacity}" : "NULL")}");

        // Debug: Check if DataFlowMetrics can see the channel
        if (metrics is DataFlowMetrics dataFlowMetrics)
        {
            // We need to access the private ChannelRegistry to debug
            // Let's try a different approach - check what the observable gauge returns
            Output.WriteLine("6. Calling RecordObservableInstruments...");
        }

        collector.RecordObservableInstruments();
        Output.WriteLine("7. RecordObservableInstruments called");

        // Give it a moment to process
        await Task.Delay(200);

        // Check what measurements we got immediately
        var immediateSnapshot = collector.GetMeasurementSnapshot();
        Output.WriteLine($"8. Immediate measurements count: {immediateSnapshot.Count}");

        foreach (var measurement in immediateSnapshot)
        {
            Output.WriteLine($"   Measurement: Value={measurement.Value}, Tags=[{string.Join(", ", measurement.Tags.Select(t => $"{t.Key}={t.Value}"))}]");
        }

        // Now wait for more measurements if needed
        if (immediateSnapshot.Count == 0)
        {
            Output.WriteLine("9. No immediate measurements, waiting...");
            await collector.WaitForMeasurementsAsync(1, TimeSpan.FromSeconds(5));
            var delayedSnapshot = collector.GetMeasurementSnapshot();
            Output.WriteLine($"10. After wait measurements count: {delayedSnapshot.Count}");

            foreach (var measurement in delayedSnapshot)
            {
                Output.WriteLine($"    Delayed Measurement: Value={measurement.Value}, Tags=[{string.Join(", ", measurement.Tags.Select(t => $"{t.Key}={t.Value}"))}]");
            }
        }

        var measurements = collector.GetMeasurementSnapshot();

        // Find our specific channel's measurement
        var channelMeasurement = measurements.FirstOrDefault(m =>
            m.Tags.Any(t => t.Key == DataFlowMetrics.TagNames.BlockName && t.Value?.ToString() == testBlockName));

        if (channelMeasurement == null)
        {
            Output.WriteLine($"11. ERROR: Could not find measurement for block '{testBlockName}'");
            Output.WriteLine("Available measurements:");
            foreach (var m in measurements)
            {
                Output.WriteLine($"    Value={m.Value}, Tags=[{string.Join(", ", m.Tags.Select(t => $"{t.Key}={t.Value}"))}]");
            }

            // Let's also check what tag names we're actually looking for
            Output.WriteLine($"Looking for tag: {DataFlowMetrics.TagNames.BlockName} = {testBlockName}");
        }

        Assert.NotNull(channelMeasurement);

        // Utilization should be 25%
        Assert.Equal(25, channelMeasurement.Value);

        // Verify all dimensions were captured
        Assert.Contains(channelMeasurement.Tags,
            t => t.Key == DataFlowMetrics.TagNames.FlowInvocationId && t.Value?.ToString() == invocationId.ToString());
        Assert.Contains(channelMeasurement.Tags,
            t => t.Key == DataFlowMetrics.TagNames.BlockName && t.Value?.ToString() == testBlockName);
    }

    /// <summary>
    /// Helper test to verify the observable gauge is working at all
    /// </summary>
    /// <returns></returns>
    [Exploratory]
    [Fact]
    public async Task Debug_ObservableGauge_Registration()
    {
        var services = CreateServiceProvider();
        var meterFactory = services.GetRequiredService<IMeterFactory>();
        var metrics = services.GetRequiredService<IDataFlowMetrics>();

        // Try to collect metrics without any channels
        var collector = new MetricCollector<int>(meterFactory, MeterAccessor.MeterName, DataFlowMetrics.InstrumentNames.ChannelBufferUtilizationMetricName);

        collector.RecordObservableInstruments();
        var emptyMeasurements = collector.GetMeasurementSnapshot();

        Console.WriteLine($"Empty measurements count: {emptyMeasurements.Count}");

        // The gauge should still fire even with no channels (returning empty enumerable)
        // If this doesn't work, the issue is with the meter/collector setup
    }

    public IServiceCollection Services { get; set; }

    public ITestOutputHelper Output => _testOutputHelper;

    private IServiceProvider CreateServiceProvider()
    {
        return Services.BuildServiceProvider();
    }

}
// Test implementation of IDataFlowContext
public class TestDataFlowContext : IDataFlowContext
{
    //private readonly Dictionary<string, string> _dimensionsInternal = new Dictionary<string, string>();

    public IServiceProvider ServiceProvider { get; set; } = new TestServiceProvider();
    public CancellationToken CancellationToken { get; set; } = CancellationToken.None;
    //public IDictionary<string, string> Dimensions => _dimensionsInternal;

    public Guid InvocationId { get; set; }
    public string Name { get; set; }
    public DataFlowMetricsTagsContext FlowMetricsContext { get; set; }
    public ConcurrentDictionary<string, object> Items { get; }
    //public void AddDimension(string key, string value)
    //{
    //    _dimensionsInternal[key] = value;
    //}
}

// Simple service provider for testing
public class TestServiceProvider : IServiceProvider
{
    public object GetService(Type serviceType) => null;
}

// Collector to capture metrics measurements
public class TestMetricCollector
{
    private readonly Dictionary<string, List<Measurement>> _measurements = new();
    private readonly List<MeterListener> _listeners = new();

    public void RegisterMeter(string meterName)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == meterName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            }
        };

        listener.SetMeasurementEventCallback<int>((instrument, measurement, tags, state) =>
        {
            RecordMeasurement(instrument.Name, measurement, tags);
        });

        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            RecordMeasurement(instrument.Name, measurement, tags);
        });

        listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) =>
        {
            RecordMeasurement(instrument.Name, measurement, tags);
        });

        listener.Start();
        _listeners.Add(listener);
    }

    private void RecordMeasurement(string name, object value, ReadOnlySpan<KeyValuePair<string, object>> tags)
    {
        if (!_measurements.TryGetValue(name, out var list))
        {
            list = new List<Measurement>();
            _measurements[name] = list;
        }

        list.Add(new Measurement
        {
            Value = value,
            Tags = tags.ToArray()
        });
    }

    public void CollectMetrics()
    {
        // Clear previous measurements
        _measurements.Clear();

        // Manually call RecordObservableInstruments on each listener
        foreach (var listener in _listeners)
        {
            listener.RecordObservableInstruments();
        }
    }

    public IReadOnlyList<Measurement> GetMeasurements(string instrumentName)
    {
        return _measurements.TryGetValue(instrumentName, out var measurements)
            ? measurements
            : Array.Empty<Measurement>();
    }

    public class Measurement
    {
        public object Value { get; set; }
        public KeyValuePair<string, object>[] Tags { get; set; }
    }

    public void Dispose()
    {
        foreach (var listener in _listeners)
        {
            listener.Dispose();
        }
    }
}





