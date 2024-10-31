using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Discord.Net.Hanz.Introspection;

public class NodeIntrospection
{
    public static string Introspect<T>(IncrementalValueProvider<T> provider)
    {
        var node = GetFieldValue(provider, provider.GetType(), "Node");

        if (node is null) return "Null Node";

        return ReverseTree.IntrospectAndRender(node, node.GetType());
    }

    public static string Introspect<T>(IncrementalValuesProvider<T> provider)
    {
        var node = GetFieldValue(provider, provider.GetType(), "Node");

        if (node is null) return "Null Node";

        return ReverseTree.IntrospectAndRender(node, node.GetType());
    }

    public static string IntrospectSmart<T>(IncrementalValueProvider<T> provider)
    {
        var node = GetFieldValue(provider, provider.GetType(), "Node");

        if (node is null) return "Null Node";

        return SmartTree.IntrospectAndRender(node, node.GetType());
    }

    public static string IntrospectSmart<T>(IncrementalValuesProvider<T> provider)
    {
        var node = GetFieldValue(provider, provider.GetType(), "Node");

        if (node is null) return "Null Node";

        return SmartTree.IntrospectAndRender(node, node.GetType());
    }

    public static string PrettyTypeName(Type t)
    {
        if (t.IsArray)
        {
            return PrettyTypeName(t.GetElementType()!) + "[]";
        }

        if (t.IsGenericType)
        {
            return string.Format(
                "{0}<{1}>",
                t.Name.Substring(0, t.Name.LastIndexOf("`", StringComparison.InvariantCulture)),
                string.Join(", ", t.GetGenericArguments().Select(PrettyTypeName)));
        }

        return t.Name;
    }

    public static object? GetNodeFieldValue(object node, Type type, string name = "_sourceNode")
        => GetFieldValue(node, type, name);

    public static object? GetFieldValue(object node, Type type, string name)
    {
        var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);

        return field?.GetValue(node);
    }

    public sealed class Box
    {
        public List<Entry> Entries { get; } = [];

        //public List<Box> Input { get; } = [];

        public int BoxWidth
            => ContentWidth + 4;

        public int ContentWidth
        {
            get
            {
                var rawWidth = Entries.Select(x => x.ContentWidth).Max();

                if (rawWidth % 2 == 0)
                    rawWidth++;

                return rawWidth;
            }
        }

        public int Height => Entries.Select(x => x.ContentHeight).Sum();

        // public Box WithInput(object? child)
        // {
        //     if (child is null) return this;
        //
        //     var inputBox = IntrospectNode(child, child.GetType());
        //
        //     if (inputBox is not null)
        //         Input.Add(inputBox);
        //
        //     return this;
        // }

        public IEnumerable<string> RenderLines(bool joinUp = false, bool joinDown = false)
        {
            var spacerWidth = BoxWidth - 2;
            var spacer = string.Empty.PadLeft(spacerWidth, '\u2500');

            var topLine = joinUp
                ? spacer.Remove(spacerWidth / 2, 1).Insert(spacerWidth / 2, "\u2534")
                : spacer;
            
            var bottomLine = joinDown
                ? spacer.Remove(spacerWidth / 2, 1).Insert(spacerWidth / 2, "\u252c")
                : spacer;
            
            yield return $"\u250c{topLine}\u2510";

            foreach
            (
                var entry in
                Entries
                    .SelectMany(x => x
                        .RenderLines(ContentWidth)
                    )
            )
            {
                yield return $"\u2502 {entry.PadRight(ContentWidth)} \u2502";
            }
            
            yield return $"\u2514{bottomLine}\u2518";
        }

        public override string ToString()
        {
            var line = string.Empty.PadLeft(BoxWidth - 2, '\u2500');

            return
                $$"""
                  ┌{{line}}┐
                  {{
                      string.Join(
                          Environment.NewLine,
                          Entries
                              .SelectMany(x => x
                                  .RenderLines(ContentWidth)
                                  .Select(line =>
                                      $"\u2502 {line.PadRight(ContentWidth)} \u2502"
                                  )
                              )
                      )
                  }}
                  └{{line}}┘
                  """;
        }
    }

    public abstract class Entry
    {
        public abstract string Render(int containerWidth);

        public string[] RenderLines(int containerWidth)
            => Render(containerWidth).Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries);

        public abstract int ContentWidth { get; }
        public abstract int ContentHeight { get; }

        public static Entry Keys(params (string, string?)[] entries)
            => new KeysEntry(entries);

        public static Entry Text(string content, bool alignCenter = false)
            => new TextEntry(content, alignCenter);

        private sealed class TextEntry(string Content, bool AlignCenter = false) : Entry
        {
            public override string Render(int containerWidth)
            {
                if (AlignCenter)
                {
                    var diff = containerWidth - Content.Length;
                    var lPad = diff == 0 ? 0 : diff / 2;
                    var rPad = diff == 0 ? 0 : (int) Math.Ceiling(diff / 2d);

                    return $"{string.Empty.PadLeft(lPad)}{Content}{string.Empty.PadRight(rPad)}";
                }

                return Content.PadRight(containerWidth);
            }

            public override int ContentWidth => Content.Length;

            public override int ContentHeight => 1;
        }

        private sealed class KeysEntry : Entry
        {
            public override int ContentHeight => _entries.Length;

            public override int ContentWidth { get; }

            public int KeyPad { get; }
            public int ValuePad { get; }

            private readonly (string Key, string Value)[] _entries;
            private readonly string _separator;

            public KeysEntry((string Key, string? Value)[] entries, string separator = "->")
            {
                _entries = entries.Where(x => x.Value is not null).ToArray()!;
                _separator = separator;
                KeyPad = _entries.Select(x => x.Key.Length).Max();
                ValuePad = _entries.Select(x => x.Value.Length).Max();

                ContentWidth = KeyPad + 1 + separator.Length + 1 + ValuePad;
            }

            public override string Render(int containerWidth)
            {
                var builder = new StringBuilder();
                var pad = containerWidth - ContentWidth;

                foreach (var (key, value) in _entries)
                {
                    builder.AppendLine(
                        $"{key.PadRight(KeyPad)} {_separator.PadRight(pad)} {value.PadLeft(ValuePad)}"
                    );
                }

                return builder.ToString().TrimEnd(Environment.NewLine.ToCharArray());
            }
        }
    }
}