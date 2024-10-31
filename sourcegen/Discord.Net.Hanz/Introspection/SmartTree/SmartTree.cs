using System.Diagnostics;
using System.Drawing;

namespace Discord.Net.Hanz.Introspection;

using static Discord.Net.Hanz.Introspection.NodeIntrospection;

public partial class SmartTree
{
    public const string BatchNodeName = "BatchNode`1";
    public const string CombineNodeName = "CombineNode`2";
    public const string InputNodeName = "InputNode`1";
    public const string SyntaxInputNodeName = "SyntaxInputNode`1";
    public const string TransformNodeName = "TransformNode`2";

    public static string IntrospectAndRender(object node, Type type)
    {
        var table = new Dictionary<object, Node>();

        var tail = GetNode(node, type, table);

        return Build(tail, table);
    }

    private static string Build(Node tail, Dictionary<object, Node> nodes)
    {
        var graph = Graph.Create(tail);

        using var logger = Logger.CreateForTask("SmartTree")
            .GetSubLogger("Build");

        var result = graph.ToString();

        logger.Log($"Debug\n{graph.Debug()}");
        logger.Log($"Build\n{result}");

        return result;
    }

    private static Node GetNode(object node, Type type, Dictionary<object, Node> graph)
    {
        if (graph.TryGetValue(node, out var graphNode))
            return graphNode;

        graphNode = graph[node] = new(node);

        foreach (var input in GetInputs(node, type))
        {
            if (!graph.TryGetValue(input, out var inputNode))
                inputNode = GetNode(input, input.GetType(), graph);

            graphNode.AddInput(inputNode);
            //inputNode.AddConsumer(graphNode);
        }

        return graphNode;
    }

    private static object[] GetInputs(object node, Type type)
    {
        object?[] inputs = type.Name switch
        {
            BatchNodeName or TransformNodeName => [GetNodeFieldValue(node, type)],
            CombineNodeName => [GetFieldValue(node, type, "_input1"), GetFieldValue(node, type, "_input2")],
            _ => []
        };

        return inputs.Where(x => x is not null).ToArray()!;
    }


    public static string GetNameWithoutGenericArity(Type t)
    {
        string name = t.Name;
        int index = name.IndexOf('`');
        return index == -1 ? name : name.Substring(0, index);
    }
}