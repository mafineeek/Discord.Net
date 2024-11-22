using System.Collections.Immutable;
using Discord.Net.Hanz.Nodes;
using Discord.Net.Hanz.Utils.Bakery;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Discord.Net.Hanz.Tasks.Actors.Nodes;

using RelationshipDetails = (string RelationshipName, ImmutableEquatableArray<Relationship> Relationships);
using RelationshipGenerationDetails =
    (
    Node.StatefulGeneration<ActorNode.BuildState> State,
    ActorRelationships Relationships,
    ImmutableEquatableArray<ActorInfo> Hierarchy
    );

public enum RelationshipKind
{
    Normal,
    Canonical
}

public readonly record struct Relationship(
    ActorInfo To,
    RelationshipKind Kind
)
{
    public static Relationship? From(ActorInfo actor, string relationship)
    {
        RelationshipKind? kind = relationship switch
        {
            "Relationship" => RelationshipKind.Normal,
            "CanonicalRelationship" => RelationshipKind.Canonical,
            _ => null
        };

        if (!kind.HasValue) return null;

        return new Relationship(actor, kind.Value);
    }
}

public readonly record struct ActorRelationships(
    ActorInfo ActorInfo,
    string RelationshipName,
    ImmutableEquatableArray<Relationship> Relationships
);

public sealed partial class ActorNode
{
    public IncrementalKeyValueProvider<ActorInfo, ActorRelationships> Relationships { get; private set; }
    private IncrementalKeyValueProvider<ActorInfo, string> UserSpecifiedRelationshipNames { get; set; }
    private IncrementalKeyValueProvider<ActorInfo, string> RelationshipNames { get; set; }

    private void CreateRelationshipProviders(IncrementalGeneratorInitializationContext context)
    {
        UserSpecifiedRelationshipNames = context
            .SyntaxProvider
            .ForAttributeWithMetadataName(
                "Discord.RelationshipNameAttribute",
                (node, _) => node is TypeDeclarationSyntax,
                (string Actor, string Name)? (context, token) =>
                {
                    if (context.SemanticModel.GetDeclaredSymbol(context.TargetNode) is not INamedTypeSymbol symbol)
                        return null;

                    if (context.Attributes.Length != 1)
                        return null;

                    var attribute = context.Attributes[0];

                    if (attribute.ConstructorArguments.Length != 1)
                        return null;

                    if (attribute.ConstructorArguments[0].Value is not string name)
                        return null;

                    return (symbol.ToDisplayString(), name);
                }
            )
            .WhereNonNull()
            .KeyedBy(x => x.Actor, x => x.Name)
            .PairKeys(GetTask<ActorsTask>().ActorInfos);

        RelationshipNames = GetTask<ActorsTask>(context)
            .ActorAncestors
            .MergeByKey(
                UserSpecifiedRelationshipNames,
                (info, ancestors, name) =>
                    name
                        .Or(
                            ancestors
                                .Map(x => x
                                    .Select(UserSpecifiedRelationshipNames.GetValueOrDefault)
                                    .FirstOrDefault(x => x is not null)
                                )
                        )
                        .Or(GetFriendlyName(info.Actor))
            );

        Relationships = GetTask<ActorsTask>(context)
            .Actors
            .GroupManyBy(
                x => ActorInfo.Create(x),
                x => x
                    .GetCoreActor()
                    .Interfaces
                    .Where(x => x.Name.EndsWith("Relationship"))
                    .Select(x =>
                    {
                        var split = x.Name.Split('.');
                        return (
                            Ident: string.Join(".", split.Take(split.Length - 1)),
                            Relationship: split[split.Length - 1]
                        );
                    })
            )
            .MaybeMapValues(
                (info, target) =>
                {
                    var to = GetTask<ActorsTask>(context)
                        .ActorInfos
                        .Entries
                        .FirstOrDefault(x => x.Key.EndsWith(target.Ident))
                        .Value;

                    if (to == default)
                        return default;

                    return Relationship
                        .From(to, target.Relationship)
                        .AsOptional();
                }
            )
            .ToKeyed((info, relationships) => (info, relationships.ToImmutableEquatableArray()))
            .MergeByKey(
                RelationshipNames,
                (info, relationships, name) => new ActorRelationships(
                    info,
                    name.Or(GetFriendlyName(info.Actor)),
                    relationships.Or(ImmutableEquatableArray<Relationship>.Empty)
                )
            );
    }

    public void CreateRelationships(IncrementalGeneratorInitializationContext context)
    {
        CreateRelationshipProviders(context);

        context.RegisterSourceOutput(
            ContainersProvider
                .KeyedBy(x => x.State.ActorInfo)
                .JoinByKey(Relationships)
                .JoinByKey(
                    GetTask<ActorsTask>(context).ActorAncestors,
                    (info, state, ancestors) => (
                        State: state.Value,
                        Relationships: state.Other,
                        Hierarchy: ancestors.ToImmutableEquatableArray()
                    )
                )
                .Select(CreateRelationshipsTypes),
            (sourceContext, sourceSpec) => sourceContext.AddSource(
                sourceSpec.Path,
                sourceSpec.ToString()
            )
        );
    }

