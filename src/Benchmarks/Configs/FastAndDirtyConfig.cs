namespace Benchmarks;

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Toolchains.InProcess.NoEmit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public partial class Program
{
    /// <summary>
    /// A faster configuration for running quick benchmarks during development.
    /// Not suitable for publication, but good for rapid testing.
    /// </summary>
    public class FastAndDirtyConfig : ManualConfig
    {
        public FastAndDirtyConfig()
        {
            AddJob(Job.Default
                .WithToolchain(InProcessNoEmitToolchain.Instance)
                .WithIterationCount(3) // Reduce iterations
                .WithWarmupCount(1)    // Minimal warmup
                .WithInvocationCount(1)); // No invocation overhead calculations

            // Disable diagnosers for speed
            WithOptions(ConfigOptions.DisableOptimizationsValidator);

            // Add a console logger for BenchmarkDotNet
            AddLogger(ConsoleLogger.Default);
        }
    }
}

public static class LoggingSetup
{
    public static IServiceCollection AddConsoleLogging(this IServiceCollection services, LogLevel minimumLevel = LogLevel.Information)
    {
        // Add console logger explicitly 
        services.AddLogging(builder =>
        {
            // Add console provider with custom formatter
            builder.AddConsole(options =>
            {
                // options.FormatterName = "simple";
                // options.IncludeScopes
                //options.IncludeScopes = true;
            });

            // Add debug output for VS debugging
          //  builder.AddDebug();

            // Set minimum level
            builder.SetMinimumLevel(minimumLevel);
        });

        return services;
    }
}

