using System.Collections.Immutable;
using System.Diagnostics;
using Discord.Net.Hanz.Tasks.Actors.Links.V5.Nodes.Common;
using Discord.Net.Hanz.Utils.Bakery;
using Microsoft.CodeAnalysis;

namespace Discord.Net.Hanz.Tasks.Actors.Links.V5.Nodes;

public abstract class Node
{
    private static readonly Dictionary<Type, Node> _nodes = [];

    private static Logger _nodeLogger = Net.Hanz.Logger.CreateForTask("LinkNodes");

    private readonly NodeProviders _providers;

    protected Logger Logger { get; }

    protected Node(NodeProviders providers, Logger logger)
    {
        Logger = logger;
        _providers = providers;
    }

    public readonly record struct StatefulGeneration<TState>(
        TState State,
        TypeSpec Spec
    )
    {
        public StatefulGeneration<TNewState> Mutate<TNewState>(TNewState state)
            => new(state, Spec);
    }

    protected IncrementalValuesProvider<Branch<StatefulGeneration<TState>>> NestTypesViaPaths<TState>(
        IncrementalValuesProvider<Branch<StatefulGeneration<TState>>> provider
    ) where TState : IPathedState
    {
        return provider.Collect().SelectMany(MapGraphs).SelectMany(BuildGraph);
    }

    private IEnumerable<StatefulGeneration<TState>> BuildGraph<TState>(
        Graph<TState> graph,
        CancellationToken token
    ) where TState : IPathedState
    {
        using var logger = Logger
            .GetSubLogger(nameof(BuildGraph))
            .GetSubLogger(GetType().Name);

        var result = new List<StatefulGeneration<TState>>();
        var stack = new Stack<StatefulGeneration<TState>>();

        foreach (var generation in graph.Links)
        {
            var (state, spec) = generation;

            if (stack.Count == 0)
            {
                stack.Push(generation);
                logger.Log($"stack: += {spec.Name} -> {state.Path}");
                continue;
            }

            var pathDepth = state.Path.CountOfType(GetType());

            if (pathDepth == 1)
            {
                logger.Log($"stack: building tree size of {stack.Count}");
                BuildTree();
            }

            logger.Log($"stack: += {spec.Name} -> {state.Path}");
            stack.Push(generation);
        }

        if (stack.Count > 0)
            BuildTree();

        return result;

        void BuildTree()
        {
            start:

            var part = stack.Pop();
            logger.Log($" - part = {part.Spec.Name}");

            var group = new List<TypeSpec>() {part.Spec};
            var deferred = new Queue<StatefulGeneration<TState>>();

            if (stack.Count == 0)
            {
                logger.Log("   - single size tree added to result");
                result.Add(part);
                return;
            }

            while (stack.Count > 0)
            {
                var previous = stack.Pop();
                logger.Log($" - prev = {previous.Spec.Name}");

                if (deferred.Count > 0)
                {
                    for (var i = deferred.Count; i > 0 && deferred.Count > 0; i--)
                    {
                        var deferredPart = deferred.Dequeue();

                        if (previous.State.Path.IsParentTo(deferredPart.State.Path))
                        {
                            logger.Log($"   - deferred {deferredPart.Spec.Name} added to prev");
                            previous = previous with {Spec = previous.Spec.AddNestedType(deferredPart.Spec)};
                            continue;
                        }

                        deferred.Enqueue(deferredPart);
                    }
                }

                if (previous.State.Path.IsParentTo(part.State.Path))
                {
                    logger.Log($"   - {group.Count} types are children to previous type {previous.State.Path}");

                    previous = previous with {Spec = previous.Spec.AddNestedTypes(group)};

                    group.Clear();

                    if (previous.State.Path.CountOfType(GetType()) == 1)
                    {
                        logger.Log("     - previous is a root, adding to results");
                        // its a root
                        result.Add(previous);

                        if (stack.Count > 0)
                        {
                            logger.Log("     - stack has more elements, reiterating...");
                            goto start;
                        }

                        break;
                    }

                    if (stack.Count == 0)
                    {
                        // we have a non root, with no roots on the stack, this is an error
                        logger.Warn($"non-root {previous.Spec.Name}: {previous.State.Path} has no parent on stack");
                        throw new InvalidOperationException(
                            $"non-root {previous.Spec.Name}: {previous.State.Path} has no parent on stack"
                        );
                    }

                    logger.Log($"   - part <- {previous.Spec.Name}");
                    // set the part we're working with to the modified previous
                    part = previous;
                    group.Add(part.Spec);
                    continue;
                }

                // they share the same parent
                if (-previous.State.Path == -part.State.Path)
                {
                    if (previous.State.Path.CountOfType(GetType()) == 1)
                    {
                        // should not happen
                        logger.Warn(
                            $"dual-root stack {previous.Spec.Name}: {previous.State.Path} | {part.Spec.Name}: {part.State.Path}");
                        throw new InvalidOperationException(
                            $"non-root {previous.Spec.Name}: {previous.State.Path} has no parent on stack"
                        );
                    }

                    group.Add(previous.Spec);
                    logger.Log($"   - group += {previous.Spec.Name} ({group.Count})");
                    continue;
                }

                if ((previous.State.Path & part.State.Path).CountOfType(GetType()) >= 1)
                {
                    logger.Log($"   - deferring {part.Spec.Name}: {part.State.Path}");

                    // the two parts share a common ancestor in the graph.
                    // we can defer the part until the ancestor appears.
                    deferred.Enqueue(part);

                    part = previous;
                    group.Clear();
                    group.Add(part.Spec);
                    logger.Log($"   - part <- {previous.Spec.Name}");

                    continue;
                }

                // we cant do anything with this node
                logger.Warn(
                    $"Unknown node: {previous.Spec.Name}: {previous.State.Path} | {part.Spec.Name}: {part.State.Path}");
            }

            if (deferred.Count > 0)
            {
                logger.Log($" - pushing {deferred.Count} deferred nodes onto the stack");

                while (deferred.Count > 0)
                {
                    var deferredPart = deferred.Dequeue();
                    stack.Push(deferredPart);
                    logger.Log($"   += {deferredPart.Spec.Name}: {deferredPart.State.Path}");
                }
            }
        }
    }

