using System.Collections.Immutable;
using System.Text;
using Discord.Net.Hanz.Tasks.Actors.Common;
using Discord.Net.Hanz.Tasks.Actors.Nodes;
using Discord.Net.Hanz.Tasks.ApiRoutes;
using Discord.Net.Hanz.Tasks.EntityProperties;
using Discord.Net.Hanz.Utils;
using Discord.Net.Hanz.Utils.Bakery;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Discord.Net.Hanz.Tasks.Actors.TraitsV2.Nodes;

public sealed partial class CreatableTraitNode : TraitNode
{
    public readonly record struct CreatableTraitState(
        string Actor,
        ImmutableEquatableArray<TraitDetails> Details
    );

    public readonly record struct TraitDetails(
        RouteInfo Route,
        string MethodName,
        EntityPropertiesTask.EntityPropertiesWithInheritance? Properties,
        ImmutableEquatableArray<TypeRef> RouteGenerics,
        ImmutableEquatableArray<ActorInfo> FromBackLinks,
        string? IdPath
    );

    private readonly record struct TraitAttributeDetails(
        string Route,
        string? MethodName,
        string? Properties,
        ImmutableEquatableArray<TypeRef> RouteGenerics,
        ImmutableEquatableArray<string> FromBackLinks,
        string? IdPath
    );

    private readonly record struct ActorMapping(
        string Actor,
        ImmutableEquatableArray<TraitAttributeDetails> Details
    );

    public IncrementalKeyValueProvider<ActorInfo, CreatableTraitState> State { get; }

    private IncrementalValuesProvider<ActorMapping> CreatableProvider { get; }
    private IncrementalValuesProvider<ActorMapping> CreatableWithParametersProvider { get; }

    public CreatableTraitNode(IncrementalGeneratorInitializationContext context, Logger logger) : base(context, logger)
    {
        CreatableProvider = context.SyntaxProvider.ForAttributeWithMetadataName(
                "Discord.CreatableAttribute",
                (node, _) => node is InterfaceDeclarationSyntax,
                Map
            )
            .WhereNonNull();

        CreatableWithParametersProvider = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "Discord.CreatableAttribute`1",
                (node, _) => node is InterfaceDeclarationSyntax,
                Map
            )
            .WhereNonNull();

        State = CreatableProvider
            .Collect()
            .Combine(CreatableWithParametersProvider.Collect())
            .SelectMany(IEnumerable<ActorMapping> (x, _) => [..x.Left, ..x.Right])
            .DependsOn(GetTask<ApiRouteTask>(context).Routes)
            .DependsOn(GetTask<ActorsTask>(context).ActorInfos)
            .DependsOn(GetTask<EntityPropertiesTask>(context).PropertiesWithInherited)
            .Select((mapping, _) =>
            {
                return (
                    mapping.Actor,
                    Details: mapping
                        .Details
                        .Select(x =>
                            (
                                Detail: x,
                                Route: GetTask<ApiRouteTask>(context).Routes.GetValueOrDefault(x.Route),
                                FromBackLinks: x.FromBackLinks
                                    .Select(GetTask<ActorsTask>(context).ActorInfos.GetValueOrDefault)
                                    .Where(x => x != default)
                                    .ToImmutableEquatableArray(),
                                Properties: GetTask<EntityPropertiesTask>(context).PropertiesWithInherited
                                    .GetValueOrDefault(x.Properties)
                            )
                        )
                        .Where(x =>
                            x.Route != default
                            &&
                            (
                                x.FromBackLinks.Count == 0 ||
                                x.FromBackLinks.All(x => x != default)
                            )
                            &&
                            x.Detail.Properties is null == (x.Properties == default)
                        )
                        .ToImmutableEquatableArray()
                );
            })
            .Where(x => x.Details.Count > 0)
            .KeyedBy(
                x => x.Actor,
                x => new CreatableTraitState(
                    x.Actor,
                    x.Details
                        .Select(x =>
                            new TraitDetails(
                                x.Route,
                                x.Detail.MethodName ?? "CreateAsync",
                                x.Properties,
                                x.Detail.RouteGenerics,
                                x.FromBackLinks,
                                x.Detail.IdPath
                            )
                        )
                        .ToImmutableEquatableArray()
                )
            )
            .PairKeys(GetTask<ActorsTask>(context).ActorInfos);

        CreateExtensions(context);
        CreateImplementation(context);
    }


    private ActorMapping? Map(GeneratorAttributeSyntaxContext context, CancellationToken token)
    {
        if (context.SemanticModel.GetDeclaredSymbol(context.TargetNode) is not INamedTypeSymbol symbol)
            return null;

        var details = new List<TraitAttributeDetails>();

        using var logger = Logger.GetSubLogger(symbol.ToFullMetadataName()).WithCleanLogFile();

        foreach (var attribute in context.Attributes)
        {
            logger.Log($"Processing attribute {attribute} on {symbol}");

            if (attribute.ConstructorArguments.Length == 0) continue;

            if (attribute.ConstructorArguments[0].Value is not string route)
                continue;

            foreach (var arg in attribute.ConstructorArguments)
            {
                logger.Log($" - arg: {arg.Kind} : {arg.Type}");
            }

            foreach (var arg in attribute.NamedArguments)
            {
                logger.Log($" - named: {arg.Key} : {arg.Value.Kind} - {arg.Value.Type}");
            }

            var routeGenerics = new List<TypeRef>();

            var routeGenericsArg = attribute.NamedArguments
                .FirstOrDefault(x => x.Key == "RouteGenerics")
                .Value;

            if (routeGenericsArg.Kind is TypedConstantKind.Array)
            {
                routeGenerics.AddRange(
                    routeGenericsArg.Values
                        .Where(x => x.Value is ITypeSymbol)
                        .Select(x => new TypeRef(x.Value as ITypeSymbol))
                );
            }

            details.Add(new TraitAttributeDetails(
                route,
                attribute.NamedArguments
                    .FirstOrDefault(x => x.Key == "MethodName")
                    .Value.Value as string,
                attribute.AttributeClass.TypeArguments[0].ToDisplayString(),
                routeGenerics.ToImmutableEquatableArray(),
                attribute.ConstructorArguments.Length == 1
                    ? ImmutableEquatableArray<string>.Empty
                    : attribute
                        .ConstructorArguments[attribute.ConstructorArguments.Length - 1]
                        .Values
                        .Select(x => x.Value as string).Where(x => x is not null)
                        .ToImmutableEquatableArray(),
                attribute.ConstructorArguments.Length == 3
                    ? attribute.ConstructorArguments[1].Value as string
                    : null
            ));
        }

        if (details.Count == 0)
        {
            logger.Log($"no details");
            return null;
        }

        foreach (var detail in details)
        {
            logger.Log($" - {detail}");
            logger.Log($" - {detail.FromBackLinks.Count} backlinks:");

            foreach (var backLink in detail.FromBackLinks)
            {
                logger.Log($"   - {backLink}");
            }
        }

        return new(symbol.ToDisplayString(), details.ToImmutableEquatableArray());
    }
}