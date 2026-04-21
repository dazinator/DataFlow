using DataFlow.DeltaSpike.Delta;
using Shouldly;

namespace DataFlow.POC.Tests;

public sealed class DeltaSpikeTableClientTests
{
    [Fact]
    public async Task Should_append_and_read_incrementally_across_restart()
    {
        var tablePath = CreateTempTablePath();
        var firstClient = new DeltaLakeNetTableClient(tablePath, new DeltaTableClientOptions(PreferCdf: false, EnableCdfOnCreate: false));

        var firstCommit = await firstClient.AppendAsync(
        [
            new DeltaSpikeRow(1, "first", 0),
            new DeltaSpikeRow(2, "second", 0),
        ],
            CancellationToken.None);

        var firstRead = await firstClient.ReadChangesAsync(0, firstCommit.Version, CancellationToken.None);
        firstRead.Items.Select(x => x.Payload).ShouldBe(["first", "second"]);

        var secondClient = new DeltaLakeNetTableClient(tablePath, new DeltaTableClientOptions(PreferCdf: false, EnableCdfOnCreate: false));
        var secondCommit = await secondClient.AppendAsync(
        [
            new DeltaSpikeRow(3, "third", 0),
            new DeltaSpikeRow(4, "fourth", 0),
        ],
            CancellationToken.None);

        var incrementalRead = await secondClient.ReadChangesAsync(firstCommit.Version, secondCommit.Version, CancellationToken.None);

        incrementalRead.Strategy.ShouldBe(DeltaReadStrategy.VersionDiffFallback);
        incrementalRead.Items.Select(x => x.Payload).ShouldBe(["third", "fourth"]);
    }

    [Fact]
    public async Task Should_try_cdf_first_and_fallback_when_cdf_read_api_not_available()
    {
        var tablePath = CreateTempTablePath();
        var client = new DeltaLakeNetTableClient(tablePath, new DeltaTableClientOptions(PreferCdf: true, EnableCdfOnCreate: true));

        var commit = await client.AppendAsync(
        [
            new DeltaSpikeRow(10, "cdf-probe", 0),
        ],
            CancellationToken.None);

        var read = await client.ReadChangesAsync(0, commit.Version, CancellationToken.None);

        read.Strategy.ShouldBe(DeltaReadStrategy.VersionDiffFallback);
        read.Diagnostic.ShouldNotBeNull();
        read.Diagnostic.ShouldContain("CDF unavailable", Case.Insensitive);
        read.Items.Select(x => x.Payload).ShouldBe(["cdf-probe"]);
    }

    private static string CreateTempTablePath()
    {
        var directory = Path.Combine(Path.GetTempPath(), "dataflow-delta-spike-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
