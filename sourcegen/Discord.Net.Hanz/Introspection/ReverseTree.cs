using System.Reflection;
using System.Text;
using static Discord.Net.Hanz.Introspection.NodeIntrospection;

namespace Discord.Net.Hanz.Introspection;

public class ReverseTree
{
    public static string IntrospectAndRender(object node, Type type)
    {
        var box = IntrospectNode(node, type);

        if (box is null)
            return $"Null box {node}";

        var builder = new StringBuilder();

        using var logger = Logger.CreateForTask("ReverseTree").GetSubLogger("Build").WithCleanLogFile();
        
        builder.Append(RenderBox(box));

        var result = builder.ToString();
        
        logger.Log($"Result\n{result}");
        
        return result;
    }

    private static Pipeline RenderBox(NodeBox box, int depth = 0)
    {
        var current = box;
        var pipeline = new Pipeline();

        while (current is not null)
        {
            pipeline.Nodes.Insert(0, current);

            switch (current.Inputs.Count)
            {
                case 0:
                    current = null;
                    break;
                case 1:
                    current = current.Inputs[0];
                    break;
                default:
                    //if (depth >= 2) return pipeline;

                    pipeline.Inputs.AddRange(current.Inputs.Select(x => RenderBox(x, depth + 1)));
                    return pipeline;
            }
        }

        return pipeline;
    }

    public static int WhiteSpaceAtEnd(string self)
    {
        int count = 0;
        int ix = self.Length - 1;
        while (ix >= 0 && char.IsWhiteSpace(self[ix--]))
            ++count;

        return count;
    }

    public static int WhiteSpaceAtStart(string self)
    {
        int count = 0;
        int ix = 0;
        while (ix < self.Length && char.IsWhiteSpace(self[ix++]))
            ++count;

        return count;
    }

    private sealed class Pipeline
    {
        public List<NodeBox> Nodes { get; } = [];
        public List<Pipeline> Inputs { get; } = [];

        public int TotalWidth
        {
            get
            {
                var rawWidth = Math.Max(Inputs.Select(x => x.TotalWidth).Sum() + (Inputs.Count - 1), NodesWidth);

                if (rawWidth % 2 == 0)
                    rawWidth++;

                return rawWidth;
            }
        }

        public int JoinPoint => (int) Math.Floor(TotalWidth / 2d);

        public int NodesWidth => Nodes.Max(x => x.Box.BoxWidth);
        public int NodeJoinPoint => (int) Math.Floor(NodesWidth / 2d);
        

        private int[] _gaps = Array.Empty<int>();
        private int _inputWidth;
        //
        // private readonly struct RenderedNodes
        // {
        //     public readonly int Width;
        //     public readonly int JoinPoint;
        //
        //     public int Height => _lines.Count;
        //
        //     public string this[int i]
        //     {
        //         get => _lines[i];
        //         set => _lines[i] = value;
        //     }
        //     
        //     private readonly List<string> _lines;
        //
        //     public RenderedNodes(List<string> lines, int joinPoint, int width)
        //     {
        //         _lines = lines;
        //         JoinPoint = joinPoint;
        //         Width = width;
        //     }
        // }
        //
        // private sealed class RenderedPipeline
        // {
        //     public RenderedNodes Nodes { get; }
        //     public List<RenderedPipeline> Inputs { get; }
        //
        //     public int TreeWidth => Math.Max(Nodes.Width, Inputs.Sum(x => x.TreeWidth)) - InputGap;
        //
        //     public (int Lower, int Upper) JoinRange
        //     {
        //         get
        //         {
        //             if (Inputs.Count < 2) return (0, 0);
        //             
        //             return (Inputs[0].Nodes.JoinPoint, Inputs[1].Nodes.JoinPoint);
        //         }
        //     }
        //
        //     public int InputGap { get; private set; } = int.MaxValue;
        //     
        //     public void Visit()
        //     {
        //         if (Inputs.Count == 0) return;
        //         
        //         foreach (var input in Inputs)
        //         {
        //             input.Visit();
        //         }
        //         
        //         // can we move our two inputs closer
        //         var left = Inputs[0];
        //         var right = Inputs[1];
        //         
        //         var leftNodes = Inputs[0].Nodes;
        //         var rightNodes = Inputs[1].Nodes;
        //         
        //         var smallest = Math.Min(leftNodes.Height, rightNodes.Height);
        //
        //         int gap = int.MaxValue;
        //         
        //         for (var i = 0; i < smallest; i++)
        //         {
        //             var leftLine = leftNodes[leftNodes.Height - i - 1];
        //             var rightLine = rightNodes[rightNodes.Height - i - 1];
        //
        //             var lineGap = WhiteSpaceAtEnd(leftLine) + WhiteSpaceAtStart(rightLine);
        //
        //             if (lineGap < gap)
        //                 gap = lineGap;
        //         }
        //
        //         InputGap = Math.Max(gap, JoinRange.Upper - JoinRange.Lower);
        //     }
        //     
        //     public RenderedPipeline(
        //         RenderedNodes nodes,
        //         List<RenderedPipeline> inputs)
        //     {
        //         Nodes = nodes;
        //         Inputs = inputs;
        //
        //         //JoinPoint = Inputs.Sum(x => x.TreeWidth) / 2;
        //     }
        //
        //     private List<string> Render()
        //     {
        //         Visit();
        //
        //         var lines = new List<string>();
        //
        //         for (var i = 0; i < Inputs.Count; i++)
        //         {
        //             var input = Inputs[i];
        //             
        //         }
        //     }
        //     
        //     public override string ToString()
        //     {
        //        
        //
        //     }
        // }
        
