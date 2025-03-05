namespace Tests.DataFlow;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using Serilog;
using Serilog.Events;
using SerilogTracing;
using Xunit;
using Xunit.Abstractions;
using SerilogTracing.Expressions;
using Serilog.Templates;

[Exploratory]
public class ActivityTracingTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly List<LogEvent> _logEvents = new();
    private readonly IDisposable _activityListener;
    private static readonly ActivitySource TestActivitySource = new("TestSource");

    public ActivityTracingTests(ITestOutputHelper output)
    {
        _output = output;

        // Set up Serilog with a test sink to capture log events and proper tracing format
        var formatter = new ExpressionTemplate(
            "{@m}",  // Just use the message for simplicity in testing
            nameResolver: new TracingNameResolver());

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Sink(new TestLogEventSink(_logEvents))
            .WriteTo.TestOutput(output, formatter)
            .CreateLogger();

        // Set up SerilogTracing to capture activities
        _activityListener = new ActivityListenerConfiguration()
            .InitialLevel.Override("TestSource", LogEventLevel.Debug)
            .TraceToSharedLogger();
    }

    /// <summary>
    /// This test fails because Serilog tracing doesn't seem to support message templates with parameters driven by Activity tags. It has its own abstractions for activities and ways to add message template properties.
    /// see https://github.com/serilog-tracing/serilog-tracing/blob/dev/test/SerilogTracing.Tests/Instrumentation/ActivityInstrumentationTests.cs#L23
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task Activity_WithParameterizedDisplayName_CreatesParameterizedLogEvents()
    {
        // Create an activity with a parameterized display name
        using (var activity = TestActivitySource.StartActivity("TestOperation"))
        {
            if (activity != null)
            {
                // Set tags that can be used as parameters
                activity.AddTag("Operation", "CREATE");
                activity.AddTag("Database", "test-db");

                // Set the display name with parameter placeholders
                activity.DisplayName = "Test {Operation} {Database}";
                activity.AddBaggage("Operation", "CREATE");
                activity.AddEvent(new ActivityEvent("TestEvent"));
                activity.SetCustomProperty("Database", "test-db");
                activity.SetCustomProperty("{Database}", "test-db");
                // Add some typical activity
                await Task.Delay(50);

                // Complete the activity explicitly (may help if SerilogTracing hooks into completion)
                activity.Stop();
            }
        }

        // Allow time for events to be processed
        await Task.Delay(100);

        // Dump all captured events for inspection
        _output.WriteLine($"Captured {_logEvents.Count} total log events");
        _output.WriteLine("--- All Log Events ---");
        foreach (var logEvent in _logEvents)
        {
            _output.WriteLine($"Template: \"{logEvent.MessageTemplate}\"");
            _output.WriteLine($"Rendered: \"{logEvent.RenderMessage()}\"");

            // Output all properties
            foreach (var prop in logEvent.Properties)
            {
                _output.WriteLine($"  {prop.Key} = {prop.Value}");
            }
            _output.WriteLine("---");
        }

        // First, check if we got any events at all
        Assert.NotEmpty(_logEvents);

        // Look for any events with SerilogTracing data
        var tracingEvents = _logEvents.Where(e =>
            e.Properties.Keys.Any(k => k.StartsWith("@sp") || k.StartsWith("@tr"))).ToList();

        _output.WriteLine($"Found {tracingEvents.Count} events with tracing properties");

        // Look for events with our specific tags
        var tagEvents = _logEvents.Where(e =>
            e.Properties.ContainsKey("Operation") ||
            e.Properties.ContainsKey("Database")).ToList();

        _output.WriteLine($"Found {tagEvents.Count} events with our specific tags");

        // If we have events with our tags, verify them
        if (tagEvents.Any())
        {
            var tagEvent = tagEvents.First();

            // Verify the rendered message contains our tag values
            var renderedMessage = tagEvent.RenderMessage();
            _output.WriteLine($"Found event with template: {tagEvent.MessageTemplate}");
            _output.WriteLine($"Rendered as: {renderedMessage}");

            Assert.Contains("CREATE", renderedMessage);
            Assert.Contains("test-db", renderedMessage);
        }
        // If we don't have events with our tags but do have tracing events,
        // inspect those to see what's happening
        else if (tracingEvents.Any())
        {
            var tracingEvent = tracingEvents.First();
            _output.WriteLine($"Found tracing event with template: {tracingEvent.MessageTemplate}");

            // See what properties we have
            _output.WriteLine("Tracing event properties:");
            foreach (var prop in tracingEvent.Properties)
            {
                _output.WriteLine($"  {prop.Key} = {prop.Value}");
            }

            _output.WriteLine("Could not find expected tags in tracing events");
            Assert.True(false, "No events with our specific tags found");
        }
        else
        {
            _output.WriteLine("No tracing events captured at all");
            Assert.True(false, "No tracing events captured");
        }
    }

    public void Dispose()
    {
        _activityListener?.Dispose();
        Log.CloseAndFlush();
    }

    private class TestLogEventSink : Serilog.Core.ILogEventSink
    {
        private readonly List<LogEvent> _logEvents;

        public TestLogEventSink(List<LogEvent> logEvents)
        {
            _logEvents = logEvents;
        }

        public void Emit(LogEvent logEvent)
        {
            _logEvents.Add(logEvent);
        }
    }
}
