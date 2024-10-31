using System.Collections.Immutable;
using System.Diagnostics;
using Discord.Net.Hanz.Tasks.Actors.Links.V5.Nodes;
using Discord.Net.Hanz.Tasks.Actors.V3;
using Microsoft.CodeAnalysis;

namespace Discord.Net.Hanz.Tasks.Actors.Links.V5;

public class LinksV5 : GenerationTask
{
    private readonly Logger _logger;

    public LinksV5(
        IncrementalGeneratorInitializationContext context,
        Logger logger
    ) : base(context, logger)
    {
        _logger = logger;

        var actorTask = GetTask<ActorsTask>(context);
        var schematicTask = GetTask<LinkSchematics>(context);

        var provider = schematicTask.Schematics
            .Combine(actorTask.Actors.Collect())
            .SelectMany((x, _) => x.Right.Select(y => new NodeContext(x.Left, y)));

        Node.Initialize(
            new NodeProviders(
                schematicTask.Schematics,
                actorTask.Actors,
                provider,
                context
            )
        );
    }

    public readonly struct NodeContext : IEquatable<NodeContext>
    {
        public readonly LinkSchematics.Schematic Schematic;
        public readonly ActorsTask.ActorSymbols Target;

        public NodeContext(LinkSchematics.Schematic schematic, ActorsTask.ActorSymbols target)
        {
            Schematic = schematic;
            Target = target;
        }

        public override int GetHashCode()
            => HashCode.Of(Schematic).And(Target);

        public bool Equals(NodeContext other)
            => Schematic.Equals(other.Schematic) && Target.Equals(other.Target);
    }
}