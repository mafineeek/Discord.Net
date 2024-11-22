using System.Text;
using Discord.Net.Hanz.Nodes;
using Discord.Net.Hanz.Tasks.Actors.Common;
using Discord.Net.Hanz.Tasks.Actors.Nodes;
using Microsoft.CodeAnalysis;

namespace Discord.Net.Hanz.Tasks.Actors.TraitsV2.Nodes;

public readonly record struct ActorPathingInfo(
    ActorsTask.ActorHierarchy Hierarchy,
    ActorRelationships Relationships
)
{
    public string? ResolveRouteParameterUsingPathable(RouteParameter parameter)
        => ResolveRouteParameterUsingPathable(parameter, "path");

    public string? ResolveRouteParameterUsingPathable(RouteParameter parameter, string pathableName)
    {
        if (parameter.Heuristics.Count == 0) return null;

        return $"{pathableName}.Require<{parameter.Heuristics[0]}>()";
    }
}

public abstract class TraitNode : Node
{
    protected Logger Logger { get; }

    

    protected IncrementalKeyValueProvider<ActorInfo, ActorPathingInfo> PathingInfoProvider { get; }

    protected TraitNode(IncrementalGeneratorInitializationContext context, Logger logger) : base(context, logger)
    {
        Logger = logger;

        PathingInfoProvider = GetTask<ActorsTask>()
            .ActorHierarchies
            .JoinByKey(
                GetNode<ActorNode>().Relationships,
                ActorPathingInfo? (info, hierarchy, relationships) =>
                {
                    if (hierarchy == default || relationships == default)
                        return null;

                    return new ActorPathingInfo(hierarchy, relationships);
                }
            )
            .Where((_, x) => x != null)
            .Map((_, x) => x.Value);
    }
}