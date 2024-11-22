using Microsoft.CodeAnalysis;

namespace Discord.Net.Hanz.Tasks.Actors.Nodes;

public sealed partial class ActorNode
{
    private void CreateAliases(IncrementalGeneratorInitializationContext context)
    {
        var targets = GetTask<ActorsTask>(context)
            .ActorInfos
            .ValuesProvider
            .Where((x) => x.Actor.Generics.Length == 0);

        AddOutput(
            targets.Select((x, _) => $"global using {GetFriendlyName(x.Actor)}Link = {x.FormattedLink};"),
            "Links"
        );

        AddOutput(
            targets.Select((x, _) => $"global using {GetFriendlyName(x.Actor)}LinkType = {x.FormattedLinkType};"),
            "LinkTypes"
        );

        AddOutput(
            targets.Select((x, _) => $"global using {GetFriendlyName(x.Actor)}Identity = {x.FormattedIdentifiable}"),
            "Identities"
        );

        void AddOutput(IncrementalValuesProvider<string> provider, string name)
        {
            context.RegisterSourceOutput(
                provider.Collect(),
                (sourceContext, values) => sourceContext.AddSource(
                    $"Aliases/{name}",
                    string.Join(Environment.NewLine, values)
                )
            );
        }
    }
}