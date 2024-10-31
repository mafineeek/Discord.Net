namespace Discord.Net.Hanz.Introspection;

public abstract class NodeRenderer
{
    public int Width { get; }
    public int Height { get; }

    public virtual void Visit(Builder builder){}

    protected abstract void Render(Builder builder);

    protected class Column
    {
        private List<NodeRenderer> _renderers;

        private RenderedText _text;
        
        public Column(NodeRenderer root)
        {
            _renderers = [root];
            _text = RenderedText.Empty;
        }
    }
    
    public class Builder
    {
        private readonly List<Column> _row = [];

        private readonly Dictionary<Tree, HashSet<NodeRenderer>> _dependencies = [];

        private readonly List<NodeRenderer> _nodes;
        private readonly Dictionary<Tree, TreeRenderer> _table;

        public Builder(IEnumerable<NodeRenderer> nodes)
        {
            _nodes = nodes.ToList();
            
            _table = nodes
                .OfType<TreeRenderer>()
                .ToDictionary(x => x.Tree);
        }

        public static Builder Create(IEnumerable<Tree> trees)
        {
            var builder = new Builder(trees.Select(TreeRenderer.Create));

            return builder;
        }

        public void Visit()
            => _nodes.ForEach(x => x.Visit(this));

        private TreeRenderer AddNewTree(Tree tree)
        {
            if (_table.ContainsKey(tree))
                return _table[tree];

            var renderer = _table[tree] = TreeRenderer.Create(tree);

            renderer.Visit(this);

            return renderer;
        }

        public void AddDependency(Tree tree, NodeRenderer dependent)
        {
            if (!_table.TryGetValue(tree, out var renderer))
                renderer = AddNewTree(tree);

            if (!_dependencies.TryGetValue(tree, out var dependencies))
                _dependencies[tree] = dependencies = new();

            dependencies.Add(dependent);
        }
        
        public void AddDependencies(IEnumerable<Tree> trees, NodeRenderer dependent)
        {
            foreach (var tree in trees)
            {
                AddDependency(tree, dependent);
            }
        }
    }

    private abstract class TreeRenderer : NodeRenderer
    {
        public Tree Tree { get; }

        public TreeRenderer(Tree? tree)
        {
            Tree = tree;
        }
        
        public static TreeRenderer Create(Tree tree)
        {
            switch (tree.Inputs.Count, tree.Consumers.Count)
            {
                case (0, _):
                    return new RootRenderer(tree);
                case (<= 1, > 1):
                    return new SplitRenderer(tree);
                case (> 1, <= 1):
                    return new CombinativeRenderer(tree);
                case (> 1, > 1):
                    return new SingularityRenderer(tree);
                default:
                    return new ScalarRenderer(tree);
            }
        }
    }

    private class ValueRenderer : NodeRenderer
    {
        private readonly RenderedText _text;

        public ValueRenderer(RenderedText text) : base()
        {
            _text = text;
        }

        protected override void Render(Builder builder)
        {
            
        }
    }

    private class SplitRenderer : TreeRenderer
    {
        public SplitRenderer(Tree tree) : base(tree)
        {
        }

        public override void Visit(Builder builder)
        {
            builder.AddDependencies(Tree.Consumers, this);
            builder.AddDependencies(Tree.Inputs, this);
        }

        protected override RenderedText Render(Builder builder)
        {
            throw new NotImplementedException();
        }
    }

    private class CombinativeRenderer : TreeRenderer
    {
        public CombinativeRenderer(Tree? tree) : base(tree)
        {
        }

        public override void Visit(Builder builder)
        {
            builder.AddDependencies(Tree.Consumers, this);
            builder.AddDependencies(Tree.Inputs, this);
        }
    }
    
    private class SingularityRenderer : TreeRenderer
    {
        public SingularityRenderer(Tree? tree) : base(tree)
        {
        }
        
        public override void Visit(Builder builder)
        {
            builder.AddDependencies(Tree.Consumers, this);
            builder.AddDependencies(Tree.Inputs, this);
        }
    }

    private class RootRenderer : TreeRenderer
    {
        public RootRenderer(
            Tree tree
        ) : base(tree)
        {
        }

        public override void Visit(Builder builder)
        {
            builder.AddDependencies(Tree.Consumers, this);
            builder.AddDependencies(Tree.Inputs, this);
        }
    }

    private class ScalarRenderer : TreeRenderer
    {
        public ScalarRenderer(
            Tree tree
        ) : base(tree)
        {
        }

        public override void Visit(Builder builder)
        {
            builder.AddDependencies(Tree.Consumers, this);
            builder.AddDependencies(Tree.Inputs, this);
        }
    }
}