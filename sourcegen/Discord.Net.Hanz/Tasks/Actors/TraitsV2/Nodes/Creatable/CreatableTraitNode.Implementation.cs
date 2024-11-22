using Discord.Net.Hanz.Tasks.Actors.Common;
using Discord.Net.Hanz.Tasks.Actors.Nodes;
using Discord.Net.Hanz.Tasks.EntityProperties;
using Discord.Net.Hanz.Utils.Bakery;
using Microsoft.CodeAnalysis;

namespace Discord.Net.Hanz.Tasks.Actors.TraitsV2.Nodes;

public sealed partial class CreatableTraitNode
{
    private void CreateImplementation(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterSourceOutput(
            State
                .Map(ActorNode.CreateActorContainer)
                .Select(CreateImplementation),
            (sourceContext, spec) => sourceContext.AddSource(
                spec.Path,
                spec.ToString()
            )
        );
    }

    private SourceSpec CreateImplementation(ActorInfo info, StatefulGeneration<CreatableTraitState> generation)
    {
        var (state, spec) = generation;

        foreach (var detail in state.Details)
        {
            ImplementDetails(ref spec, info, detail);
        }

        return new SourceSpec(
            $"Creatable/{info.Actor.MetadataName}",
            "Discord",
            new(["Discord"]),
            new([
                spec
            ])
        );
    }

    private void ImplementDetails(ref TypeSpec spec, ActorInfo info, TraitDetails details)
    {
        if (details.Properties.HasValue)
        {
            ImplementCreatableWithProperties(ref spec, info, details, details.Properties.Value);
            return;
        }
    }

    private void ImplementCreatableWithProperties(
        ref TypeSpec spec,
        ActorInfo info,
        TraitDetails details,
        EntityPropertiesTask.EntityPropertiesWithInheritance properties)
    {
        var creatableInterface = $"Discord.ICreatable<" +
                                 $"{info.Actor}, " +
                                 $"{info.Entity}, " +
                                 $"{info.Id}, " +
                                 $"{details.Properties.Value.Source.Type}, " +
                                 $"{details.Properties.Value.Source.ParamsType}, " +
                                 $"{info.Model}>";

        var extraParameters = new List<RouteParameter>();

        var routeExpression = details.Route
            .AsInvocation(
                parameter =>
                {
                    if (parameter.Heuristics.Count > 0)
                        return $"path.Require<{parameter.Heuristics[0]}>()";

                    if (parameter.Name is "id")
                        return $"path.Require<{info.Entity}>()";

                    if (parameter.Type.Equals(properties.Source.ParamsType))
                        return "args";

                    extraParameters.Add(parameter);
                    return null;
                },
                details.RouteGenerics.Select(x => x.DisplayString)
            );

        spec = spec
            .AddBases(creatableInterface)
            .AddMethods(
                new MethodSpec(
                    "CreateRoute",
                    $"IApiInOutRoute<{details.Properties.Value.Source.ParamsType}, {info.Model}>",
                    ExplicitInterfaceImplementation: creatableInterface,
                    Modifiers: new(["static"]),
                    Parameters: new([
                        ("IPathable", "path"),
                        (details.Properties.Value.Source.ParamsType.DisplayString, "args")
                    ]),
                    Expression: routeExpression
                )
            );
    }
}