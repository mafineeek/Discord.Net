using System.Collections.Immutable;
using Discord.Net.Hanz.Nodes;
using Discord.Net.Hanz.Tasks.Actors.Links.Nodes.Modifiers;
using Discord.Net.Hanz.Tasks.Actors.Links.Nodes.Types;
using Discord.Net.Hanz.Utils.Bakery;
using Microsoft.CodeAnalysis;

namespace Discord.Net.Hanz.Tasks.Actors.Nodes;

public sealed partial class ActorNode
{
    public readonly record struct IntrospectedBuildState(
        ActorNode.BuildState BuildState,
        ImmutableEquatableArray<IntrospectedBuildState> Ancestors,
        ImmutableEquatableArray<IntrospectedBuildState> EntityAssignableAncestors
    ) : IHasActorInfo
    {
        public ActorInfo ActorInfo => BuildState.ActorInfo;

        public bool RedefinesRootInterfaceMemebrs =>
            !BuildState.ActorInfo.IsCore || EntityAssignableAncestors.Count > 0;
    }
    
    private void CreateLinks(IncrementalGeneratorInitializationContext context)
    {
        var buildProvider = AddNestedTypes(
                ContainersProvider,
                (state, _) => new(state.ActorInfo, state.Path),
                GetNode<HierarchyNode>(),
                GetNode<BackLinkNode>(),
                GetNode<ExtensionNode>(),
                GetNode<LinkTypeNode>()
            )
            .Collect()
            .SelectMany(Introspect)
            .Select(CreateLinkInterface);

        context.RegisterSourceOutput(
            buildProvider,
            (sourceContext, generation) => sourceContext.AddSource(
                $"Links/{generation.State.ActorInfo.Actor.MetadataName}",
                $$"""
                  using Discord;
                  using Discord.Models;
                  using MorseCode.ITask;

                  namespace {{generation.State.ActorInfo.Actor.Namespace}};

                  #pragma warning disable CS0108
                  #pragma warning disable CS0109
                  {{generation.Spec}}
                  #pragma warning restore CS0108
                  #pragma warning restore CS0109
                  """
            )
        );
    }

    private static IEnumerable<Node.StatefulGeneration<IntrospectedBuildState>> Introspect(
        ImmutableArray<Node.StatefulGeneration<ActorNode.BuildState>> states,
        CancellationToken token
    )
    {
        var table = new Dictionary<string, IntrospectedBuildState>();

        foreach (var build in states)
        {
            yield return new StatefulGeneration<IntrospectedBuildState>(
                CreateIntrospected(build.State),
                build.Spec
            );

            token.ThrowIfCancellationRequested();
        }

        yield break;

        IntrospectedBuildState Find(string name)
        {
            if (table.TryGetValue(name, out var state))
                return state;

            return table[name] = CreateIntrospected(
                states
                    .First(x => x.State.ActorInfo.Actor.DisplayString == name)
                    .State
            );
        }

        IntrospectedBuildState CreateIntrospected(BuildState context)
        {
            var ancestors = context.AncestralInfo.Ancestors.Select(Find).ToArray();

            return new IntrospectedBuildState(
                BuildState: context,
                Ancestors: new ImmutableEquatableArray<IntrospectedBuildState>(
                    ancestors
                ),
                EntityAssignableAncestors: new ImmutableEquatableArray<IntrospectedBuildState>(
                    ancestors.Where(x => context
                        .AncestralInfo
                        .EntityAssignableAncestors
                        .Contains(x.ActorInfo.Actor.DisplayString)
                    )
                )
            );
        }
    }
    
    public Node.StatefulGeneration<IntrospectedBuildState> CreateLinkInterface(
        Node.StatefulGeneration<IntrospectedBuildState> context,
        CancellationToken token)
    {
        var linkType = new TypeSpec(
            "Link",
            TypeKind.Interface,
            Bases: new([
                context.State.ActorInfo.FormattedLink
            ])
        );

        if (context.State.Ancestors.Count > 0 || !context.State.ActorInfo.IsCore)
        {
            linkType = linkType.AddModifiers("new");
        }

        if (!context.State.ActorInfo.IsCore)
            linkType = linkType.AddBases(context.State.ActorInfo.FormattedCoreLink);

        if (context.State.RedefinesRootInterfaceMemebrs)
        {
            linkType = linkType
                .AddInterfaceMethodOverload(
                    context.State.ActorInfo.Actor.DisplayString,
                    context.State.ActorInfo.FormattedActorProvider,
                    "GetActor",
                    [
                        new ParameterSpec(
                            context.State.ActorInfo.Id.DisplayString,
                            "id"
                        )
                    ],
                    expression: "GetActor(id)"
                )
                .AddInterfaceMethodOverload(
                    context.State.ActorInfo.Entity.DisplayString,
                    context.State.ActorInfo.FormattedEntityProvider,
                    "CreateEntity",
                    [
                        new ParameterSpec(
                            context.State.ActorInfo.Model.DisplayString,
                            "model"
                        )
                    ],
                    expression: "CreateEntity(model)"
                );

            if (!context.State.ActorInfo.IsCore)
            {
                linkType = linkType
                    .AddInterfaceMethodOverload(
                        context.State.ActorInfo.CoreActor.DisplayString,
                        context.State.ActorInfo.FormattedCoreActorProvider,
                        "GetActor",
                        [
                            new ParameterSpec(
                                context.State.ActorInfo.Id.DisplayString,
                                "id"
                            )
                        ],
                        expression: "GetActor(id)"
                    )
                    .AddInterfaceMethodOverload(
                        context.State.ActorInfo.CoreEntity.DisplayString,
                        context.State.ActorInfo.FormattedCoreEntityProvider,
                        "CreateEntity",
                        [
                            new ParameterSpec(
                                context.State.ActorInfo.Model.DisplayString,
                                "model"
                            )
                        ],
                        expression: "CreateEntity(model)"
                    );
            }
        }

        foreach (var ancestor in context.State.Ancestors)
        {
            var ancestorActorProviderTarget = ancestor.RedefinesRootInterfaceMemebrs
                ? $"{ancestor.ActorInfo.Actor}.Link"
                : ancestor.ActorInfo.FormattedActorProvider;

            var ancestorEntityProviderTarget = ancestor.RedefinesRootInterfaceMemebrs
                ? $"{ancestor.ActorInfo.Actor}.Link"
                : ancestor.ActorInfo.FormattedEntityProvider;

            linkType = linkType
                .AddInterfaceMethodOverload(
                    ancestor.ActorInfo.Actor.DisplayString,
                    ancestorActorProviderTarget,
                    "GetActor",
                    [(ancestor.ActorInfo.Id.DisplayString, "id")],
                    expression: "GetActor(id)"
                )
                .AddInterfaceMethodOverload(
                    ancestor.ActorInfo.Entity.DisplayString,
                    ancestorEntityProviderTarget,
                    "CreateEntity",
                    [(ancestor.ActorInfo.Model.DisplayString, "model")],
                    expression: "CreateEntity(model)"
                );
        }

        return context with
        {
            Spec = context.Spec.AddNestedType(linkType)
        };
    }
}