    private IEnumerable<Branch<Graph<TState>>> MapGraphs<TState>(
        ImmutableArray<Branch<StatefulGeneration<TState>>> states,
        CancellationToken token
    ) where TState : IPathedState
    {
        foreach (var link in states.GroupBy(x => x.SourceVersion))
        {
            yield return new Branch<Graph<TState>>(
                link.Key,
                new Graph<TState>(link.Select(x => x.Value).ToImmutableEquatableArray())
            );
        }
    }

    private readonly record struct Graph<TState>(
        ImmutableEquatableArray<StatefulGeneration<TState>> Links
    ) where TState : IPathedState;

    protected IncrementalValuesProvider<Branch<TResult>> AddNestedTypes<TSource, TState, TIn, TResult>(
        INestedTypeProducerNode<TIn> node,
        IncrementalValuesProvider<Branch<TSource>> provider,
        Func<TSource, CancellationToken, TIn> parameterMapper,
        Func<TSource, ImmutableArray<TypeSpec>, CancellationToken, TResult> resultMapper,
        Func<TSource, TState> stateMapper
    )
    {
        return node
            .Create(
                provider
                    .Select((branch, token) => branch
                        .CreateNestedBranch(
                            (parameterMapper(branch.Value, token), stateMapper(branch.Value))
                        )
                    )
            )
            .Collect(
                provider,
                (source, spec, token) => source.Mutate(resultMapper(source.Value, spec, token))
            );
    }

    protected IncrementalValuesProvider<Branch<StatefulGeneration<TState>>> AddNestedTypes<TState, TIn>(
        IncrementalValuesProvider<Branch<StatefulGeneration<TState>>> provider,
        Func<TState, CancellationToken, TIn> parameterMapper,
        params INestedTypeProducerNode<TIn>[] nodes
    )
    {
        return provider
            .Select((branch, token) => branch
                .CreateNestedBranch(
                    (parameterMapper(branch.Value.State, token), branch.Value.State)
                )
            )
            .ForEach(
                nodes,
                (provider, node) => node.Create(provider)
            )
            .Collect(
                provider,
                (branch, specs, _) =>
                    branch.Mutate(branch.Value with {Spec = branch.Value.Spec.AddNestedTypes(specs)})
            );
    }

    protected IncrementalValuesProvider<StatefulGeneration<TState>> AddNestedTypes<TState, TIn>(
        IncrementalValuesProvider<StatefulGeneration<TState>> provider,
        Func<TState, CancellationToken, TIn> parameterMapper,
        params INestedTypeProducerNode<TIn>[] nodes
    )
    {
        return provider
            .Branch()
            .Select(
                (branch, token) =>
                    branch.Mutate(
                        (parameterMapper(branch.Value.State, token), branch.Value.State)
                    )
            )
            .ForEach(
                nodes,
                (provider, node) => node.Create(provider)
            )
            .Collect(
                provider,
                (source, specs, _) =>
                    source with {Spec = source.Spec.AddNestedTypes(specs)}
            );
    }

