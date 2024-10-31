using static Discord.Net.Hanz.Introspection.SmartTree;

namespace Discord.Net.Hanz.Introspection;

public class Node
    {
        public string Name { get; }

        public List<Node> Inputs { get; }

        public NodeIntrospection.Box Box { get; }

        public object Value { get; }

        public Type Type { get; }

        public Node(object value)
        {
            Type = value.GetType();
            Name = Type.Name;
            Inputs = [];
            Value = value;

            Box = new NodeIntrospection.Box()
            {
                Entries =
                {
                    NodeIntrospection.Entry.Text(GetNameWithoutGenericArity(Type), true),
                    NodeIntrospection.Entry.Keys(("HashCode", value.GetHashCode().ToString()))
                }
            };

            switch (Name)
            {
                case TransformNodeName:
                    Box.Entries.Add(NodeIntrospection.Entry.Keys(
                        ("From", NodeIntrospection.PrettyTypeName(Type.GenericTypeArguments[0])),
                        ("To", NodeIntrospection.PrettyTypeName(Type.GenericTypeArguments[1])),
                        ("Name", NodeIntrospection.GetFieldValue(value, Type, "_name")?.ToString()),
                        ("Func", ((Delegate) NodeIntrospection.GetFieldValue(value, Type, "_func")).Method.Name)
                    ));
                    break;
                case BatchNodeName:
                case SyntaxInputNodeName:
                case InputNodeName:
                    Box.Entries.Add(NodeIntrospection.Entry.Keys(
                        ("Type", NodeIntrospection.PrettyTypeName(Type.GenericTypeArguments[0])),
                        ("Name", NodeIntrospection.GetFieldValue(value, Type, "_name")?.ToString())
                    ));
                    break;
                case CombineNodeName:
                    Box.Entries.Add(NodeIntrospection.Entry.Keys(
                        ("Left", NodeIntrospection.PrettyTypeName(Type.GenericTypeArguments[0])),
                        ("Right", NodeIntrospection.PrettyTypeName(Type.GenericTypeArguments[1])),
                        ("Name", NodeIntrospection.GetFieldValue(value, Type, "_name")?.ToString())
                    ));
                    break;
            }
        }

        public void AddInput(Node node)
        {
            Inputs.Add(node);
            Box.Entries.Add(NodeIntrospection.Entry.Keys(($"Input {Inputs.Count}", node.GetHashCode().ToString())));
        }

        // public void AddConsumer(Node node)
        // {
        //     Consumers.Add(node);
        //     Box.Entries.Add(Entry.Keys(($"Consumer {Consumers.Count}", node.GetHashCode().ToString())));
        // }

        public override int GetHashCode()
            => Value.GetHashCode();
    }