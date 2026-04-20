using DataFlow.DeltaSpike.Delta;

var arguments = Args.Parse(args);
var cancellationToken = CancellationToken.None;

if (arguments.CleanTableFirst && Directory.Exists(arguments.TableLocation))
{
    Directory.Delete(arguments.TableLocation, recursive: true);
}

var client = new DeltaLakeNetTableClient(
    arguments.TableLocation,
    new DeltaTableClientOptions(
        PreferCdf: true,
        EnableCdfOnCreate: true));

Console.WriteLine($"Delta spike table: {arguments.TableLocation}");

var firstCommit = await client.AppendAsync(
[
    new DeltaSpikeRow(1, "first-run-a", 0),
    new DeltaSpikeRow(2, "first-run-b", 0),
],
    cancellationToken);

Console.WriteLine($"Commit 1 -> version {firstCommit.Version}, rows {firstCommit.RowCount}");

var initialRead = await client.ReadChangesAsync(0, firstCommit.Version, cancellationToken);
Console.WriteLine($"Initial read strategy: {initialRead.Strategy}");
PrintRows("Initial read rows", initialRead.Items);

var restartCursor = firstCommit.Version;
var restartedClient = new DeltaLakeNetTableClient(
    arguments.TableLocation,
    new DeltaTableClientOptions(
        PreferCdf: true,
        EnableCdfOnCreate: true));

var secondCommit = await restartedClient.AppendAsync(
[
    new DeltaSpikeRow(3, "second-run-c", 0),
    new DeltaSpikeRow(4, "second-run-d", 0),
],
    cancellationToken);

Console.WriteLine($"Commit 2 -> version {secondCommit.Version}, rows {secondCommit.RowCount}");

var incrementalRead = await restartedClient.ReadChangesAsync(restartCursor, secondCommit.Version, cancellationToken);
Console.WriteLine($"Incremental read strategy: {incrementalRead.Strategy}");
if (!string.IsNullOrWhiteSpace(incrementalRead.Diagnostic))
{
    Console.WriteLine($"Incremental read diagnostic: {incrementalRead.Diagnostic}");
}
PrintRows("Incremental rows after restart", incrementalRead.Items);

if (!string.IsNullOrWhiteSpace(arguments.AbfssUri))
{
    await ProbeAbfssAsync(arguments.AbfssUri!, arguments.BearerToken, cancellationToken);
}

return;

static void PrintRows(string title, IReadOnlyList<DeltaSpikeRow> rows)
{
    Console.WriteLine(title);
    foreach (var row in rows)
    {
        Console.WriteLine($"  id={row.Id}, payload={row.Payload}, commitVersion={row.CommitVersion}");
    }
}

static async Task ProbeAbfssAsync(string abfssUri, string? bearerToken, CancellationToken cancellationToken)
{
    var storageOptions = string.IsNullOrWhiteSpace(bearerToken)
        ? new Dictionary<string, string>()
        : new Dictionary<string, string> { ["bearer_token"] = bearerToken };

    Console.WriteLine($"ABFSS probe: {abfssUri}");

    try
    {
        var probeClient = new DeltaLakeNetTableClient(
            abfssUri,
            new DeltaTableClientOptions(PreferCdf: false, EnableCdfOnCreate: false, StorageOptions: storageOptions));

        var latestVersion = await probeClient.GetLatestVersionAsync(cancellationToken);
        Console.WriteLine($"ABFSS probe result: SUCCESS (latest version={latestVersion})");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"ABFSS probe result: {ex.GetType().Name} -> {ex.Message}");
    }
}

file sealed record Args(string TableLocation, bool CleanTableFirst, string? AbfssUri, string? BearerToken)
{
    private const string DefaultTablePath = "/tmp/dataflow-delta-spike/table";

    public static Args Parse(string[] args)
    {
        var tableLocation = DefaultTablePath;
        var cleanTableFirst = true;
        string? abfssUri = null;
        string? bearerToken = Environment.GetEnvironmentVariable("DELTA_SPIKE_BEARER_TOKEN");

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--table-path" when i + 1 < args.Length:
                    tableLocation = args[++i];
                    break;
                case "--keep-table":
                    cleanTableFirst = false;
                    break;
                case "--abfss-uri" when i + 1 < args.Length:
                    abfssUri = args[++i];
                    break;
                case "--bearer-token" when i + 1 < args.Length:
                    bearerToken = args[++i];
                    break;
            }
        }

        return new Args(tableLocation, cleanTableFirst, abfssUri, bearerToken);
    }
}
