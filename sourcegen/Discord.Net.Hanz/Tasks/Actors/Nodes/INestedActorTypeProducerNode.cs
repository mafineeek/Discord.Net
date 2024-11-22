using Discord.Net.Hanz.Nodes;
using Discord.Net.Hanz.Tasks.Actors;

namespace Discord.Net.Hanz.Tasks.Actors.Nodes;

public readonly record struct NestedTypeProducerContext(
    ActorInfo ActorInfo,
    TypePath Path
);

public interface INestedTypeProducerNode : INestedTypeProducerNode<NestedTypeProducerContext>;
