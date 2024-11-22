using Discord.Net.Hanz.Tasks.Actors.Common;
using Discord.Net.Hanz.Tasks.Actors.Nodes;
using Discord.Net.Hanz.Tasks.ApiRoutes;
using Discord.Net.Hanz.Utils.Bakery;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Discord.Net.Hanz.Tasks.Actors.TraitsV2.Nodes.Deletable;

public sealed class DeletableTraitNode : TraitNode
{
    public IncrementalKeyValueProvider<ActorInfo, RouteInfo> DeletableActors { get; }

    public DeletableTraitNode(IncrementalGeneratorInitializationContext context, Logger logger) : base(context, logger)
    {
        DeletableActors = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "Discord.DeletableAttribute",
                (node, _) => node is InterfaceDeclarationSyntax,
                (string Actor, string Route)? (context, _) =>
                {
                    if (context.SemanticModel.GetDeclaredSymbol(context.TargetNode) is not INamedTypeSymbol symbol)
                        return null;

                    if (context.Attributes.Length != 1)
                        return null;

                    if (context.Attributes[0].ConstructorArguments.Length != 1 ||
                        context.Attributes[0].ConstructorArguments[0].Value is not string route)
                        return null;

                    return (
                        symbol.ToDisplayString(),
                        route
                    );
                }
            )
            .WhereNonNull()
            .DependsOn(GetTask<ApiRouteTask>().Routes)
            .DependsOn(GetTask<ActorsTask>().ActorInfos)
            .MaybeSelect(x =>
            {
                using var logger = Logger
                    .GetSubLogger("Mapping");
                    
                if (!GetTask<ApiRouteTask>().Routes.TryGetValue(x.Route, out var routeInfo))
                {
                    logger.Log($"{x.Actor}: no route info found for {x.Route}");
                    return default;
                }

                if (!GetTask<ActorsTask>().ActorInfos.TryGetValue(x.Actor, out var actorInfo))
                {
                    logger.Log($"{x.Actor}: no actor info found for {x.Actor}");
                    return default;
                }

                logger.Log($"{x.Actor}: mapped {actorInfo.Actor} : {routeInfo}");
                
                return (ActorInfo: actorInfo, RouteInfo: routeInfo).Some();
            })
            .KeyedBy(x => x.ActorInfo, x => x.RouteInfo);

        context.RegisterSourceOutput(
            DeletableActors
                .JoinByKey(PathingInfoProvider)
                .Map(ActorNode.CreateActorContainer)
                .Select(CreateImplementation)
        );
    }

    private static SourceSpec CreateImplementation(
        ActorInfo info,
        StatefulGeneration<(RouteInfo RouteInfo, ActorPathingInfo PathingInfo)> generation)
    {
        var ((route, pathing), spec) = generation;

        var deletableInterface = $"Discord.IDeletable<{info.Id}, {info.Actor}>";

        spec = spec
            .AddBases(
                deletableInterface
            )
            .AddMethods(
                new MethodSpec(
                    "DeleteRoute",
                    "IApiRoute",
                    Modifiers: new([
                        "static"
                    ]),
                    ExplicitInterfaceImplementation: deletableInterface,
                    Parameters: new([
                        ("IPathable", "path"),
                        (info.Id.DisplayString, "id")
                    ]),
                    Expression: route.AsInvocation(parameter =>
                    {
                        if (parameter.Type.Equals(info.Id))
                            return "id";

                        return pathing.ResolveRouteParameterUsingPathable(parameter);
                    })
                )
            );

        return new SourceSpec(
            $"Deletable/{info.Actor.MetadataName}",
            "Discord",
            Types: new([
                spec
            ])
        );
    }
}