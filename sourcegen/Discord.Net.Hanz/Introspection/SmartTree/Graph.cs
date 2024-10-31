using System.Text;

namespace Discord.Net.Hanz.Introspection;

public sealed class Graph
{
    private List<Tree> Trees { get; }
    private Dictionary<Node, Tree> Table { get; }

    private readonly Tree _tail;

    private Graph(
        Tree tail,
        Dictionary<Node, Tree> table)
    {
        _tail = tail;
        Trees = new(table.Values.Distinct());
        Table = table;
    }

    private void Test()
    {
        var rootTrees = Trees.Where(x => x.Inputs.Count == 0).ToArray();

        
    }
    
    public override string ToString()
    {
        var plot = new Plot();
        var sb = new StringBuilder();

        var trees = Trees.Where(x => x.Inputs.Count == 0).ToArray();
        var next = new HashSet<Tree>();
        var nextQueue = new Queue<Tree>();

        for (var i = 0; i < trees.Length; i++)
        {
            var root = trees[i];
            root.Render(plot.CreateColumnFor(root));
            next.UnionWith(root.Consumers);
        }

        while (next.Count > 0)
        {
            var row = plot.GetCurrentRowAndCreateNew();

            foreach (var nextTree in next)
                nextQueue.Enqueue(nextTree);

            while (nextQueue.Count > 0)
            {
                var consumer = nextQueue.Dequeue();

                if (!next.Contains(consumer))
                    continue;

                sb.AppendLine(
                    $"{string.Join(" + ", consumer.Inputs.Select(x => x.GetHashCode()))} = {consumer.GetHashCode()}"
                );

                switch (consumer.Inputs.Count)
                {
                    case 0:
                        sb.AppendLine($"{consumer.GetHashCode()}> ends");
                        next.Remove(consumer);
                        break;
                    case 1:
                        var input = consumer.Inputs[0];
                        sb.AppendLine($"{consumer.GetHashCode()}> 1 input: {consumer.Inputs[0].GetHashCode()}");

                        if (row.TryGetColumnForTree(input, out var column))
                        {
                            sb.AppendLine($"{consumer.GetHashCode()}> simple input");

                            var consumerColumn = column.CreateSubColumnForTree(consumer);
                            consumer.Render(consumerColumn);
                            row.Replace(column, consumerColumn);

                            next.Remove(consumer);

                            sb.AppendLine($"{consumer.GetHashCode()}> {column.Depth} -> {consumerColumn.Depth}");


                            foreach (var nextConsumer in consumer.Consumers)
                            {
                                if (!next.Add(nextConsumer))
                                    continue;

                                nextQueue.Enqueue(nextConsumer);
                            }
                        }
                        else
                        {
                            sb.AppendLine($"{consumer.GetHashCode()}> complex input");
                        }

                        break;
                    case 2:
                        var left = consumer.Inputs[0];
                        var right = consumer.Inputs[1];

                        sb.AppendLine(
                            $"{consumer.GetHashCode()}> 2 inputs, {left.GetHashCode()} + {right.GetHashCode()}");

                        if (row.TryGetColumnForTree(left, out var leftColumn) &&
                            row.TryGetColumnForTree(right, out var rightColumn))
                        {
                            if (left.Consumers.Count == 1 && right.Consumers.Count == 1)
                            {
                                sb.AppendLine($"{consumer.GetHashCode()}> simple merge");

                                // we can pull the two out into 1 new mega column
                                var consumerColumn = plot.MergeColumnsFor(consumer, leftColumn, rightColumn);
                                //consumerColumn.Add(RenderedText.AlignHorizontally([leftColumn.Render(), rightColumn.Render()]));
                                consumer.Render(consumerColumn);

                                row.Columns.Insert(
                                    row.Columns.IndexOf(leftColumn),
                                    consumerColumn
                                );

                                row.Columns.Remove(leftColumn);
                                row.Columns.Remove(rightColumn);

                                next.Remove(consumer);

                                foreach (var nextConsumer in consumer.Consumers)
                                {
                                    if (!next.Add(nextConsumer))
                                        continue;

                                    nextQueue.Enqueue(nextConsumer);
                                }
                            }
                            else if (leftColumn == rightColumn)
                            {
                                sb.AppendLine(
                                    $"{consumer.GetHashCode()}> column merge"
                                );

                                if (leftColumn.SearchForTree(left, out var leftPart) &&
                                    leftColumn.SearchForTree(right, out var rightPart))
                                {
                                    sb.AppendLine(
                                        $"{consumer.GetHashCode()}> Parts: {leftPart.Tree.GetHashCode()} {leftPart.Depth} | {rightPart.Tree.GetHashCode()} {rightPart.Depth}"
                                    );

                                    var higherPart = leftPart.Depth < rightPart.Depth
                                        ? leftPart
                                        : rightPart;

                                    var lowerPart = leftPart.Depth > rightPart.Depth
                                        ? leftPart
                                        : rightPart;

                                    if (lowerPart.ParentColumns.Contains(higherPart))
                                    {
                                        sb.AppendLine(
                                            $"{consumer.GetHashCode()}> Lower part is child of higher part"
                                        );

                                        lowerPart.DetachFromParents();

                                        var lowerRender = lowerPart.RenderOld();

                                        higherPart.AttachChild(lowerPart);

                                        var bar = RenderedText.VertialBar(5, lowerRender.Height);

                                        int[] joinPoints =
                                        [
                                            (int) Math.Floor(lowerRender.Width / 2d),
                                            lowerRender.Width + (int) Math.Floor(bar.Width / 2d)
                                        ];
                                        var entryPoint = (joinPoints[0] + joinPoints[1]) / 2;

                                        var content = RenderedText.AlignHorizontally([lowerRender, bar]);

                                        // if (content.Width < entryPoint * 2)
                                        // {
                                        //     content = content.PadX((entryPoint * 2) - content.Width);
                                        // }

                                        lowerPart.Set(
                                            RenderedText.CreateJoinBar(
                                                lowerRender.Width + bar.Width,
                                                joinPoints,
                                                [entryPoint]
                                            ),
                                            content,
                                            RenderedText.CreateJoinBar(
                                                lowerRender.Width + bar.Width,
                                                [entryPoint],
                                                joinPoints
                                            )
                                        );

                                        var consumerColumn = lowerPart.CreateSubColumnForTree(consumer);

                                        consumer.Render(consumerColumn);

                                        row.Replace(leftColumn, consumerColumn);

                                        next.Remove(consumer);

                                        foreach (var nextConsumer in consumer.Consumers)
                                        {
                                            if (!next.Add(nextConsumer))
                                                continue;

                                            nextQueue.Enqueue(nextConsumer);
                                        }
                                    }
                                    else
                                    {
                                        sb.AppendLine(
                                            $"{consumer.GetHashCode()}> Lower part is not a child of higher part"
                                        );
                                    }
                                }
                            }
                            else
                            {
                                sb.AppendLine(
                                    $"{consumer.GetHashCode()}> complex merge, {leftColumn.Depth} | {rightColumn.Depth}");
                            }
                        }
                        else
                        {
                            sb.AppendLine($"{consumer.GetHashCode()}> both inputs are not rendered yet");
                        }

                        break;
                }
            }

            sb.AppendLine(row.ToString());

            foreach (var tree in next)
            {
                sb.AppendLine(
                    $"{string.Join(" + ", tree.Inputs.Select(x => x.GetHashCode()))} = {tree.GetHashCode()}"
                );
            }

            next.Clear();

            break;
        }

        return sb.ToString();
    }

