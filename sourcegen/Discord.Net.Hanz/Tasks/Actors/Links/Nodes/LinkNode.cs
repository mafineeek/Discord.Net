using Discord.Net.Hanz.Nodes;
using Microsoft.CodeAnalysis;

namespace Discord.Net.Hanz.Tasks.Actors.Links.Nodes;

public abstract class LinkNode : Node
{
    protected IncrementalValuesProvider<LinkSchematics.Schematic> Schematics { get; }

    protected LinkNode(
        IncrementalGeneratorInitializationContext context,
        Logger logger
    ) : base(context, logger)
    {
        Schematics = GetTask<LinkSchematics>(context).Schematics;
    }
}