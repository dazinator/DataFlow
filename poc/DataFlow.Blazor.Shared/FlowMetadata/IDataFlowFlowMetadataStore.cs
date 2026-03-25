namespace DataFlow.Blazor.FlowMetadata;

/// <summary>
/// Provides visualization metadata for registered dataflows, keyed by their
/// fully-qualified graph name as emitted in <c>FlowStartedEvent.FlowName</c>
/// (e.g. <c>"journal-v2:main"</c> for a graph named "main" in namespace "journal-v2").
///
/// When <c>AddDataFlows("journal-v2", df => df.DisplayName("My Flow"))</c> is used
/// together with <c>df.AddGraph("main", …)</c>, the display name is stored under the
/// key <c>"journal-v2:main"</c> — matching the value of <c>FlowSnapshot.FlowName</c>
/// that the server resolves at request time.
///
/// If <c>DisplayName</c> is called without any <c>AddGraph</c> calls in the same builder
/// (e.g. in a unit-test or metadata-only scenario), the namespace prefix is used as the
/// fallback key (e.g. <c>"journal-v2"</c>).
///
/// Registered as a singleton populated at startup via <c>AddDataFlows()</c>.
/// </summary>
public interface IDataFlowFlowMetadataStore
{
    /// <summary>Returns the full metadata for the named flow, or <see langword="null"/> if none is registered.</summary>
    DataFlowFlowMetadata? GetMetadata(string flowName);

    /// <summary>
    /// Returns the configured display name for the named flow, or <see langword="null"/> if none is registered.
    /// </summary>
    string? GetDisplayName(string flowName);
}
