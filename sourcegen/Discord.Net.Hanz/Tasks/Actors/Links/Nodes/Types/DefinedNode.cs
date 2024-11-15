using Discord.Net.Hanz.Tasks.Actors.Links.V5.Nodes.Common;
using Discord.Net.Hanz.Utils.Bakery;
using Microsoft.CodeAnalysis;

namespace Discord.Net.Hanz.Tasks.Actors.Links.V5.Nodes.Types;

public class DefinedNode : BaseLinkNode
{
    public DefinedNode(NodeProviders providers, Logger logger) : base(providers, logger)
    {
    }

    protected override bool ShouldContinue(LinkNode.State linkState, CancellationToken token)
        => linkState.Entry.Type.Name == "Defined";

    protected override IncrementalValuesProvider<Branch<ILinkImplmenter.LinkImplementation>> CreateImplementation(
        IncrementalValuesProvider<Branch<LinkInfo>> provider
    ) => provider.Select(CreateImplementation);

    private static string GetOverrideTarget(
        LinkInfo info,
        AncestorInfo ancestor
    ) => ancestor.HasAncestors
        ? $"{ancestor.ActorInfo.Actor}.{info.State.Path.FormatRelative()}"
        : $"{ancestor.ActorInfo.FormattedLinkType}.Defined";
    
    private ILinkImplmenter.LinkImplementation CreateImplementation(LinkInfo info, CancellationToken token)
    {
        return new ILinkImplmenter.LinkImplementation(
            CreateInterfaceSpec(info, token),
            CreateImplementationSpec(info, token)
        );
    }

    private ILinkImplmenter.LinkSpec CreateInterfaceSpec(LinkInfo info, CancellationToken token)
    {
        return new ILinkImplmenter.LinkSpec(
            Properties: new([
                new PropertySpec(
                    Type: $"IReadOnlyCollection<{info.State.ActorInfo.Id}>",
                    Name: "Ids",
                    Modifiers: new(["new"])
                ),
                new PropertySpec(
                    Type: $"IReadOnlyCollection<{info.State.ActorInfo.Id}>",
                    Name: "Ids",
                    ExplicitInterfaceImplementation: $"{info.State.ActorInfo.FormattedLinkType}.Defined",
                    Expression: "Ids"
                ),
                ..info.Ancestors.Select(x =>
                    new PropertySpec(
                        Type: $"IReadOnlyCollection<{x.ActorInfo.Id}>",
                        Name: "Ids",
                        ExplicitInterfaceImplementation: GetOverrideTarget(info, x),
                        Expression: "Ids"
                    )
                )
            ])
        );
    }

    private ILinkImplmenter.LinkSpec CreateImplementationSpec(LinkInfo state, CancellationToken token)
    {
        return ILinkImplmenter.LinkSpec.Empty;
    }
}