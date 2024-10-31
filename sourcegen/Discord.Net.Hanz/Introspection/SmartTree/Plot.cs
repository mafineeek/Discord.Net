using System.Text;

namespace Discord.Net.Hanz.Introspection;

public sealed class Plot
{
    public List<Column> Columns => _currentRow.Columns;

    private readonly Dictionary<Tree, Column> _treeTable = [];

    public sealed class RowBuilder
    {
        private readonly Dictionary<Column, RenderedText> _texts = [];
        private readonly Dictionary<Column, RenderedText> _result = [];

        public bool TryGetValue(Column column, out RenderedText text)
            => _result.TryGetValue(column, out text) || _texts.TryGetValue(column, out text);

        public bool HasRendered(Column column)
            => _texts.ContainsKey(column);

        public void AddText(Column column, RenderedText text)
            => _texts[column] = text;

        public void AddResult(Column column, RenderedText text)
            => _result[column] = text;

        public void RemoveFromFinalGraph(Column column) => _result.Remove(column);

        public void RemoveFromFinalGraph(IEnumerable<Column> columns)
        {
            foreach (var column in columns)
            {
                _result.Remove(column);
            }
        }

        public bool TryGetResults(IEnumerable<Column> columns, out List<RenderedText> values)
            => TryGetInternal(_result, columns, out values);

        public bool TryGetTexts(IEnumerable<Column> columns, out List<RenderedText> values)
            => TryGetInternal(_result, columns, out values);

        private bool TryGetInternal(
            Dictionary<Column, RenderedText> dict,
            IEnumerable<Column> columns,
            out List<RenderedText> values
        )
        {
            values = [];

            foreach (var column in columns)
            {
                if (!dict.TryGetValue(column, out var text))
                    return false;

                values.Add(text);
            }

            return true;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();

            sb.AppendLine(
                RenderedText.AlignHorizontally(_result.Values.ToArray()).ToString()
            );

            return sb.ToString();
        }
    }

    public sealed class Row
    {
        public List<Column> Columns { get; } = [];

        public override string ToString()
        {
            return RenderedText
                .AlignHorizontally(
                    Columns
                        .Select(x => x
                            .RenderOld()
                        )
                        .ToArray()
                )
                .ToString();

            var builder = new RowBuilder();

            foreach (var column in Columns)
            {
                column.RenderContent(builder);
            }

            foreach (var column in Columns)
            {
                column.Render(builder);
            }

            return builder.ToString();
        }

        public bool TryGetColumnForTree(Tree tree, out Column column)
            => (column = Columns.FirstOrDefault(x => x.Contains(tree))) is not null;

        public bool Replace(Column old, Column newColumn)
        {
            var index = Columns.IndexOf(old);

            if (index == -1) return false;

            Columns.Insert(index, newColumn);
            return Columns.Remove(old);
        }
    }

    private Row _currentRow = new();

    public Row GetCurrentRowAndCreateNew()
    {
        var row = _currentRow;

        _currentRow = new();

        return row;
    }

    public Column CreateColumnFor(Tree tree)
    {
        var column = new Column(this, tree);
        Columns.Add(column);
        return column;
    }

    public Column MergeColumnsFor(Tree tree, params Column[] columns)
    {
        var column = new Column(
            this,
            tree,
            columns.SelectMany(x => x.Roots).Distinct(),
            columns,
            depth: columns.Max(x => x.Depth) + 1
        );

        return column;
    }

    public bool TryGetColumnFor(Tree tree, out Column column)
        => _treeTable.TryGetValue(tree, out column);

    public sealed class Column
    {
        public int TextHeight
            => ValuesHeight + SubColumns.Sum(x => x.TextHeight);

        public int ValuesHeight
            => _values.Sum(x => x.Height);

        public int ValuesWidth
            => _values.Count == 0 ? 0 : _values.Max(x => x.Width);

        public int TextWidth
            => Math.Max(
                ValuesWidth,
                SubColumns.Count == 0 ? 0 : SubColumns.Max(x => x.TextWidth)
            );

        public int Width => SubColumns.Sum(x => x.Width) + 1;

        public IEnumerable<Column> Roots => _roots.Count == 0 ? [this] : _roots;

        public int Depth { get; }

        public Plot Plot { get; }

        private readonly List<RenderedText> _values = [];
        public List<Column> SubColumns { get; }
        public List<Column> ParentColumns { get; }
        private readonly List<Column> _roots;

        public Tree? Tree { get; }

        private readonly bool? _rendersParents;

        public Column(
            Plot plot,
            Tree? tree,
            IEnumerable<Column>? roots = null,
            IEnumerable<Column>? parents = null,
            bool? renderParents = null,
            int depth = 0)
        {
            _rendersParents = renderParents;
            Plot = plot;
            Depth = depth;
            Tree = tree;
            _roots = roots?.ToList() ?? [];
            ParentColumns = parents?.ToList() ?? [];
            SubColumns = [];

            if (tree is not null)
                Plot._treeTable[tree] = this;
        }

