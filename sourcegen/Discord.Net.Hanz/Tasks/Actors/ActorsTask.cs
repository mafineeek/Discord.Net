using Discord.Net.Hanz.Utils;
using Discord.Net.Hanz.Utils.Bakery;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Discord.Net.Hanz.Tasks.Actors;

public class ActorsTask : GenerationTask
{
    public enum AssemblyTarget
    {
        Core,
        Rest
    }

    public static readonly string[] AllowedAssemblies =
    [
        "Discord.Net.V4.Core",
        "Discord.Net.V4.Rest"
    ];

    public sealed class ActorSymbols(
        SemanticModel semanticModel,
        TypeDeclarationSyntax syntax,
        INamedTypeSymbol entity,
        INamedTypeSymbol actor,
        INamedTypeSymbol model,
        ITypeSymbol id,
        AssemblyTarget assembly
    ) : IEquatable<ActorSymbols>
    {
        public SemanticModel SemanticModel { get; private set; } = semanticModel;
        public TypeDeclarationSyntax Syntax { get; } = syntax;
        public INamedTypeSymbol Entity { get; } = entity;
        public INamedTypeSymbol Actor { get; } = actor;
        public INamedTypeSymbol Model { get; } = model;
        public ITypeSymbol Id { get; } = id;
        public AssemblyTarget Assembly { get; } = assembly;

        public bool Equals(ActorSymbols other)
            => GetHashCode() == other.GetHashCode();

        public override int GetHashCode()
            => HashCode
                .Of(Actor.ToDisplayString())
                .And((int) Assembly)
                .AndEach(Actor
                    .GetAttributes()
                    .Select(x => HashCode
                        .Of(x.AttributeClass?.ToDisplayString())
                    )
                );

        public INamedTypeSymbol GetCoreActor()
        {
            if (Assembly is AssemblyTarget.Core) return Actor;

            return Hierarchy.GetHierarchy(Actor, false)
                .First(x =>
                    x.Type.ContainingAssembly.Name == "Discord.Net.V4.Core"
                    &&
                    x.Type.AllInterfaces.Any(y => y is {Name: "IActor", TypeArguments.Length: 2})
                ).Type;
        }

        public INamedTypeSymbol GetCoreEntity()
        {
            if (Assembly is AssemblyTarget.Core) return Entity;

            return Hierarchy.GetHierarchy(Entity, false)
                .First(x =>
                    x.Type.ContainingAssembly.Name == "Discord.Net.V4.Core"
                    &&
                    x.Type.AllInterfaces.Any(y => y is {Name: "IEntity"})
                ).Type;
        }
    }

    public readonly record struct ActorHierarchy(
        ImmutableEquatableArray<ActorInfo> Parents,
        ImmutableEquatableArray<ActorInfo> Children
    )
    {
        public static readonly ActorHierarchy Empty = new(
            ImmutableEquatableArray<ActorInfo>.Empty,
            ImmutableEquatableArray<ActorInfo>.Empty
        );
    }

    public IncrementalValuesProvider<ActorSymbols> Actors { get; }
    public IncrementalKeyValueProvider<string, ActorInfo> ActorInfos { get; }

    public IncrementalKeyValueProvider<ActorInfo, ImmutableEquatableArray<ActorInfo>> ActorAncestors { get; }

    public IncrementalKeyValueProvider<ActorInfo, ActorHierarchy> ActorHierarchies { get; }

