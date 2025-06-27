namespace Tests.DataFlow;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Uniun.DataFlow.Metrics;

[IntegrationTest]
public class MonitoredChannelTests
{

    public MonitoredChannelTests()
    {
        Services = new ServiceCollection();
        AddDefaultServices();
    }

    private void AddDefaultServices()
    {
        Services.AddDataFlowMetrics();
        Services.AddDataFlows();
        // Services.AddSingleton<IDataFlowMetrics, DataFlowMetrics>();
    }

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
        // testContext.AddDimension("tenant.id", "test-tenant-123");
        //  testContext.AddDimension(DataFlowMetrics.TagNames., "test-flow-456");

        var channelCapacity = 100;
        var testBlockName = "test-block";

        var services = CreateServiceProvider();
        var meterFactory = services.GetRequiredService<IMeterFactory>();
        var collector = new MetricCollector<int>(meterFactory, MeterAccessor.MeterName, DataFlowMetrics.InstrumentNames.ChannelBufferUtilizationMetricName);

        var metrics = services.GetRequiredService<IDataFlowMetrics>();
        var factory = new MonitoredChannelFactory(metrics);
        var channel = factory.CreateMonitoredChannel<string>(testBlockName, channelCapacity);
        channel.StartMonitoring(testContext);
        // Act - Write some items to the channel
        var itemsToWrite = 25; // 25% utilization
        for (var i = 0; i < itemsToWrite; i++)
        {
            await channel.Writer.WriteAsync($"Item-{i}");
        }

        collector.RecordObservableInstruments();
        await collector.WaitForMeasurementsAsync(2, TimeSpan.FromSeconds(10));
        var measurements = collector.GetMeasurementSnapshot();
        //Assert.Equal(1, measurements.Count);
        //Assert.Equal(15, measurements[0].Value);            

        // Find our specific channel's measurement
        var channelMeasurement = measurements.FirstOrDefault(m =>
            m.Tags.Any(t => t.Key == DataFlowMetrics.TagNames.BlockName && t.Value.ToString() == testBlockName));

        Assert.NotNull(channelMeasurement);

        // Utilization should be close to 25%
        Assert.Equal(25, channelMeasurement.Value);

        // Verify all dimensions were captured
        //Assert.Contains(channelMeasurement.Tags,
        //    t => t.Key == "tenant.id" && t.Value.ToString() == "test-tenant-123");
        Assert.Contains(channelMeasurement.Tags,
            t => t.Key == DataFlowMetrics.TagNames.FlowInvocationId && t.Value.ToString() == invocationId.ToString());
        Assert.Contains(channelMeasurement.Tags,
            t => t.Key == DataFlowMetrics.TagNames.BlockName && t.Value.ToString() == testBlockName);

        //var countMeasurements = metricCollector.GetMeasurements(DataFlowMetrics.ChannelBufferUtilizationMetricName)
        //    .Where(m => m.Tags.Any(t => t.Key == "metric" && t.Value.ToString() == "active_channel_count"))
        //    .ToList();     
    }

    public IServiceCollection Services { get; set; }

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
    public DataFlowMetricsContext FlowMetricsContext { get; set; }
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





