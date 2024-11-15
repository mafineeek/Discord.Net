using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using Discord.Net.Hanz.Tasks.Actors.Links.V5.Nodes.Common;
using Discord.Net.Hanz.Tasks.Actors.V3;
using Discord.Net.Hanz.Utils.Bakery;
using Microsoft.CodeAnalysis;

namespace Discord.Net.Hanz.Tasks.Actors.Links.V5.Nodes.Types;

public class LinkNode :
    Node,
    INestedTypeProducerNode
{
    public readonly record struct State(
        ActorInfo ActorInfo,
        TypePath Path,
        LinkSchematics.Entry Entry
    ) : IPathedState
    {
        public bool IsTemplate { get; } = !Path.Contains<LinkNode>();

        public TypePath Path { get; } = Path.Add<LinkNode>(Entry.Type.ReferenceName);
    }


    private readonly IncrementalValueProvider<ImmutableArray<LinkSchematics.Schematic>> _schematics;

    public LinkNode(NodeProviders providers, Logger logger) : base(providers, logger)
    {
        _schematics = providers.Schematics.Collect();
    }

    public IncrementalValuesProvider<Branch<TypeSpec>> Create<TSource>(
        IncrementalValuesProvider<Branch<(NestedTypeProducerContext Parameters, TSource Source)>> provider)
    {
        var stateProvider = provider
            .Select((x, _) => x.Parameters)
            .Combine(_schematics)
            .SelectMany((tuple, token) =>
                CreateState(
                    tuple.Left.Value,
                    tuple.Right,
                    token
                ).Select(x =>
                    tuple.Left.Mutate(x)
                )
            );

        var implementationProvider = Branch(
            stateProvider,
            CreateLinkType,
            GetInstance<IndexableNode>(),
            GetInstance<EnumerableNode>(),
            GetInstance<DefinedNode>(),
            GetInstance<PagedNode>()
        );

        var nestedProvider = AddNestedTypes(
            implementationProvider,
            (state, _) => new NestedTypeProducerContext(state.ActorInfo, state.Path),
            GetInstance<BackLinkNode>(),
            GetInstance<HierarchyNode>(),
            GetInstance<ExtensionNode>()
        );

        return NestTypesViaPaths(nestedProvider).Select((x, _) => x.Spec);
    }

    private StatefulGeneration<State> CreateLinkType(
        State state,
        ImmutableArray<ILinkImplmenter.LinkImplementation> implementations,
        CancellationToken token
    )
    {
        var spec = TypeSpec
            .From(state.Entry.Type)
            .AddModifiers("new");

        foreach (var implementation in implementations)
        {
            implementation.Interface.Apply(ref spec);
        }

        if (state.IsTemplate)
        {
            spec = spec.AddBases(
                $"{state.ActorInfo.Actor}.Link"
            );

            switch (state.ActorInfo.Assembly)
            {
                case ActorsTask.AssemblyTarget.Core:
                    spec = spec.AddBases(
                        $"{state.ActorInfo.FormattedLinkType}.{state.Path.FormatRelative()}"
                    );
                    break;
                case ActorsTask.AssemblyTarget.Rest:
                    spec = spec.AddBases(
                        state.ActorInfo.FormattedRestLinkType
                    );
                    break;
            }
        }
        else if(state.Path.First.HasValue)
        {
            spec = spec.AddBases([..state.Path.First.Value + state.Path.Slice(1).SemanticalProduct()]);
        }

        return new StatefulGeneration<State>(
            state,
            spec
        );
    }

    private static IEnumerable<State> CreateState(
        NestedTypeProducerContext context,
        ImmutableArray<LinkSchematics.Schematic> schematics,
        CancellationToken token
    )
    {
        foreach
        (
            var state in
            from schematic in schematics
            from entry in schematic.Root.Children
            from state in CreateStateForEntry(entry, context.Path)
            select state
        ) yield return state;

        yield break;

        IEnumerable<State> CreateStateForEntry(
            LinkSchematics.Entry entry,
            TypePath path)
        {
            var state = new State(context.ActorInfo, path, entry);

            yield return state;

            foreach (var child in entry.Children)
            foreach (var childState in CreateStateForEntry(child, path.Add<LinkNode>(entry.Type.ReferenceName)))
                yield return childState;
        }
    }
}