    public string Debug()
    {
        var sb = new StringBuilder();

        var visited = new HashSet<Tree>();

        foreach (var tree in Trees)
        {
            LogTree(tree, visited, 0);
        }

        return sb.ToString();

        void LogTree(Tree tree, HashSet<Tree> visited, int depth = 0)
        {
            sb.AppendLine($"Tree {tree.GetHashCode()}".Prefix(depth * 2));

            if (!visited.Add(tree))
                return;

            if (tree.Nodes.Count > 0)
            {
                sb.AppendLine($" - {tree.Nodes.Count} Nodes:".Prefix(depth * 2));

                foreach (var node in tree.Nodes)
                {
                    sb.AppendLine($"   - {node.GetHashCode()}: {node.Name}".Prefix(depth * 2));
                }
            }

            if (tree.Inputs.Count > 0)
            {
                sb.AppendLine($" - {tree.Inputs.Count} Inputs:".Prefix(depth * 2));

                foreach (var input in tree.Inputs)
                {
                    sb.AppendLine($"   - Tree: {input.GetHashCode()}".Prefix(depth * 2));
                }
            }

            if (tree.Consumers.Count > 0)
            {
                sb.AppendLine($" - {tree.Consumers.Count} Consumers:".Prefix(depth * 2));

                foreach (var input in tree.Consumers)
                {
                    sb.AppendLine($"   - Tree: {input.GetHashCode()}".Prefix(depth * 2));
                }
            }
        }
    }

    public static Graph Create(Node node)
    {
        using var logger = Logger.CreateForTask("SmartTree").WithCleanLogFile();
        var table = new Dictionary<Node, Tree>();

        return new(CreateTree(node, table, logger), table);
    }

    private static Tree CreateTree(
        Node node,
        Dictionary<Node, Tree> table,
        Logger logger)
    {
        if (table.TryGetValue(node, out var tree))
            return tree;

        var current = node;

        tree = new Tree();

        while (true)
        {
            if (table.TryGetValue(current, out var existing))
            {
                tree.Inputs.Add(existing);
                existing.Consumers.Add(tree);
                return tree;
            }

            table[current] = tree;

            tree.Nodes.AddFirst(current);

            switch (current.Inputs.Count)
            {
                case 0: return tree;
                case 1:
                    current = current.Inputs[0];
                    break;
                default:
                    var inputTrees = current.Inputs
                        .Select(x => CreateTree(x, table, logger))
                        .ToArray();
                    tree.Inputs.AddRange(inputTrees);
                    foreach (var inputTree in inputTrees)
                    {
                        inputTree.Consumers.Add(tree);
                    }

                    return tree;
            }
        }
    }
}