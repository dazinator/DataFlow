namespace DataFlow.Blazor.Models;

/// <summary>
/// Discriminated union representing the currently selected object in the flow diagram.
/// </summary>
public abstract record SelectedObject;

/// <summary>
/// The trigger icon was clicked — show trigger parameters in the detail pane.
/// </summary>
public record TriggerSelected : SelectedObject;

/// <summary>
/// A block node was clicked — show block details in the detail pane.
/// </summary>
public record BlockSelected(string BlockName) : SelectedObject;

/// <summary>
/// The buffers toolbar icon was clicked — show the buffer summary panel.
/// </summary>
public record BuffersSelected : SelectedObject;