    public ActorsTask(IncrementalGeneratorInitializationContext context, Logger logger) : base(context, logger)
    {
        Actors = context
            .SyntaxProvider
            .CreateSyntaxProvider(
                IsPossibleActorNode,
                GetPossibleActorSymbols
            )
            .WhereNonNull();

        ActorInfos = Actors
            .Select((x, _) => ActorInfo.Create(x))
            .KeyedBy(x => x.Actor.DisplayString);

        ActorAncestors = Actors
            .KeyedBy(
                x => x.Actor.ToDisplayString(),
                x => TypeUtils
                    .GetBaseTypes(x.Actor)
                    .Concat(x.Actor.AllInterfaces)
                    .Select(x => x.ToDisplayString())
                    .ToImmutableEquatableArray()
            )
            .TransformKeyVia(ActorInfos)
            .Map((info, ancestors) => ancestors
                .Where(ActorInfos.ContainsKey)
                .Select(ActorInfos.GetValue)
                .ToImmutableEquatableArray()
            );

        ActorHierarchies = ActorAncestors
            .Map((info, ancestors) =>
                new ActorHierarchy(
                    ancestors.ToImmutableEquatableArray(),
                    ActorAncestors
                        .Entries
                        .Where(x => x.Value.Contains(info))
                        .Select(x => x.Key)
                        .ToImmutableEquatableArray()
                )
            );
    }

    public static AssemblyTarget? GetAssemblyTarget(
        Compilation compilation
    ) => GetAssemblyTarget(compilation.Assembly.Name);

    public static AssemblyTarget? GetAssemblyTarget(string name)
    {
        return name switch
        {
            "Discord.Net.V4.Core" => AssemblyTarget.Core,
            "Discord.Net.V4.Rest" => AssemblyTarget.Rest,
            _ => null
        };
    }

    public bool IsPossibleActorNode(SyntaxNode node, CancellationToken token)
    {
        if (node is not TypeDeclarationSyntax typeSyntax)
            return false;

        while (true)
        {
            if (typeSyntax.Modifiers.IndexOf(SyntaxKind.PartialKeyword) == -1)
                return false;

            if (typeSyntax.Parent is not TypeDeclarationSyntax parent)
                return true;

            typeSyntax = parent;
        }
    }

    public ActorSymbols? GetPossibleActorSymbols(GeneratorSyntaxContext context, CancellationToken token)
    {
        if (!AllowedAssemblies.Contains(context.SemanticModel.Compilation.Assembly.Name)) return null;

        var assembly = GetAssemblyTarget(context.SemanticModel.Compilation) ?? throw new NotSupportedException();

        if (context.Node is not TypeDeclarationSyntax syntax)
            return null;

        if (ModelExtensions.GetDeclaredSymbol(context.SemanticModel, syntax) is not INamedTypeSymbol symbol)
            return null;

        var actorType = assembly switch
        {
            AssemblyTarget.Core => "IActor",
            AssemblyTarget.Rest => "IRestActor",
            _ => throw new NotSupportedException()
        };

        var actorInterface = Hierarchy.GetHierarchy(symbol)
            .Select(x => x.Type)
            .FirstOrDefault(x => x.Name == actorType && x is {TypeArguments.Length: 2});

        if (actorInterface is null)
            return null;

        // don't apply to entities
        if (
            actorInterface.TypeArguments[1].Equals(symbol, SymbolEqualityComparer.Default) ||
            actorInterface.TypeArguments[1] is not INamedTypeSymbol entity ||
            symbol.AllInterfaces.Contains(entity))
            return null;

        var entityOfInterface = Hierarchy.GetHierarchy(actorInterface.TypeArguments[1])
            .Select(x => x.Type)
            .FirstOrDefault(x => x is {Name: "IEntityOf", TypeArguments.Length: 1});

        if (entityOfInterface?.TypeArguments.FirstOrDefault() is not INamedTypeSymbol model)
            return null;

        if (syntax.Modifiers.IndexOf(SyntaxKind.PartialKeyword) == -1)
        {
            return null;
        }

        var parent = syntax.Parent;

        while (parent is TypeDeclarationSyntax parentSyntax)
        {
            if (parentSyntax.Modifiers.IndexOf(SyntaxKind.PartialKeyword) == -1)
            {
                return null;
            }

            parent = parentSyntax.Parent;
        }

        return new ActorSymbols(
            context.SemanticModel,
            syntax,
            entity,
            symbol,
            model,
            actorInterface.TypeArguments[0],
            assembly
        );
    }
}