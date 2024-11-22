using System.Collections.Immutable;
using Discord.Net.Hanz.Tasks.Actors.Nodes;
using Discord.Net.Hanz.Utils.Bakery;
using Microsoft.CodeAnalysis;

namespace Discord.Net.Hanz.Tasks.Actors.TraitsV2.Nodes.Fetchable;

using GenerationState = (FetchableTraitNode.FetchableDetails Details, ActorPathingInfo Pathing);

public sealed partial class FetchableTraitNode
{
    private void CreateImplementation(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterSourceOutput(
            FetchableProvider
                .MapValues((info, details) =>
                {
                    using var logger = Logger.GetSubLogger("State2");
                    
                    logger.Log($"{details.Kind}: {details.Route}");
                
                    return details;
                })
                .MapValuesVia(
                    PathingInfoProvider,
                    GenerationState (info, details, pathing) => (details, pathing)
                )
                .MapValues(ActorNode.CreateActorContainer)
                .MapValues(ImplementFetchableDetails)
                .Select(ToSourceSpec)
        );
    }

    private SourceSpec ToSourceSpec(ActorInfo info, StatefulGeneration<GenerationState> generation)
    {
        return new(
            $"{generation.State.Details.Kind}/{info.Actor.MetadataName}",
            "Discord",
            new(["Discord", "Discord.Rest"]),
            new([generation.Spec])
        );
    }

    private StatefulGeneration<GenerationState> ImplementFetchableDetails(
        ActorInfo info,
        StatefulGeneration<GenerationState> generation)
    {
        using var logger = Logger
            .GetSubLogger("Implement")
            .GetSubLogger(info.Actor.MetadataName);
        
        logger.Log($"Implementing {generation.State.Details.Kind}...");
        
        var spec = generation.Spec;
        
        switch (generation.State.Details.Kind)
        {
            case Kind.Fetchable:
                ImplementSimpleFetchable(info, generation.State.Details, generation.State.Pathing, ref spec);
                break;
            case Kind.FetchableOfMany:
                ImplementFetchableOfMany(info, generation.State.Details, generation.State.Pathing, ref spec);
                break;
            case Kind.PagedFetchableOfMany:
                ImplementPagedFetchableOfMany(info, generation.State.Details, generation.State.Pathing, ref spec);
                break;
        }
        
        logger.Log($"Spec:\n{spec}");

        return generation with {Spec = spec};
    }

    private void ImplementPagedFetchableOfMany(
        ActorInfo info,
        FetchableDetails details,
        ActorPathingInfo pathing,
        ref TypeSpec spec
    )
    {
        if (details.PageParams is null || details.ApiType is null)
            return;

        var fetchableInterface =
            $"Discord.IPagedFetchableOfMany<{info.Id}, {info.Model}, {details.PageParams}, {details.ApiType}>";

        spec = spec.AddBases(fetchableInterface);
    }

    private void ImplementFetchableOfMany(
        ActorInfo info,
        FetchableDetails details,
        ActorPathingInfo pathing,
        ref TypeSpec spec
    )
    {
        var fetchableInterface = $"Discord.IFetchableOfMany<{info.Id}, {info.Model}>";

        var routeInvocation = details.Route.AsInvocation(pathing.ResolveRouteParameterUsingPathable);

        spec = spec
            .AddBases(fetchableInterface)
            .AddMethods(
                new MethodSpec(
                    "FetchManyRoute",
                    $"IApiOutRoute<IEnumerable<{info.Model}>>",
                    Modifiers: new(["static"]),
                    ExplicitInterfaceImplementation: fetchableInterface,
                    Parameters: new([
                        ("IPathable", "path")
                    ]),
                    Expression: routeInvocation
                )
            );
    }

    private void ImplementSimpleFetchable(
        ActorInfo info,
        FetchableDetails details,
        ActorPathingInfo pathing,
        ref TypeSpec spec)
    {
        var fetchableInterface = $"Discord.IFetchable<{info.Id}, {info.Model}>";

        var routeInvocation = details.Route.AsInvocation(pathing.ResolveRouteParameterUsingPathable);

        spec = spec
            .AddBases(fetchableInterface)
            .AddMethods(
                new MethodSpec(
                    "FetchRoute",
                    $"IApiOutRoute<{info.Model}>",
                    Modifiers: new(["static"]),
                    ExplicitInterfaceImplementation: fetchableInterface,
                    Parameters: new([
                        ("IPathable", "path"),
                        (info.Id.DisplayString, "id")
                    ]),
                    Expression: routeInvocation
                )
            );
    }
}