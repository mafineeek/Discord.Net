using Discord.Net.Hanz.Tasks.Actors.Links.Nodes.Types;
using Discord.Net.Hanz.Tasks.Actors.Nodes;
using Discord.Net.Hanz.Utils;
using Discord.Net.Hanz.Utils.Bakery;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Discord.Net.Hanz.Tasks.Actors.Links.Nodes.Modifiers;

public class ExtensionNode :
    LinkNode,
    INestedTypeProducerNode
{
    public readonly record struct Extension(
        string Actor,
        string Name,
        ImmutableEquatableArray<Extension.Property> Properties
    )
    {
        public readonly record struct Property(
            string Name,
            string Type,
            string? Overloads,
            Property.Kind PropertyKind,
            ActorInfo? ActorInfo = null
        )
        {
            public enum Kind
            {
                Normal,
                LinkMirror,
                BackLinkMirror
            }

            public bool IsDefinedOnPath(TypePath path)
            {
                var isRoot = path.Equals(typeof(ActorNode), typeof(ExtensionNode));

                return PropertyKind switch
                {
                    Kind.Normal => isRoot,
                    Kind.LinkMirror => path.Contains<LinkTypeNode>() || isRoot,
                    Kind.BackLinkMirror => isRoot ||
                                           path.Equals(typeof(ActorNode), typeof(ExtensionNode), typeof(BackLinkNode)),
                    _ => false
                };
            }

            public static Property Create(IPropertySymbol symbol)
            {
                var kind = Kind.Normal;

                var linkMirrorAttribute = symbol.GetAttributes()
                    .FirstOrDefault(x => x.AttributeClass?.Name == "LinkMirrorAttribute");

                if (linkMirrorAttribute is not null)
                {
                    kind = linkMirrorAttribute
                        .NamedArguments
                        .FirstOrDefault(x => x.Key == "OnlyBackLinks")
                        .Value
                        .Value is true
                        ? Kind.BackLinkMirror
                        : Kind.LinkMirror;
                }


                return new Property(
                    MemberUtils.GetMemberName(symbol),
                    symbol.Type.ToDisplayString(),
                    symbol.ExplicitInterfaceImplementations.FirstOrDefault()?.ContainingType.ToDisplayString(),
                    kind
                );
            }
        }

        public static IEnumerable<Extension> GetExtensions(
            ActorsTask.ActorSymbols target,
            CancellationToken cancellationToken)
        {
            var types = target
                .GetCoreActor()
                .GetTypeMembers()
                .Where(x => x.Name.EndsWith("Extension"))
                .Where(x => x
                    .GetAttributes()
                    .Any(x => x.AttributeClass?.Name == "LinkExtensionAttribute")
                );

            foreach (var extensionSymbol in types)
            {
                yield return new Extension(
                    target.Actor.ToDisplayString(),
                    extensionSymbol.Name.Replace("Extension", string.Empty),
                    extensionSymbol
                        .GetMembers()
                        .OfType<IPropertySymbol>()
                        .Select(Property.Create)
                        .ToImmutableEquatableArray()
                );
            }
        }
    }

    public readonly record struct BuildContext(
        ActorInfo ActorInfo,
        TypePath Path,
        ImmutableEquatableArray<Extension> Extensions
    );

    public readonly record struct ExtensionContext(
        Extension Extension,
        ActorInfo ActorInfo,
        TypePath Path
    ) : IPathedState;

    private readonly IncrementalGroupingProvider<ActorInfo, Extension> _extensions;

    public ExtensionNode(
        IncrementalGeneratorInitializationContext context,
        Logger logger
    ) : base(context, logger)
    {
        _extensions = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "Discord.LinkExtensionAttribute",
                (node, _) => node is InterfaceDeclarationSyntax,
                Extension? (context, token) =>
                {
                    if (context.SemanticModel.GetDeclaredSymbol(context.TargetNode) is not INamedTypeSymbol
                        {
                            ContainingType: not null
                        } symbol)
                        return null;

                    var ext = new Extension(
                        symbol.ContainingType.ToDisplayString(),
                        symbol.Name.Replace("Extension", string.Empty),
                        symbol.GetMembers().OfType<IPropertySymbol>().Select(Extension.Property.Create)
                            .ToImmutableEquatableArray()
                    );

                    return ext;
                }
            )
            .WhereNonNull()
            .GroupBy(x => x.Actor)
            .TransformKeysVia(GetTask<ActorsTask>().ActorInfos);
    }

    public IncrementalValuesProvider<Branch<TypeSpec>> Create<TSource>(
        IncrementalValuesProvider<Branch<(NestedTypeProducerContext Parameters, TSource Source)>> provider)
    {
        var extensionProvider = provider
            .KeyedBy(x => x.Value.Parameters.ActorInfo)
            .JoinByKey(
                _extensions,
                (info, branch, extensions) => branch
                    .Mutate(
                        new BuildContext(
                            branch.Value.Parameters.ActorInfo,
                            branch.Value.Parameters.Path,
                            extensions.ToImmutableEquatableArray()
                        )
                    )
            )
            .ValuesProvider
            .SelectMany(BuildExtensions);

        var nestedProvider = AddNestedTypes(
            extensionProvider,
            (context, token) => new NestedTypeProducerContext(context.ActorInfo, context.Path),
            GetNode<BackLinkNode>()
        );

        return NestTypesViaPaths(nestedProvider)
            .Select((x, _) => x.Spec);
    }

    private IEnumerable<StatefulGeneration<ExtensionContext>> BuildExtensions(
        BuildContext context,
        CancellationToken token)
    {
        using var logger = Logger
            .GetSubLogger(context.ActorInfo.Assembly.ToString())
            .GetSubLogger(nameof(BuildExtensions))
            .GetSubLogger(context.ActorInfo.Actor.MetadataName)
            .WithCleanLogFile();

        logger.Log($"Building {context.Extensions.Count} extensions...");

        foreach (var extension in context.Extensions)
        foreach (var result in Build(extension, context.Extensions.Remove(extension), context.Path))
        {
            token.ThrowIfCancellationRequested();
            yield return result;
        }

        yield break;

        IEnumerable<StatefulGeneration<ExtensionContext>> Build(
            Extension extension,
            ImmutableEquatableArray<Extension> next,
            TypePath path,
            int depth = 0)
        {
            var extensionPath = path.Add<ExtensionNode>(extension.Name);

            logger.Log($" - {extension.Name} -> {path}".Prefix(depth * 2));

            yield return BuildExtension(context.ActorInfo, extensionPath, extension);

            token.ThrowIfCancellationRequested();

            if (next.Count == 0) yield break;

            var nextExtensions = next.Remove(extension);

            foreach (var child in nextExtensions)
            foreach
            (
                var result
                in Build(
                    child,
                    nextExtensions,
                    extensionPath,
                    depth + 1
                )
            )
                yield return result;
        }
    }

    private StatefulGeneration<ExtensionContext> BuildExtension(
        ActorInfo actorInfo,
        TypePath path,
        Extension extension)
    {
        using var logger = Logger.GetSubLogger(actorInfo.Assembly.ToString())
            .GetSubLogger(nameof(BuildExtension))
            .GetSubLogger(actorInfo.Actor.MetadataName);

        logger.Log($"Extension for {actorInfo.Actor.DisplayString}:");
        logger.Log($" - {extension}");
        logger.Log($" - {path}");

        foreach (var property in extension.Properties)
        {
            logger.Log(
                $"   - {property.Name}: {{ {property.PropertyKind}, {property.Type}, Has Actor: {property.ActorInfo.HasValue}, Is On Path: {property.IsDefinedOnPath(path)} }} ");
        }

        var bases = ImmutableEquatableArray<string>.Empty;

        if (path.First.HasValue && !path.Equals(typeof(ActorNode), typeof(ExtensionNode)))
        {
            bases = bases.AddRange(
                (path.First.Value + path.Slice(1).SemanticalProduct())
                .Select(x => x.ToString())
            );
        }

        return new StatefulGeneration<ExtensionContext>(
            new(extension, actorInfo, path),
            new TypeSpec(
                Name: extension.Name,
                Kind: TypeKind.Interface,
                Properties: extension.Properties
                    .SelectMany(x =>
                        BuildExtensionProperty(path, x, extension)
                    )
                    .ToImmutableEquatableArray(),
                Bases: bases
            )
        );
    }

    public static IEnumerable<PropertySpec> BuildExtensionProperty(
        TypePath path,
        Extension.Property property,
        Extension extension)
    {
        if (!property.IsDefinedOnPath(path))
            yield break;

        if (property.PropertyKind is not Extension.Property.Kind.Normal && property.ActorInfo is null)
            yield break;

        var hasNewKeyword = property.PropertyKind switch
        {
            Extension.Property.Kind.Normal => false,
            Extension.Property.Kind.LinkMirror or Extension.Property.Kind.BackLinkMirror =>
                path.Contains<LinkTypeNode>(),
            _ => false
        };

        var propertyType = property.PropertyKind switch
        {
            Extension.Property.Kind.Normal => property.Type,
            Extension.Property.Kind.LinkMirror =>
                path.Equals(typeof(ActorNode), typeof(ExtensionNode))
                    ? property.ActorInfo!.Value.FormattedLink
                    : $"{property.ActorInfo!.Value.Actor}.{path.OfType<LinkTypeNode>().FormatRelative()}",
            Extension.Property.Kind.BackLinkMirror =>
                path.Last?.Type == typeof(BackLinkNode)
                    ? $"{property.ActorInfo!.Value.Actor}.BackLink<TSource>"
                    : property.ActorInfo!.Value.Actor.DisplayString,
            _ => throw new ArgumentOutOfRangeException()
        };

        var spec = new PropertySpec(
            Name: property.Name,
            Type: propertyType,
            Modifiers: hasNewKeyword
                ? new(["new"])
                : ImmutableEquatableArray<string>.Empty
        );

        yield return spec;

        switch (property.PropertyKind)
        {
            case Extension.Property.Kind.LinkMirror:
                foreach (var pathProduct in path.OfType<LinkTypeNode>().CartesianProduct())
                {
                    yield return new PropertySpec(
                        Name: property.Name,
                        Type: $"{property.ActorInfo!.Value.Actor.DisplayString}.{pathProduct.FormatRelative()}",
                        ExplicitInterfaceImplementation: $"{extension.Actor}.{pathProduct}.{extension.Name}",
                        Expression: property.Name
                    );
                }

                break;
            case Extension.Property.Kind.BackLinkMirror when path.Last?.Type == typeof(BackLinkNode):
                yield return new PropertySpec(
                    Name: property.Name,
                    Type: property.ActorInfo!.Value.Actor.DisplayString,
                    ExplicitInterfaceImplementation: $"{extension.Actor}.{extension.Name}",
                    Expression: property.Name
                );
                break;
        }
    }
}