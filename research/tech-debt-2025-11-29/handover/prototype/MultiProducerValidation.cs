namespace DataFlow.POC.Core;

/// <summary>
/// Prototype for validating multiple upstream connections to a single target.
/// This demonstrates one approach to addressing Finding 3 in the tech debt analysis.
/// </summary>
public static class MultiProducerValidation
{
    /// <summary>
    /// Validates that a block doesn't have multiple producers without a buffer node.
    /// This would be called in DataFlowGraphBuilder.Connect() method.
    /// </summary>
    /// <param name="sourceBlock">The source block being connected</param>
    /// <param name="targetBlock">The target block being connected to</param>
    /// <param name="existingIncomingEdges">Dictionary of existing incoming edges by block</param>
    /// <exception cref="InvalidOperationException">Thrown when multiple producers detected without buffer node</exception>
    public static void ValidateMultipleProducers(
        IBlock sourceBlock,
        IBlock targetBlock,
        Dictionary<IBlock, List<Edge>> existingIncomingEdges)
    {
        // Check if target block already has incoming edges
        if (existingIncomingEdges.TryGetValue(targetBlock, out var incomingEdges))
        {
            if (incomingEdges.Count > 0)
            {
                var existingSource = incomingEdges[0].Source;
                
                throw new InvalidOperationException(
                    $"Cannot connect block '{sourceBlock.Name}' to '{targetBlock.Name}': " +
                    $"Target block already has an incoming connection from '{existingSource.Name}'. " +
                    $"\n\n" +
                    $"Multiple producers to a single consumer require a buffer node to coordinate them. " +
                    $"\n\n" +
                    $"Use this pattern:\n" +
                    $"  var buffer = builder.Buffer<T>(capacity: N);\n" +
                    $"  builder.Connect(source1, buffer);\n" +
                    $"  builder.Connect(source2, buffer);\n" +
                    $"  builder.Connect(buffer, target);\n" +
                    $"\n" +
                    $"Where T is your data type and N is the buffer capacity.");
            }
        }
    }

    /// <summary>
    /// Alternative: Auto-create implicit buffer nodes when multiple producers detected.
    /// This demonstrates the "implicit buffer" approach mentioned in the issue.
    /// </summary>
    /// <remarks>
    /// This approach would require:
    /// 1. Detecting when multiple producers connect to same target
    /// 2. Automatically creating a buffer node
    /// 3. Rewiring connections through the buffer
    /// 4. Generating a unique name for the implicit buffer
    /// 5. Determining appropriate buffer capacity (default?)
    /// 
    /// Trade-offs:
    /// - PRO: More intuitive API, less boilerplate
    /// - CON: Hidden behavior, harder to debug
    /// - CON: Unclear what capacity to use
    /// - CON: Implicit buffers don't appear in user's mental model
    /// </remarks>
    public static BufferNode CreateImplicitBufferIfNeeded(
        IBlock targetBlock,
        Type dataType,
        Dictionary<IBlock, List<Edge>> existingIncomingEdges,
        List<BufferNode> bufferNodes,
        int defaultCapacity = 100)
    {
        // Check if we already created an implicit buffer for this target
        var existingBuffer = bufferNodes.FirstOrDefault(b => 
            b.Name == $"implicit-buffer-{targetBlock.Name}");
        
        if (existingBuffer != null)
        {
            return existingBuffer;
        }

        // Create new implicit buffer
        var buffer = new BufferNode(
            dataType, 
            defaultCapacity, 
            $"implicit-buffer-{targetBlock.Name}");
        
        bufferNodes.Add(buffer);
        
        return buffer;
    }
}