        private void RenderInputs(StringBuilder builder)
        {
            _inputWidth = TotalWidth;
            
            if (Inputs.Count == 0) return;

            var builtInputs = Inputs
                .Select(x => x
                    .ToString()
                    .Split(
                        [Environment.NewLine],
                        StringSplitOptions.RemoveEmptyEntries
                    )
                    .Select(line => line.PadRight(x.TotalWidth))
                    .ToArray()
                ).ToArray();

            var inputHieght = builtInputs.Select(x => x.Length).Max();

            _gaps = Enumerable.Repeat(int.MaxValue, Inputs.Count - 1).ToArray();

            for (var pairingIndex = 0; pairingIndex < builtInputs.Length - 1; pairingIndex++)
            {
                var left = builtInputs[pairingIndex];
                var right = builtInputs[pairingIndex + 1];

                var smallest = Math.Min(left.Length, right.Length);

                var leftOffset = left.Length - smallest;
                var rightOffset = right.Length - smallest;

                for (var lineIndex = 0; lineIndex < smallest; lineIndex++)
                {
                    var leftLine = left[lineIndex + leftOffset];
                    var rightLine = right[lineIndex + rightOffset];

                    var gap = WhiteSpaceAtEnd(leftLine) + WhiteSpaceAtStart(rightLine);

                    if (gap < _gaps[pairingIndex])
                        _gaps[pairingIndex] = gap;
                }

                if (_gaps[pairingIndex] is > 0 and < int.MaxValue)
                {
                    for (var lineIndex = 0; lineIndex < smallest; lineIndex++)
                    {
                        ref var leftLine = ref left[lineIndex + leftOffset];
                        ref var rightLine = ref right[lineIndex + rightOffset];

                        var leftCount = WhiteSpaceAtEnd(leftLine);
                        var rightCount = WhiteSpaceAtStart(rightLine);

                        var leftRemove = Math.Min(leftCount, _gaps[pairingIndex]);
                        var rightRemove = Math.Min(_gaps[pairingIndex] - leftRemove, rightCount);

                        try
                        {
                            if (leftRemove > 0)
                            {
                                var idx = leftLine.Length - leftRemove;
                                leftLine = leftLine
                                    .Remove(idx, leftRemove);
                                //.Insert(idx, string.Empty.PadRight(leftRemove, 'L'));
                            }

                            if (rightRemove > 0)
                            {
                                rightLine = rightLine
                                    .Remove(0, rightRemove);
                                //.Insert(0, string.Empty.PadRight(rightRemove, 'R'));
                            }
                        }
                        catch (Exception ex)
                        {
                            throw new Exception(
                                $"Left: {left}\n" +
                                $" - {left.Length}\n" +
                                $" - {leftRemove}\n" +
                                $"Right: {right}\n" +
                                $" - {right.Length}\n" +
                                $" - {rightRemove}\n" +
                                $"Dist: {_gaps[pairingIndex]}",
                                ex
                            );
                        }
                    }
                }
            }

            _inputWidth = TotalWidth - _gaps.Sum();
            
            for (var j = 0; j < inputHieght; j++)
            {
                var section = new StringBuilder();

                for (var i = 0; i < Inputs.Count; i++)
                {
                    var input = Inputs[i];

                    var inputWidth = input.TotalWidth;
                    var inputLines = builtInputs[i];

                    var offset = inputHieght - inputLines.Length;
                    var index = j - offset;

                    if (i > 0)
                        section.Append(' ');

                    if (index < 0)
                    {
                        var spacingWidth = inputWidth;

                        if (i < Inputs.Count - 1 && _gaps[i] < int.MaxValue)
                        {
                            spacingWidth -= _gaps[i];
                        }
                        
                        if(spacingWidth < 0) continue;
                        
                        section.Append(string.Empty.PadLeft(spacingWidth));
                        continue;
                    }

                    var line = inputLines[index];

                    // if (i > 0)
                    // {
                    //     var lineSpacing = line.TakeWhile(x => x is ' ').Count();
                    //     
                    //     if(gaps[i - 1] > lineSpacing)
                    //         gaps[i - 1] = lineSpacing;
                    // }

                    if (j == inputHieght - 1)
                    {
                        line = line.Remove(input.JoinPoint, 1).Insert(input.JoinPoint, "\u252c");
                    }

                    section.Append(line);
                }

                builder.AppendLine(section.ToString().PadRight(TotalWidth));
                section.Clear();
            }

            var drop = new StringBuilder(string.Empty.PadLeft(TotalWidth));
            var horizontal = new StringBuilder(string.Empty.PadLeft(TotalWidth, '\u2500'));

            var joinOffset = 0;
            for (var i = 0; i < Inputs.Count; i++)
            {
                var input = Inputs[i];

                if (i > 0)
                {
                    joinOffset++;
                    horizontal.Append('\u2500');
                }

                var index = input.JoinPoint + joinOffset;

                if (i == 0)
                {
                    horizontal.Remove(0, index).Insert(0, string.Empty.PadRight(index, ' '));
                }
                else if (i == Inputs.Count - 1)
                {
                    var count = horizontal.Length - index;
                    horizontal.Remove(index, count).Insert(index, string.Empty.PadRight(count - 1, ' '));
                }

                drop[index] = '\u2502';

                var joinChar =
                    i == 0
                        ? '\u2514'
                        : i == Inputs.Count - 1
                            ? '\u2518'
                            : '\u2534';

                horizontal[index] = joinChar;
                
                joinOffset += input.TotalWidth;

                if (i < Inputs.Count - 1)
                    joinOffset -= _gaps[i];
            }

            switch (horizontal[JoinPoint])
            {
                case '\u2500':
                    horizontal[JoinPoint] = '\u252c';
                    break;
                case '\u2534':
                    horizontal[JoinPoint] = '\u253c';
                    break;
            }

            builder
                .AppendLine(drop.ToString())
                .AppendLine(horizontal.ToString())
                .AppendLine(string.Empty.PadLeft(TotalWidth).Remove(JoinPoint, 1).Insert(JoinPoint, "\u2502"));
        }

