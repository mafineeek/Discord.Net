using Discord.Net.Hanz.Utils.Bakery;
using Microsoft.CodeAnalysis;

namespace Discord.Net.Hanz.Tasks.Actors.Links.Nodes.Types;

public abstract class BaseLinkTypeNode : LinkNode, ILinkImplmenter
{
    protected readonly record struct AncestorInfo(
        ActorInfo ActorInfo,
        bool HasAncestors
    );

    protected readonly record struct LinkInfo(
        ImmutableEquatableArray<AncestorInfo> Ancestors,
        LinkTypeNode.State State
    )
    {
        public bool IsTemplate => State.IsTemplate;
        public ActorInfo ActorInfo => State.ActorInfo;
    }

    public BaseLinkTypeNode(IncrementalGeneratorInitializationContext context, Logger logger) : base(context, logger)
    {
    }

    protected abstract bool ShouldContinue(LinkTypeNode.State linkState, CancellationToken token);

    protected virtual bool ShouldContinue(LinkInfo info, CancellationToken token)
        => true;

    protected abstract IncrementalValuesProvider<Branch<ILinkImplmenter.LinkImplementation>> CreateImplementation(
        IncrementalValuesProvider<Branch<LinkInfo>> provider
    );


    private IncrementalValuesProvider<Branch<LinkInfo>> CreateProvider(
        IncrementalValuesProvider<Branch<LinkTypeNode.State>> provider
    )
    {
        var actorAncestors = GetTask<ActorsTask>()
            .ActorAncestors;

        return provider
            .Where(ShouldContinue)
            .Select((branch, __) => branch
                .Mutate(
                    new LinkInfo(
                        actorAncestors
                            .GetValueOrDefault(branch.Value.ActorInfo, ImmutableEquatableArray<ActorInfo>.Empty)
                            .Select(x => new AncestorInfo(
                                x,
                                actorAncestors.GetValueOrDefault(x, ImmutableEquatableArray<ActorInfo>.Empty).Count > 0)
                            )
                            .ToImmutableEquatableArray(),
                        branch.Value
                    )
                )
            )
            .Where(ShouldContinue);
    }

    public IncrementalValuesProvider<Branch<ILinkImplmenter.LinkImplementation>> Branch(
        IncrementalValuesProvider<Branch<LinkTypeNode.State>> provider
    ) => CreateImplementation(CreateProvider(provider));
}