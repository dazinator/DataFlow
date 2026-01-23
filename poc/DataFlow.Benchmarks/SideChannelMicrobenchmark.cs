namespace DataFlow.POC.Benchmarks;

using System.Threading.Channels;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;
using DataFlow.POC.Registry;

/// <summary>
/// BenchmarkDotNet microbenchmark for side-channel competing edge mechanisms.
/// Focuses on isolated components: channel writes, reads, and merge operations.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80, warmupCount: 3, iterationCount: 10)]
public class SideChannelMicrobenchmark
{
    private const int ItemCount = 10000;
    private const int ControlSignalCount = 100;
    
    private Channel<IDataEnvelope> _singleChannel = null!;
    private Channel<IDataEnvelope> _dataChannel = null!;
    private Channel<IDataEnvelope> _controlChannel1 = null!;
    private Channel<IDataEnvelope> _controlChannel2 = null!;
    private List<IDataEnvelope> _testData = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Create test data
        _testData = new List<IDataEnvelope>(ItemCount + ControlSignalCount);
        var controlInterval = ItemCount / ControlSignalCount;
        
        for (int i = 0; i < ItemCount; i++)
        {
            _testData.Add(new DataItem<int>(i));
            
            if ((i + 1) % controlInterval == 0 && i < ItemCount - 1)
            {
                _testData.Add(new CheckpointBarrier(Guid.NewGuid(), DateTime.UtcNow));
            }
        }