        public override string ToString()
        {
            if (Nodes.Count == 0) return string.Empty;

            var builder = new StringBuilder();

            RenderInputs(builder);

            var nodes = RenderNodes();

            if (Inputs.Count > 0)
            {
                nodes = nodes.Remove(NodeJoinPoint, 1).Insert(NodeJoinPoint, "\u2534");
            }

            var pad = (TotalWidth - NodesWidth) / 2;

            if (pad > 0)
                nodes = nodes.Prefix(pad).WithNewlinePadding(pad);
            
            
            // if (_inputWidth > 0 && builder.Length > 0)
            // {
            //     if (_inputWidth < NodesWidth)
            //     {
            //         var delta = (int)Math.Floor((NodesWidth - _inputWidth) / 2d);
            //         builder = new StringBuilder(builder.ToString().Prefix(delta, 'B').WithNewlinePadding(delta));
            //     }
            //     else
            //     {
            //         var delta = (int)Math.Floor((_inputWidth - NodesWidth) / 2d);
            //         nodes = nodes.Prefix(delta, 'A').WithNewlinePadding(delta);
            //     }
            // }

            return builder.AppendLine(nodes).ToString().TrimEnd(Environment.NewLine.ToCharArray());
        }

        // private RenderedNodes RenderNodesV2()
        // {
        //     var maxWidth = Nodes.Max(x => x.Box.BoxWidth);
        //     var joinPoint = (int) Math.Floor(maxWidth / 2d);
        //     
        //     var lines = new List<string>();
        //     
        //     for (var i = 0; i < Nodes.Count; i++)
        //     {
        //         var node = Nodes[i];
        //         
        //         
        //         var parts = node.ToString().Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries);
        //
        //         if (i > 0)
        //         {
        //             parts[0] = parts[0]
        //                 .Remove(joinPoint, 1)
        //                 .Insert(joinPoint, "\u2534");
        //         }
        //         
        //         lines.AddRange(parts);
        //
        //         if (i > 0)
        //         {
        //             lines[lines.Count - 1] = lines[lines.Count - 1]
        //                 .Remove(joinPoint, 1)
        //                 .Insert(joinPoint, "\u252c");
        //             
        //             var spacer = new StringBuilder()
        //                 .Append(string.Empty.PadLeft(maxWidth));
        //
        //             spacer[joinPoint] = '\u2502';
        //
        //             lines.Add(spacer.ToString());
        //         }
        //     }
        //
        //     return new(lines, joinPoint, maxWidth);
        // }
        //
        private string RenderNodes()
        {
            var builder = new StringBuilder();

            for (var i = 0; i < Nodes.Count; i++)
            {
                var node = Nodes[i];
                var delta = NodesWidth - node.Box.BoxWidth;
                var lPad = delta == 0 ? 0 : (int) Math.Floor(delta / 2d);
                var rPad = delta == 0 ? 0 : (int) Math.Ceiling(delta / 2d);

                var parts = node.ToString().Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries);

                if (i > 0)
                {
                    builder[builder.Length - Environment.NewLine.Length - (NodesWidth - NodeJoinPoint)] = '\u252c';

                    var spacer = new StringBuilder()
                        .Append(string.Empty.PadLeft(NodesWidth));

                    spacer[NodeJoinPoint] = '\u2502';

                    builder.AppendLine(spacer.ToString());
                }

                for (var j = 0; j < parts.Length; j++)
                {
                    var part = parts[j];

                    var line = $"{string.Empty.PadLeft(lPad)}{part}{string.Empty.PadRight(rPad)}";

                    if (i > 0 && j == 0)
                    {
                        line = line.Remove(NodeJoinPoint, 1).Insert(NodeJoinPoint, "\u2534");
                    }

                    builder.AppendLine(line.PadRight(NodesWidth));
                }
            }

