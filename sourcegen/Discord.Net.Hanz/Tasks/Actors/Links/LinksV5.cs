using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.CodeAnalysis;

namespace Discord.Net.Hanz.Tasks.Actors.Links;

public class LinksTask : GenerationTask
{
    public IncrementalValuesProvider<NodeContext> NodeContexts { get; }
    
    private readonly Logger _logger;

    public LinksTask(
        IncrementalGeneratorInitializationContext context,
        Logger logger
    ) : base(context, logger)
    {
        _logger = logger;

        var actorTask = GetTask<ActorsTask>(context);
        var schematicTask = GetTask<LinkSchematics>(context);

        NodeContexts = schematicTask.Schematics
            .Combine(actorTask.Actors.Collect())
            .SelectMany((x, _) => x.Right.Select(y => new NodeContext(x.Left, y)));
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