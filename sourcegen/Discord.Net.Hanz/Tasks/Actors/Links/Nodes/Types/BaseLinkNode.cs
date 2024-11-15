using Discord.Net.Hanz.Tasks.Actors.Links.V5.Nodes.Common;
using Discord.Net.Hanz.Utils.Bakery;
using Microsoft.CodeAnalysis;

namespace Discord.Net.Hanz.Tasks.Actors.Links.V5.Nodes.Types;

public abstract class BaseLinkNode : Node, ILinkImplmenter
{
    protected readonly record struct AncestorInfo(
        ActorInfo ActorInfo,
        bool HasAncestors
    );

    protected readonly record struct LinkInfo(
        ImmutableEquatableArray<AncestorInfo> Ancestors,
        LinkNode.State State
    )
    {
        public bool IsTemplate => State.IsTemplate;
        public ActorInfo ActorInfo => State.ActorInfo;
    }

    private readonly IncrementalValueProvider<Grouping<string, ActorInfo>> _ancestors;

    public BaseLinkNode(NodeProviders providers, Logger logger) : base(providers, logger)
    {
        _ancestors = providers.ActorAncestors;
    }

    protected abstract bool ShouldContinue(LinkNode.State linkState, CancellationToken token);

    protected virtual bool ShouldContinue(LinkInfo info, CancellationToken token)
        => true;

    protected abstract IncrementalValuesProvider<Branch<ILinkImplmenter.LinkImplementation>> CreateImplementation(
        IncrementalValuesProvider<Branch<LinkInfo>> provider
    );


    private IncrementalValuesProvider<Branch<LinkInfo>> CreateProvider(
        IncrementalValuesProvider<Branch<LinkNode.State>> provider
    )
    {
        return provider
            .Where(ShouldContinue)
            .Combine(_ancestors)
            .Select((pair, __) => pair.Left
                .Mutate(
                    new LinkInfo(
                        pair.Right
                            .GetEntriesOrEmpty(pair.Left.Value.ActorInfo.Actor.DisplayString)
                            .Select(x => new AncestorInfo(x, pair.Right.TryGetEntries(x.Actor.DisplayString, out _)))
                            .ToImmutableEquatableArray(),
                        pair.Left.Value
                    )
                )
            )
            .Where(ShouldContinue);
    }

    public IncrementalValuesProvider<Branch<ILinkImplmenter.LinkImplementation>> Branch(
        IncrementalValuesProvider<Branch<LinkNode.State>> provider
    ) => CreateImplementation(CreateProvider(provider));
}