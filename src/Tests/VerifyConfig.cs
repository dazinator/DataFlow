namespace Tests;

using System.Runtime.CompilerServices;
using Argon;

/// <summary>
/// Module initializer to configure Verify snapshot testing.
/// This ensures snapshot files are organized in a dedicated Snapshots directory
/// nested under the test class location.
/// </summary>
public static class VerifyConfig
{
    [ModuleInitializer]
    public static void Init()
    {
        // Configure Verify to store snapshots in a Snapshots subdirectory
        // organized by test class name
        Verifier.DerivePathInfo(
            (sourceFile, projectDirectory, type, method) =>
            {
                // Get the directory containing the test file
                var sourceDirectory = Path.GetDirectoryName(sourceFile)!;

                // Create Snapshots subdirectory for the test class
                var snapshotsDirectory = Path.Combine(sourceDirectory, "Snapshots", type.Name);

                return new(
                    directory: snapshotsDirectory,
                    typeName: type.Name,
                    methodName: method.Name);
            });
    }
}
