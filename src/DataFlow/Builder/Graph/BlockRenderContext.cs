namespace Uniun.DataFlow.Builder.Graph;

using System.Text;

/// <summary>
/// Default implementation of IBlockRenderContext that writes to a StringBuilder.
/// </summary>
internal class BlockRenderContext : IBlockRenderContext
{
    private readonly StringBuilder _stringBuilder;
    private readonly Action<string>? _interceptor;

    public BlockRenderContext(
        StringBuilder stringBuilder,
        BlockDefinition block,
        string direction,
        IServiceProvider? serviceProvider,
        string indentation = "",
        Action<string>? interceptor = null)
    {
        _stringBuilder = stringBuilder;
        _interceptor = interceptor;
        Block = block;
        Direction = direction;
        ServiceProvider = serviceProvider;
        Indentation = indentation;
    }

    public BlockDefinition Block { get; }
    public string Direction { get; }
    public IServiceProvider? ServiceProvider { get; }
    public string Indentation { get; }

    public void AppendLine(string content)
    {
        // Don't add indentation if content is empty
        var fullContent = string.IsNullOrEmpty(content) ? "" : Indentation + content;

        // Allow interceptor to modify or augment the content
        if (_interceptor != null)
        {
            _interceptor(fullContent);
        }
        else
        {
            _stringBuilder.AppendLine(fullContent);
        }
    }

    public IBlockRenderContext CreateNested(string additionalIndent = "    ")
    {
        return new BlockRenderContext(
            _stringBuilder,
            Block,
            Direction,
            ServiceProvider,
            Indentation + additionalIndent,
            _interceptor);
    }
}
