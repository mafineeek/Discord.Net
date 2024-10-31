namespace Discord.Net.Hanz.Introspection;

public sealed class Tree
{
    public LinkedList<Node> Nodes { get; } = [];

    public List<Tree> Inputs { get; } = [];

    public List<Tree> Consumers { get; } = [];

    public RenderedText RenderNodes()
    {
        var lines = new List<string>();
        var maxWidth = Nodes.Max(x => x.Box.BoxWidth);

        foreach (var node in Nodes)
        {
            lines.AddRange(
                node.Box.RenderLines(
                    Nodes.First.Value != node || Inputs.Count > 0,
                    Nodes.Last.Value != node || Consumers.Count > 0
                )
            );
        }

        return new RenderedText(lines);
    }

    public void Render(Plot.Column column)
    {
        column.Add(RenderNodes());
    }

    public override int GetHashCode()
        => HashCode.Of(Nodes).AndEach(Inputs);
}