        public bool Contains(Tree tree)
            => IsTreeInColumnOrParentColumn(tree) || IsTreeInColumnOrSubColumns(tree);

        public bool IsTreeInColumnOrParentColumn(Tree tree)
        {
            return Tree == tree || ParentColumns.Any(y => y.IsTreeInColumnOrParentColumn(tree));
        }

        public bool IsTreeInColumnOrSubColumns(Tree tree)
        {
            return Tree == tree || SubColumns.Any(y => y.IsTreeInColumnOrSubColumns(tree));
        }

        public void Add(RenderedText value)
        {
            _values.Add(value);
        }

        public void Set(params RenderedText[] values)
        {
            _values.Clear();
            _values.AddRange(values);
        }

        public Column CreateSubColumnForTree(Tree tree)
        {
            var subColumn = new Column(Plot, tree, _roots, [this], depth: Depth + 1);
            SubColumns.Add(subColumn);
            return subColumn;
        }

        public Column CreateSubColumnForValue(RenderedText value, bool? renderParents = null)
        {
            var column = new Column(Plot, null, Roots, [this], renderParents, depth: Depth + 1);
            column.Add(value);
            SubColumns.Add(column);
            return column;
        }

        public void RenderContent(RowBuilder builder)
        {
            foreach (var parent in ParentColumns)
                parent.RenderContent(builder);

            if (builder.HasRendered(this)) return;

            builder.AddText(
                this,
                new RenderedText(
                    _values.SelectMany(x => x.Lines)
                )
            );
        }

        public void Render(RowBuilder builder)
        {
            //Debugger.Launch();

            // if (!builder.TryGetValue(this, out var content)) return;
            //
            // foreach (var parent in ParentColumns)
            //     parent.Render(builder);
            //
            // if (ParentColumns.Count > 0 && builder.TryGetValues(ParentColumns, out var parents))
            // {
            //     builder.RemoveFromFinalGraph(ParentColumns);
            //     content = RenderedText.JoinParents(parents.ToArray(), content, true);
            //     builder.AddText(this, content);
            // }
            //
            // switch (SubColumns.Count)
            // {
            //     case 0:
            //         builder.AddResult(this, content);
            //         break;
            //     // case > 0 when builder.TryGetValues(SubColumns, out var children):
            //     //     builder.RemoveFromFinalGraph(SubColumns);
            //     //     content = RenderedText.JoinChildren(content, children.ToArray());
            //     //     builder.AddText(this, content);
            //     //     break;
            // }


            var content = new RenderedText(
                _values.SelectMany(x => x.Lines)
            );

            builder.AddText(this, content);

            switch (SubColumns.Count)
            {
                case 0: break;
                case 1: break;
                case > 1:
                    if (!builder.TryGetResults(SubColumns, out var subColumnsRender))
                        return;

                    builder.RemoveFromFinalGraph(SubColumns);

                    builder.AddResult(
                        this,
                        RenderedText.JoinChildren(
                            content,
                            subColumnsRender.ToArray()
                        )
                    );
                    break;
            }

            foreach (var parent in ParentColumns)
                parent.Render(builder);

            switch (ParentColumns.Count)
            {
                case 1:
                    break;
                case > 1:

                    break;
            }
        }

        public RenderedText RenderOld()
        {
            var content = new RenderedText(_values.SelectMany(x => x.Lines));

            if (ParentColumns.Count > 0 && (Tree is not null || _rendersParents == true))
            {
                content = RenderedText.JoinParents(
                    ParentColumns.Select(x => x.RenderOld()).ToArray(),
                    new(content.Lines)
                );
            }

            return content;
        }

        public void AttachChild(Column column)
        {
            SubColumns.Add(column);
            column.ParentColumns.Add(this);
        }

        public void Detach()
        {
            DetachFromChildren();
            DetachFromParents();
        }

        public void DetachFromParents()
        {
            foreach (var parent in ParentColumns)
            {
                parent.SubColumns.Remove(this);
            }

            ParentColumns.Clear();
        }

        public void DetachFromChildren()
        {
            foreach (var child in SubColumns)
            {
                child.ParentColumns.Remove(this);
            }

            SubColumns.Clear();
        }

        public bool SearchForTree(Tree tree, out Column column)
            => SearchSubColumnsForTree(tree, out column) || SearchParentColumnsForTree(tree, out column);

        private bool SearchSubColumnsForTree(Tree tree, out Column column)
        {
            if (tree == Tree)
            {
                column = this;
                return true;
            }

            foreach (var child in SubColumns)
            {
                if (child.SearchSubColumnsForTree(tree, out column))
                    return true;
            }

            column = null!;
            return false;
        }

        private bool SearchParentColumnsForTree(Tree tree, out Column column)
        {
            if (tree == Tree)
            {
                column = this;
                return true;
            }

            foreach (var parent in ParentColumns)
            {
                if (parent.SearchParentColumnsForTree(tree, out column))
                    return true;
            }

            column = null!;
            return false;
        }
    }
}