        // Setup channels for each iteration
        SetupChannels();
    }

    [IterationSetup]
    public void SetupChannels()
    {
        // Single channel (standard competing approach)
        _singleChannel = Channel.CreateBounded<IDataEnvelope>(new BoundedChannelOptions(100)
        {
            SingleReader = false,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });

        // Dual channels (side-channel approach)
        _dataChannel = Channel.CreateBounded<IDataEnvelope>(new BoundedChannelOptions(100)
        {
            SingleReader = false,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });

        _controlChannel1 = Channel.CreateBounded<IDataEnvelope>(new BoundedChannelOptions(5)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });

        _controlChannel2 = Channel.CreateBounded<IDataEnvelope>(new BoundedChannelOptions(5)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    /// <summary>
    /// Baseline: Write all items to a single channel (standard competing).
    /// </summary>
    [Benchmark(Baseline = true)]
    public async Task StandardCompeting_WriteToSingleChannel()
    {
        var writer = _singleChannel.Writer;
        
        foreach (var item in _testData)
        {
            await writer.WriteAsync(item);
        }
        
        writer.Complete();
    }

    /// <summary>
    /// Side-channel: Route data to data channel, broadcast control to both control channels.
    /// </summary>
    [Benchmark]
    public async Task SideChannel_WriteWithRouting()
    {
        var dataWriter = _dataChannel.Writer;
        var control1Writer = _controlChannel1.Writer;
        var control2Writer = _controlChannel2.Writer;
        
        foreach (var item in _testData)
        {
            if (item.IsControlSignal())
            {
                // Broadcast to both control channels
                await Task.WhenAll(
                    control1Writer.WriteAsync(item).AsTask(),
                    control2Writer.WriteAsync(item).AsTask()
                );
            }
            else
            {
                // Write to data channel
                await dataWriter.WriteAsync(item);
            }
        }
        
        dataWriter.Complete();
        control1Writer.Complete();
        control2Writer.Complete();
    }

    /// <summary>
    /// Baseline: Read all items from a single channel (standard competing, one consumer).
    /// </summary>
    [Benchmark]
    public async Task StandardCompeting_ReadFromSingleChannel()
    {
        SetupChannels();
        
        // First write data
        var writer = _singleChannel.Writer;
        foreach (var item in _testData)
        {
            await writer.WriteAsync(item);
        }
        writer.Complete();

        // Then read
        var count = 0;
        await foreach (var item in _singleChannel.Reader.ReadAllAsync())
        {
            count++;
        }
    }

    /// <summary>
    /// Side-channel: Read from merged data + control channels (one consumer).
    /// </summary>
    [Benchmark]
    public async Task SideChannel_ReadFromMergedChannels()
    {
        SetupChannels();
        
        // Write data
        var dataWriter = _dataChannel.Writer;
        var controlWriter = _controlChannel1.Writer;
        
        foreach (var item in _testData)
        {
            if (item.IsControlSignal())
            {
                await controlWriter.WriteAsync(item);
            }
            else
            {
                await dataWriter.WriteAsync(item);
            }
        }
        
        dataWriter.Complete();
        controlWriter.Complete();

        // Read using merge pattern
        var mergeChannel = Channel.CreateBounded<IDataEnvelope>(new BoundedChannelOptions(100)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });

        var mergeWriter = mergeChannel.Writer;

        // Forward both channels
        var controlTask = Task.Run(async () =>
        {
            await foreach (var item in _controlChannel1.Reader.ReadAllAsync())
            {
                await mergeWriter.WriteAsync(item);
            }
        });

        var dataTask = Task.Run(async () =>
        {
            await foreach (var item in _dataChannel.Reader.ReadAllAsync())
            {
                await mergeWriter.WriteAsync(item);
            }
        });

        // Complete when both done
        _ = Task.Run(async () =>
        {
            await Task.WhenAll(controlTask, dataTask);
            mergeWriter.Complete();
        });

        // Read merged
        var count = 0;
        await foreach (var item in mergeChannel.Reader.ReadAllAsync())
        {
            count++;
        }
    }

    /// <summary>
    /// Microbenchmark: Just the merge operation cost (read from 2 channels, write to 1).
    /// </summary>
    [Benchmark]
    public async Task Isolated_MergeOperation()
    {
        // Pre-fill channels
        var channel1 = Channel.CreateBounded<int>(new BoundedChannelOptions(100)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait
        });

        var channel2 = Channel.CreateBounded<int>(new BoundedChannelOptions(5)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait
        });

        var mergeChannel = Channel.CreateBounded<int>(new BoundedChannelOptions(100)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });

        // Pre-fill data
        var writer1 = channel1.Writer;
        for (int i = 0; i < ItemCount; i++)
        {
            await writer1.WriteAsync(i);
        }
        writer1.Complete();

        var writer2 = channel2.Writer;
        for (int i = 0; i < ControlSignalCount; i++)
        {
            await writer2.WriteAsync(-i);
        }
        writer2.Complete();

        // Measure merge cost
        var mergeWriter = mergeChannel.Writer;

        var task1 = Task.Run(async () =>
        {
            await foreach (var item in channel1.Reader.ReadAllAsync())
            {
                await mergeWriter.WriteAsync(item);
            }
        });

        var task2 = Task.Run(async () =>
        {
            await foreach (var item in channel2.Reader.ReadAllAsync())
            {
                await mergeWriter.WriteAsync(item);
            }
        });

        _ = Task.Run(async () =>
        {
            await Task.WhenAll(task1, task2);
            mergeWriter.Complete();
        });

        var count = 0;
        await foreach (var _ in mergeChannel.Reader.ReadAllAsync())
        {
            count++;
        }
    }

    /// <summary>
    /// Baseline: Control signal detection overhead using IsControlSignal().
    /// </summary>
    [Benchmark]
    public void Isolated_ControlSignalDetection()
    {
        var controlCount = 0;
        var dataCount = 0;
        
        foreach (var item in _testData)
        {
            if (item.IsControlSignal())
            {
                controlCount++;
            }
            else
            {
                dataCount++;
            }
        }
    }

    /// <summary>
    /// Isolated: Single channel write performance.
    /// </summary>
    [Benchmark]
    public async Task Isolated_SingleChannelWrite()
    {
        var channel = Channel.CreateBounded<IDataEnvelope>(new BoundedChannelOptions(100)
        {
            SingleReader = false,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });

        var writer = channel.Writer;
        
        var writeTask = Task.Run(async () =>
        {
            foreach (var item in _testData)
            {
                await writer.WriteAsync(item);
            }
            writer.Complete();
        });

        var readTask = Task.Run(async () =>
        {
            var count = 0;
            await foreach (var _ in channel.Reader.ReadAllAsync())
            {
                count++;
            }
        });

        await Task.WhenAll(writeTask, readTask);
    }

    /// <summary>
    /// Isolated: Dual channel write with routing performance.
    /// </summary>
    [Benchmark]
    public async Task Isolated_DualChannelWriteWithRouting()
    {
        var dataChannel = Channel.CreateBounded<IDataEnvelope>(new BoundedChannelOptions(100)
        {
            SingleReader = false,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });

        var controlChannel = Channel.CreateBounded<IDataEnvelope>(new BoundedChannelOptions(5)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });

        var writeTask = Task.Run(async () =>
        {
            foreach (var item in _testData)
            {
                if (item.IsControlSignal())
                {
                    await controlChannel.Writer.WriteAsync(item);
                }
                else
                {
                    await dataChannel.Writer.WriteAsync(item);
                }
            }
            
            dataChannel.Writer.Complete();
            controlChannel.Writer.Complete();
        });

        var readTask1 = Task.Run(async () =>
        {
            var count = 0;
            await foreach (var _ in dataChannel.Reader.ReadAllAsync())
            {
                count++;
            }
        });

        var readTask2 = Task.Run(async () =>
        {
            var count = 0;
            await foreach (var _ in controlChannel.Reader.ReadAllAsync())
            {
                count++;
            }
        });

        await Task.WhenAll(writeTask, readTask1, readTask2);
    }
}
