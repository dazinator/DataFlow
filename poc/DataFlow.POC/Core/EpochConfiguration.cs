namespace DataFlow.POC.Core;

using DataFlow.POC.Checkpointing;

/// <summary>
/// Configuration for epoch processing in a dataflow graph.
/// Defines the policy, processors, and lifecycle hooks for epoch management.
/// </summary>
public sealed class EpochConfiguration
{
    /// <summary>
    /// Gets or sets the epoch policy that determines when epochs are created.
    /// </summary>
    public EpochPolicy Policy { get; private set; } = EpochPolicy.Default;
    
    /// <summary>
    /// Gets or sets the lifecycle hooks for epoch processing.
    /// </summary>
    public EpochHooks Hooks { get; private set; } = new();
    
    /// <summary>
    /// Gets or sets the checkpoint strategy for determining when checkpoints are created.
    /// </summary>
    public ICheckpointStrategy? CheckpointStrategy { get; private set; }
    
    /// <summary>
    /// Gets the list of processor names to create.
    /// Multiple processors enable parallel epoch processing.
    /// </summary>
    public List<string> Processors { get; } = new();
    
    /// <summary>
    /// Sets the epoch policy.
    /// </summary>
    /// <param name="policy">The policy to use.</param>
    public void SetPolicy(EpochPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        Policy = policy;
    }
    
    /// <summary>
    /// Adds a processor to the configuration.
    /// Each processor runs independently, allowing for parallel epoch processing.
    /// </summary>
    /// <param name="name">Name of the processor.</param>
    public void AddProcessor(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Processor name cannot be null or whitespace", nameof(name));
        }
        if (Processors.Contains(name))
        {
            throw new ArgumentException($"Processor '{name}' already exists", nameof(name));
        }
        Processors.Add(name);
    }
    
    /// <summary>
    /// Registers a hook to execute before processing epoch operations.
    /// Typically used to begin transactions or prepare resources.
    /// </summary>
    /// <param name="handler">The handler to execute.</param>
    public void OnBeginEpoch(Func<IEpoch, CancellationToken, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        Hooks.OnBeginEpoch = handler;
    }
    
    /// <summary>
    /// Registers a hook to execute after all epoch operations complete successfully.
    /// Typically used to commit transactions or finalize resources.
    /// </summary>
    /// <param name="handler">The handler to execute.</param>
    public void OnCommitEpoch(Func<IEpoch, CancellationToken, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        Hooks.OnCommitEpoch = handler;
    }
    
    /// <summary>
    /// Registers a hook to execute when epoch processing fails.
    /// Typically used to rollback transactions or handle errors.
    /// </summary>
    /// <param name="handler">The handler to execute.</param>
    public void OnEpochError(Func<IEpoch, Exception, CancellationToken, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        Hooks.OnEpochError = handler;
    }
    
    /// <summary>
    /// Sets the checkpoint strategy that determines when checkpoints are created.
    /// </summary>
    /// <param name="strategy">The checkpoint strategy to use.</param>
    public void SetCheckpointStrategy(ICheckpointStrategy? strategy)
    {
        CheckpointStrategy = strategy;
    }
    
    /// <summary>
    /// Validates the configuration.
    /// </summary>
    internal void Validate()
    {
        if (Processors.Count == 0)
        {
            throw new InvalidOperationException("At least one processor must be configured. Call AddProcessor() to add a processor.");
        }
    }
}
