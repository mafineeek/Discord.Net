using Discord.Net.Hanz.Nodes;
using Discord.Net.Hanz.Utils.Bakery;

namespace Discord.Net.Hanz.Tasks.Actors.Links.Nodes.Types;

public interface ILinkImplmenter :
    IBranchNode<LinkTypeNode.State, ILinkImplmenter.LinkImplementation>
{
    // IncrementalValuesProvider<Branch<LinkImplementation, LinkNode.State>> Implement(
    //     IncrementalValuesProvider<Branch<LinkNode.State, LinkNode.State>> provider
    // );

    public readonly record struct LinkImplementation(
        LinkSpec Interface,
        LinkSpec Implementation
    );

    public readonly record struct LinkSpec(
        ImmutableEquatableArray<PropertySpec>? Properties = null,
        ImmutableEquatableArray<IndexerSpec>? Indexers = null,
        ImmutableEquatableArray<MethodSpec>? Methods = null
    )
    {
        public static readonly LinkSpec Empty = new();
        
        public ImmutableEquatableArray<PropertySpec> Properties { get; init; }
            = Properties ?? ImmutableEquatableArray<PropertySpec>.Empty;
        
        public ImmutableEquatableArray<IndexerSpec> Indexers { get; init; }
            = Indexers ?? ImmutableEquatableArray<IndexerSpec>.Empty;
        
        public ImmutableEquatableArray<MethodSpec> Methods { get; init; }
            = Methods ?? ImmutableEquatableArray<MethodSpec>.Empty;

        public void Apply(ref TypeSpec spec)
        {
            spec = spec with
            {
                Properties = spec.Properties.AddRange(Properties ?? []),
                Indexers = spec.Indexers.AddRange(Indexers ?? []),
                Methods = spec.Methods.AddRange(Methods ?? [])
            };
        }
    }

    // void AddMembers(ref TypeSpec spec, LinkNode.State state);
    //
    // void CreateImplementation(ref TypeSpec spec, LinkNode.State state);
}