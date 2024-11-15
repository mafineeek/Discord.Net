using System.Collections.Immutable;
using Discord.Net.Hanz.Tasks.Actors.Links.V5.Nodes.Common;
using Discord.Net.Hanz.Utils.Bakery;
using Microsoft.CodeAnalysis;

namespace Discord.Net.Hanz.Tasks.Actors.Links.V5.Nodes.Types;

public class IndexableNode : BaseLinkNode
{
    public IndexableNode(NodeProviders providers, Logger logger) : base(providers, logger)
    {
    }

    protected override bool ShouldContinue(LinkNode.State linkState, CancellationToken token)
        => linkState.Entry.Type.Name == "Indexable" && linkState.IsTemplate;

    protected override IncrementalValuesProvider<Branch<ILinkImplmenter.LinkImplementation>> CreateImplementation(
        IncrementalValuesProvider<Branch<LinkInfo>> provider
    ) => provider.Select(Build);

    private ILinkImplmenter.LinkImplementation Build(LinkInfo state, CancellationToken token)
    {
        using var logger = Logger
            .GetSubLogger(state.ActorInfo.Assembly.ToString())
            .GetSubLogger(nameof(Build))
            .GetSubLogger(state.ActorInfo.Actor.MetadataName);
        
        logger.Log("Building indexable link");
        logger.Log($" - {state.ActorInfo.Actor.DisplayString}");
        
        return new ILinkImplmenter.LinkImplementation(
            CreateInterfaceSpec(state, token),
            CreateImplementationSpec(state, token)
        );
    }

    private static string GetOverrideTarget(LinkInfo info, AncestorInfo ancestor)
        => ancestor.HasAncestors
            ? $"{ancestor.ActorInfo.Actor}.{info.State.Path.FormatRelative()}"
            : $"{ancestor.ActorInfo.FormattedLinkType}.Indexable";

    private static ILinkImplmenter.LinkSpec CreateInterfaceSpec(LinkInfo info, CancellationToken token)
    {
        var redefinesLinkMembers = info.Ancestors.Count > 0 || !info.ActorInfo.IsCore;
        
        
        var spec = new ILinkImplmenter.LinkSpec(
            Indexers: new([
                new IndexerSpec(
                    Type: info.ActorInfo.Actor.DisplayString,
                    Modifiers: new(redefinesLinkMembers ? ["new"] : []),
                    Accessibility: Accessibility.Internal,
                    Parameters: new([
                        (info.ActorInfo.FormattedIdentifiable, "identity")
                    ]),
                    Expression: "identity.Actor ?? GetActor(identity.Id)"
                )
            ])
        );

        if (!info.ActorInfo.IsCore)
        {
            spec = spec with
            {
                Indexers = spec.Indexers.AddRange(
                    new IndexerSpec(
                        Type: info.ActorInfo.CoreActor.DisplayString,
                        Parameters: new([
                            (info.ActorInfo.FormattedIdentifiable, "identity")
                        ]),
                        Expression: "identity.Actor ?? GetActor(identity.Id)",
                        ExplicitInterfaceImplementation: $"{info.ActorInfo.CoreActor}.Indexable"
                    ),
                    new IndexerSpec(
                        Type: info.ActorInfo.CoreActor.DisplayString,
                        Parameters: new([
                            (info.ActorInfo.Id.DisplayString, "id")
                        ]),
                        Expression: "this[id]",
                        ExplicitInterfaceImplementation: $"{info.ActorInfo.FormattedCoreLinkType}.Indexable"
                    )
                ),
                Methods = spec.Methods.AddRange(
                    new MethodSpec(
                        Name: "Specifically",
                        ReturnType: info.ActorInfo.Actor.DisplayString,
                        ExplicitInterfaceImplementation: $"{info.ActorInfo.FormattedCoreLinkType}.Indexable",
                        Parameters: new([
                            (info.ActorInfo.Id.DisplayString, "id")
                        ]),
                        Expression: "Specifically(id)"
                    )
                )
            };
        }

        if (!redefinesLinkMembers)
            return spec;

        return spec with
        {
            Indexers = spec.Indexers.AddRange([
                new IndexerSpec(
                    Type: info.ActorInfo.Actor.DisplayString,
                    Modifiers: new(["new"]),
                    Parameters: new([
                        (info.ActorInfo.Id.DisplayString, "id")
                    ]),
                    Expression: $"(this as {info.ActorInfo.FormattedActorProvider}).GetActor(id)"
                ),
                ..info.Ancestors.Select(x =>
                    new IndexerSpec(
                        Type: x.ActorInfo.Actor.DisplayString,
                        Parameters: new([
                            (info.ActorInfo.Id.DisplayString, "id")
                        ]),
                        ExplicitInterfaceImplementation: GetOverrideTarget(info, x),
                        Expression: "this[id]"
                    )
                )
            ]),
            Methods = spec.Methods.AddRange([
                new MethodSpec(
                    Name: "Specifically",
                    ReturnType: info.ActorInfo.Actor.DisplayString,
                    Modifiers: new(["new"]),
                    Parameters: new([
                        (info.ActorInfo.Id.DisplayString, "id")
                    ]),
                    Expression: $"(this as {info.ActorInfo.FormattedActorProvider}).GetActor(id)"
                ),
                ..info.Ancestors.Select(x =>
                    new MethodSpec(
                        Name: "Specifically",
                        ReturnType: x.ActorInfo.Actor.DisplayString,
                        Parameters: new([
                            (info.ActorInfo.Id.DisplayString, "id")
                        ]),
                        ExplicitInterfaceImplementation: GetOverrideTarget(info, x),
                        Expression: "Specifically(id)"
                    )
                )
            ])
        };
    }

    private static ILinkImplmenter.LinkSpec CreateImplementationSpec(LinkInfo info, CancellationToken token)
    {
        return ILinkImplmenter.LinkSpec.Empty;
    }
}