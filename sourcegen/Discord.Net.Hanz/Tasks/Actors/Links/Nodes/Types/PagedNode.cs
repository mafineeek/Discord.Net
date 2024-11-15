using Discord.Net.Hanz.Tasks.Actors.Links.V5.Nodes.Common;
using Discord.Net.Hanz.Utils.Bakery;
using Microsoft.CodeAnalysis;

namespace Discord.Net.Hanz.Tasks.Actors.Links.V5.Nodes.Types;

public class PagedNode : BaseLinkNode
{
    private readonly record struct State(
        bool PagesEntity,
        ActorInfo ActorInfo,
        string PagedType,
        string PagingProviderType,
        string ReferenceName,
        ImmutableEquatableArray<(string AsyncPagedType, string OverrideTarget)> Ancestors)
    {
        public string AsyncPagedType => $"IAsyncPaged<{PagedType}>";

        public static State Create(LinkInfo linkInfo, CancellationToken token)
        {
            var pagesEntity = linkInfo.State.Entry.Type.Generics.Length == 1;

            var pagedType = pagesEntity
                ? linkInfo.State.ActorInfo.Entity.DisplayString
                : linkInfo.State.Entry.Type.Generics[0];

            return new State(
                pagesEntity,
                linkInfo.ActorInfo,
                pagedType,
                $"Func<{linkInfo.ActorInfo}.{linkInfo.State.Entry.Type.ReferenceName}, TParams?, RequestOptions?, IAsyncPaged<{pagedType}>>",
                linkInfo.State.Entry.Type.ReferenceName,
                new(
                    linkInfo.Ancestors.Select(x =>
                        (
                            $"IAsyncPaged<{(pagesEntity ? x.ActorInfo.Entity.DisplayString : pagedType)}>",
                            x.HasAncestors
                                ? $"{x.ActorInfo.Actor}.{linkInfo.State.Path.FormatRelative()}"
                                : $"{x.ActorInfo.FormattedLinkType}.{linkInfo.State.Entry.Type.ReferenceName}"
                        )
                    )
                )
            );
        }
    }
    
    public PagedNode(NodeProviders providers, Logger logger) : base(providers, logger)
    {
    }

    protected override bool ShouldContinue(LinkNode.State linkState, CancellationToken token)
        => linkState is {IsTemplate: true, Entry.Type.Name: "Paged"};

    protected override IncrementalValuesProvider<Branch<ILinkImplmenter.LinkImplementation>> CreateImplementation(
        IncrementalValuesProvider<Branch<LinkInfo>> provider)
    {
        return provider
            .Select(State.Create)
            .Select(CreateImplmentation);
    }

    private ILinkImplmenter.LinkImplementation CreateImplmentation(State state, CancellationToken token)
    {
        return new ILinkImplmenter.LinkImplementation(
            CreateInterfaceSpec(state, token),
            CreateImplementationSpec(state, token)
        );
    }

    private ILinkImplmenter.LinkSpec CreateInterfaceSpec(State state, CancellationToken token)
    {
        return new ILinkImplmenter.LinkSpec(
            Methods: new([
                new MethodSpec(
                    Name: "PagedAsync",
                    ReturnType: state.AsyncPagedType,
                    Modifiers: new(["new"]),
                    Parameters: new([
                        ("TParams?", "args", "default"),
                        ("RequestOptions?", "options", "null"),
                    ])
                ),
                new MethodSpec(
                    Name: "PagedAsync",
                    ReturnType: state.AsyncPagedType,
                    ExplicitInterfaceImplementation: $"{state.ActorInfo.FormattedLinkType}.{state.ReferenceName}",
                    Parameters: new([
                        ("TParams?", "args", "default"),
                        ("RequestOptions?", "options", "null"),
                    ]),
                    Expression: "PagedAsync(args, options)"
                ),
                ..state.Ancestors.Select(x => 
                    new MethodSpec(
                        Name: "PagedAsync",
                        ReturnType: x.AsyncPagedType,
                        ExplicitInterfaceImplementation: x.OverrideTarget,
                        Parameters: new([
                            ("TParams?", "args", "default"),
                            ("RequestOptions?", "options", "null"),
                        ]),
                        Expression: "PagedAsync(args, options)"
                    )
                )
            ])
        );
    }

    private ILinkImplmenter.LinkSpec CreateImplementationSpec(State state, CancellationToken token)
    {
        return ILinkImplmenter.LinkSpec.Empty;
    }
}