    protected IncrementalValuesProvider<StatefulGeneration<TState>> AddNestedTypes<TState, TIn>(
        INestedTypeProducerNode<TIn> node,
        IncrementalValuesProvider<StatefulGeneration<TState>> provider,
        Func<TState, CancellationToken, TIn> parameterMapper
    )
    {
        return node
            .Create(
                provider
                    .Branch()
                    .Select(
                        (branch, token) =>
                            branch.Mutate(
                                (parameterMapper(branch.Value.State, token), branch.Value.State)
                            )
                    )
            )
            .Collect(
                provider,
                (source, specs, _) =>
                    source with {Spec = source.Spec.AddNestedTypes(specs)}
            );
    }

    protected IncrementalValuesProvider<Branch<TResult>> Branch<TResult, TIn, TOut>(
        IncrementalValuesProvider<Branch<TIn>> provider,
        Func<TIn, ImmutableArray<TOut>, CancellationToken, TResult> mapper,
        params IBranchNode<TIn, TOut>[] nodes
    )
    {
        return provider
            .ForEach(
                nodes,
                (branch, node) => node.Branch(branch)
            )
            .Collect(
                provider,
                (branch, results, token) => branch.Mutate(mapper(branch.Value, results, token))
            );
    }

    protected IncrementalValuesProvider<TResult> Branch<TResult, TIn, TOut>(
        IncrementalValuesProvider<TIn> provider,
        Func<TIn, ImmutableArray<TOut>, CancellationToken, TResult> mapper,
        params IBranchNode<TIn, TOut>[] nodes
    )
    {
        return provider
            .Branch()
            .ForEach(
                nodes,
                (branch, node) => node.Branch(branch)
            )
            .Collect(
                provider,
                mapper
            );
    }

    public static void Initialize(
        NodeProviders providers
    )
    {
        _nodes.Clear();

        foreach
        (
            var node in
            typeof(Node)
                .Assembly
                .GetTypes()
                .Where(x =>
                    typeof(Node).IsAssignableFrom(x) && x.IsClass && !x.IsAbstract
                )
        )
        {
            if (_nodes.ContainsKey(node)) continue;
            GetInstance(node, providers);
        }
    }

    protected TNode GetInstance<TNode>()
        where TNode : Node
        => GetInstance<TNode>(_providers);

    private static TNode GetInstance<TNode>(NodeProviders providers)
        where TNode : Node
        => (TNode) GetInstance(typeof(TNode), providers);

    private static Node GetInstance(Type type, NodeProviders providers)
    {
        if (!_nodes.TryGetValue(type, out var node))
            _nodes[type] = node = (Node) Activator.CreateInstance(
                type,
                providers,
                _nodeLogger.GetSubLogger(type.Name).WithCleanLogFile()
            );

        return node;
    }

    protected static string GetFriendlyName(TypeRef type, bool forceInterfaceRules = false)
    {
        var name = type.Name;

        if (forceInterfaceRules || type.TypeKind is TypeKind.Interface)
            name = type.Name.Remove(0, 1);

        return name
            .Replace("Trait", string.Empty)
            .Replace("Actor", string.Empty)
            .Replace("Gateway", string.Empty)
            .Replace("Rest", string.Empty);
    }
}

public interface INestedNode
{
    IncrementalValuesProvider<Node.StatefulGeneration<TState>> From<TState>(
        IncrementalValuesProvider<Node.StatefulGeneration<TState>> provider
    ) where TState : IHasActorInfo;
}

public interface INestedNode<TState>
    where TState : IHasActorInfo
{
    IncrementalValuesProvider<Node.StatefulGeneration<TState>> From(
        IncrementalValuesProvider<Node.StatefulGeneration<TState>> provider
    );
}

public interface IBranchNode<TIn, TOut>
{
    IncrementalValuesProvider<Branch<TOut>> Branch(
        IncrementalValuesProvider<Branch<TIn>> provider
    );
}

public readonly record struct NestedTypeProducerContext(
    ActorInfo ActorInfo,
    TypePath Path
);

public interface INestedTypeProducerNode : INestedTypeProducerNode<NestedTypeProducerContext>;

public interface INestedTypeProducerNode<TParameters>
{
    IncrementalValuesProvider<Branch<TypeSpec>> Create<TSource>(
        IncrementalValuesProvider<Branch<(TParameters Parameters, TSource Source)>> provider
    );
}