    private SourceSpec CreateRelationshipsTypes(
        ActorInfo info,
        RelationshipGenerationDetails details
    )
    {
        var (generation, relationships, hierarchy) = details;

        if (!info.IsCore)
            return CreateSourceSpec(generation.Spec);

        return CreateSourceSpec(
            generation.Spec
                .AddNestedType(CreateRelationshipType(info, details))
                .AddNestedType(CreateCanonicalRelationshipType(info, details))
        );

        SourceSpec CreateSourceSpec(TypeSpec spec)
        {
            return new SourceSpec(
                $"Relationships/{info.Actor.MetadataName}",
                info.Actor.Namespace,
                new(["Discord"]),
                new([spec]),
                new(["CS0108", "CS0109"])
            );
        }
    }

    private TypeSpec CreateCanonicalRelationshipType(
        ActorInfo info,
        RelationshipGenerationDetails details
    )
    {
        var type =
            new TypeSpec(
                    "CanonicalRelationship",
                    TypeKind.Interface,
                    Bases: new([
                        "Relationship",
                        info.FormattedCanonicalRelationship
                    ])
                )
                .AddBases(details.Hierarchy.Select(x => $"{x.Actor}.CanonicalRelationship"));

        foreach (var ancestor in details.Hierarchy)
        {
            type = type
                .AddInterfacePropertyOverload(
                    ancestor.Actor.DisplayString,
                    $"{ancestor.Actor}.Relationship",
                    GetRelationshipName(ancestor),
                    details.Relationships.RelationshipName
                );
        }

        // conflicts
        foreach
        (
            var group
            in details.Hierarchy.Select(x => (Info: x, RelationshipName: GetRelationshipName(x)))
                .Prepend((Info: info, RelationshipName: details.Relationships.RelationshipName))
                .GroupBy(
                    x => x.Info.Entity,
                    (key, x) => (Entity: key, Nodes: x.ToArray())
                )
                .Where(x => x.Nodes.Length > 1)
        )
        {
            var node = group.Nodes[0];
            type = type
                .AddInterfacePropertyOverload(
                    node.Info.Id.DisplayString,
                    node.Info.FormattedRelation,
                    "RelationshipId",
                    $"{node.RelationshipName}.Id"
                );
        }

        if (details.Hierarchy.Any(x => GetRelationshipName(x) == details.Relationships.RelationshipName))
        {
            type = type
                .AddProperties(
                    new PropertySpec(
                        info.Actor.DisplayString,
                        details.Relationships.RelationshipName,
                        Accessibility.Internal,
                        new(["new"])
                    )
                )
                .AddInterfacePropertyOverload(
                    info.Actor.DisplayString,
                    $"{info.Actor}.Relationship",
                    details.Relationships.RelationshipName,
                    details.Relationships.RelationshipName
                );
        }

        return type
            .AddBases(
                details
                    .Hierarchy
                    .Select(x => $"{x.Actor}.CanonicalRelationship")
            );
    }

    private TypeSpec CreateRelationshipType(
        ActorInfo info,
        RelationshipGenerationDetails details
    )
    {
        return
            new TypeSpec(
                    "Relationship",
                    TypeKind.Interface,
                    Bases: new([info.FormattedRelationship])
                )
                .AddInterfacePropertyOverload(
                    info.Actor.DisplayString,
                    info.FormattedRelationship,
                    "RelationshipActor",
                    details.Relationships.RelationshipName
                )
                .AddProperties(new PropertySpec(
                    info.Actor.DisplayString,
                    details.Relationships.RelationshipName,
                    Accessibility.Internal
                ));
    }

    public bool TryGetPathingTo(
        ActorInfo from,
        Func<ActorInfo, bool> predicate,
        out ImmutableArray<Relationship> relationshipPath)
    {
        var queue = new Queue<(ActorInfo ActorInfo, ImmutableArray<Relationship> Path)>([
            (from, ImmutableArray<Relationship>.Empty)
        ]);
        var visited = new HashSet<ActorInfo>();

        while (queue.Count > 0)
        {
            var (current, path) = queue.Dequeue();

            if (predicate(current))
            {
                relationshipPath = path;
                return true;
            }

            if (!visited.Add(current))
                continue;

            if (!Relationships.TryGetValue(current, out var relationships))
                continue;

            foreach (var relationship in relationships.Relationships)
            {
                if (!visited.Add(relationship.To))
                    continue;

                var nextPath = path.Add(relationship);

                if (predicate(relationship.To))
                {
                    relationshipPath = nextPath;
                    return true;
                }

                queue.Enqueue((relationship.To, nextPath));
            }
        }

        relationshipPath = ImmutableArray<Relationship>.Empty;
        return false;
    }

    public string GetRelationshipName(ActorInfo info)
        => RelationshipNames.TryGetValue(info, out var name) ? name : GetFriendlyName(info.Actor);
}