            return builder.ToString().TrimEnd(Environment.NewLine.ToCharArray());
        }
    }

    private static NodeBox? IntrospectNode(object node, Type type)
    {
        switch (type.Name)
        {
            case "BatchNode`1":
                return CreateBatchNodeBox(node, type);
                break;
            case "CombineNode`2":
                return CreateCombineNodeBox(node, type);
                break;

            case "InputNode`1":
                return CreateInputNodeBox(node, type);
                break;

            case "SyntaxInputNode`1":
                return CreateSyntaxInputNodeBox(node, type);
                break;

            case "TransformNode`2":
                return CreateTransformNodeBox(node, type);
        }

        return null;
    }

    private static NodeBox CreateTransformNodeBox(object node, Type type)
    {
        return new NodeBox()
            {
                Box = new Box()
                {
                    Entries =
                    {
                        Entry.Text("Transform Node", true),
                        Entry.Keys(
                            ("From", PrettyTypeName(type.GenericTypeArguments[0])),
                            ("To", PrettyTypeName(type.GenericTypeArguments[1])),
                            ("Name", GetFieldValue(node, type, "_name")?.ToString())
                        )
                    }
                }
            }
            .WithInput(GetNodeFieldValue(node, type));
    }

    private static NodeBox CreateSyntaxInputNodeBox(object node, Type type)
    {
        return new NodeBox()
        {
            Box = new Box()
            {
                Entries =
                {
                    Entry.Text("Syntax Input Node", true),
                    Entry.Keys(
                        ("Type", PrettyTypeName(type.GenericTypeArguments[0]))
                    )
                }
            }
        };
    }

    private static NodeBox CreateInputNodeBox(object node, Type type)
    {
        return new NodeBox()
        {
            Box = new Box()
            {
                Entries =
                {
                    Entry.Text("Input Node", true),
                    Entry.Keys(
                        ("Type", PrettyTypeName(type.GenericTypeArguments[0]))
                    )
                }
            }
        };
    }

    private static NodeBox CreateCombineNodeBox(object node, Type type)
    {
        var left = GetFieldValue(node, type, "_input1");
        var right = GetFieldValue(node, type, "_input2");

        return new NodeBox()
            {
                Box = new Box()
                {
                    Entries =
                    {
                        Entry.Text("Combine Node", true),
                        Entry.Keys(
                            ("Left", PrettyTypeName(type.GenericTypeArguments[0])),
                            ("Right", PrettyTypeName(type.GenericTypeArguments[1])),
                            ("Name", GetFieldValue(node, type, "_name")?.ToString())
                        )
                    }
                }
            }
            .WithInput(left)
            .WithInput(right);
    }

    private static NodeBox CreateBatchNodeBox(object node, Type type)
    {
        var box = new NodeBox()
            {
                Box = new Box()
                {
                    Entries =
                    {
                        Entry.Text("Batch Node", true),
                        Entry.Keys(
                            ("Type", PrettyTypeName(type.GenericTypeArguments[0])),
                            ("Name", GetFieldValue(node, type, "_name")?.ToString())
                        )
                    }
                }
            }
            .WithInput(GetNodeFieldValue(node, type));

        return box;
    }

    private sealed class NodeBox
    {
        public Box Box { get; set; }
        public List<NodeBox> Inputs { get; set; } = [];

        public override string ToString()
            => Box?.ToString() ?? string.Empty;

        public NodeBox WithInput(object? child)
        {
            if (child is null) return this;

            var inputBox = IntrospectNode(child, child.GetType());

            if (inputBox is not null)
                Inputs.Add(inputBox);

            return this;
        }
    }
}