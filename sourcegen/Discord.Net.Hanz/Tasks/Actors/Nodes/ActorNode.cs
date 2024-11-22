using System.Collections.Immutable;
using Discord.Net.Hanz.Nodes;
using Discord.Net.Hanz.Utils.Bakery;
using Microsoft.CodeAnalysis;

namespace Discord.Net.Hanz.Tasks.Actors.Nodes;

public sealed partial class ActorNode : Node
{
    public readonly record struct BuildState(
        ActorInfo ActorInfo,
        ActorAncestralInfo AncestralInfo
    ) : IHasActorInfo
    {
        public TypePath Path { get; } = new(new([(typeof(ActorNode), ActorInfo.Actor.DisplayString)]));

        public bool RedefinesRootInterfaceMemebrs =>
            !ActorInfo.IsCore || AncestralInfo.EntityAssignableAncestors.Count > 0;
    }


    public readonly record struct ActorAncestralInfo(
        string Actor,
        ImmutableEquatableArray<string> EntityAssignableAncestors,
        ImmutableEquatableArray<string> Ancestors
    );

    public IncrementalValuesProvider<BuildState> BuildStateProvider { get; }
    public IncrementalValuesProvider<StatefulGeneration<BuildState>> ContainersProvider { get; }

    public ActorNode(
        IncrementalGeneratorInitializationContext context,
        Logger logger
    ) : base(context, logger)
    {
        BuildStateProvider =
            GetTask<ActorsTask>(context)
                .Actors
                .Collect()
                .SelectMany(MapAncestralInfo)
                .KeyedBy(x => x.Actor)
                .Pair(GetTask<ActorsTask>(context).ActorInfos)
                .Select((ancestors, info) => new BuildState(info, ancestors));

        ContainersProvider = BuildStateProvider.Select(CreateActorContainer);

        CreateLinks(context);
        CreateAliases(context);
        CreateRelationships(context);
    }

    private static IEnumerable<ActorAncestralInfo> MapAncestralInfo(
        ImmutableArray<ActorsTask.ActorSymbols> actors,
        CancellationToken token)
    {
        return from context in actors
            let ancestors = actors.Where(x =>
                Hierarchy.Implements(context.Actor, x.Actor)
            ).ToArray()
            select new ActorAncestralInfo(
                context.Actor.ToDisplayString(),
                ancestors
                    .Where(x =>
                        x.Entity.Equals(context.Entity, SymbolEqualityComparer.Default) ||
                        Hierarchy.Implements(context.Entity, x.Entity)
                    )
                    .Select(x => x.Actor.ToDisplayString())
                    .ToImmutableEquatableArray(),
                ancestors.Select(x => x.Actor.ToDisplayString()).ToImmutableEquatableArray()
            );
    }

    private StatefulGeneration<BuildState> CreateActorContainer(BuildState state, CancellationToken token)
        => new(
            state,
            TypeSpec.From(state.ActorInfo.Actor) with
            {
                Modifiers = new(["partial"]),
            }
        );

    public static StatefulGeneration<T> CreateActorContainer<T>(ActorInfo info, T state)
        => new(
            state, TypeSpec.From(info.Actor) with
            {
                Modifiers = new(["partial"]),
            }
        );
}