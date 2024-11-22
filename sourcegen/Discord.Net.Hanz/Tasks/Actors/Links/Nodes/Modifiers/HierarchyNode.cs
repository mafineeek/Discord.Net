using Discord.Net.Hanz.Tasks.Actors.Nodes;
using Discord.Net.Hanz.Utils.Bakery;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Discord.Net.Hanz.Tasks.Actors.Links.Nodes.Modifiers;

public class HierarchyNode :
    LinkNode,
    INestedTypeProducerNode
{
    public readonly record struct BuildContext(
        ActorInfo ActorInfo,
        TypePath Path,
        bool IsTemplate,
        ImmutableEquatableArray<ActorInfo> Properties,
        ImmutableEquatableArray<string> Bases,
        ImmutableEquatableArray<string?> Overloads)
    {
        public TypePath Path { get; } = Path.Add<HierarchyNode>("Hierarchy");

        public IEnumerable<PropertySpec> GetPropertySpecs()
        {
            var actor = ActorInfo.Actor;
            var isTemplate = IsTemplate;
            var relativePath = Path.FormatRelative();
            var overloads = Overloads;

            return Properties.SelectMany(IEnumerable<PropertySpec> (x) =>
            [
                new(
                    Type: isTemplate
                        ? x.FormattedLink
                        : $"{x.Actor}.{relativePath}",
                    Name: GetFriendlyName(x.Actor)
                ),
                ..overloads.Select(path =>
                    new PropertySpec(
                        Type: path is null
                            ? x.FormattedLink
                            : $"{x.Actor}.{path}",
                        Name: GetFriendlyName(x.Actor),
                        ExplicitInterfaceImplementation: path is null
                            ? $"{actor}.Hierarchy"
                            : $"{actor}.{path}.Hierarchy",
                        Expression: GetFriendlyName(x.Actor)
                    )
                )
            ]);
        }

        public static BuildContext Create(NestedTypeProducerContext parameters,
            ImmutableEquatableArray<ActorInfo> hierarchy)
        {
            var bases = new List<string>();
            var overloads = new List<string?>();

            var isTemplate = parameters.Path.Last?.Type == typeof(ActorNode);

            if (isTemplate)
            {
                bases.Add($"{parameters.Path}");
                bases.Add($"{parameters.ActorInfo.Actor}.Hierarchy");
                overloads.Add(null);

                for (int i = 0; i < parameters.Path.Count - 1; i++)
                {
                    var path = parameters.Path.Slice(0, i);
                    bases.Add($"{path}.Hierarchy");
                    overloads.Add(path.ToString());
                }
            }

            return new(
                parameters.ActorInfo,
                parameters.Path,
                isTemplate,
                hierarchy,
                bases.ToImmutableEquatableArray(),
                overloads.ToImmutableEquatableArray()
            );
        }
    }

    private readonly IncrementalKeyValueProvider<ActorInfo, ImmutableEquatableArray<ActorInfo>> _hierarchyProvider;

    public HierarchyNode(IncrementalGeneratorInitializationContext context, Logger logger) : base(context, logger)
    {
        _hierarchyProvider = GetTask<ActorsTask>()
            .ActorHierarchies
            .JoinByKey(
                context
                    .SyntaxProvider
                    .ForAttributeWithMetadataName(
                        "Discord.LinkHierarchicalRootAttribute",
                        (node, _) => node is InterfaceDeclarationSyntax,
                        (string Actor, ImmutableEquatableArray<string> UserSpecifiedTypes)? (sourceContext, _) =>
                        {
                            if (sourceContext.SemanticModel.GetDeclaredSymbol(sourceContext.TargetNode) is not
                                INamedTypeSymbol
                                symbol)
                                return null;

                            if (sourceContext.Attributes.Length != 1)
                                return null;

                            var value = sourceContext
                                .Attributes[0]
                                .NamedArguments
                                .FirstOrDefault(x => x.Key == "Types")
                                .Value;

                            if (value.Kind is TypedConstantKind.Error)
                                return null;

                            return (
                                symbol.ToDisplayString(),
                                value.Values
                                    .Select(x => ((INamedTypeSymbol) x.Value!).ToDisplayString())
                                    .ToImmutableEquatableArray()
                            );
                        }
                    )
                    .WhereNonNull()
                    .KeyedBy(x => x.Actor, x => x.UserSpecifiedTypes)
                    .TransformKeyVia(GetTask<ActorsTask>().ActorInfos),
                (info, hierarchy, userSpecifiedTypes) => userSpecifiedTypes.Count > 0
                    ? userSpecifiedTypes
                        .Select(x => GetTask<ActorsTask>().ActorInfos.GetValueOrDefault(x))
                        .Where(x => x != default)
                        .ToImmutableEquatableArray()
                    : hierarchy.Children
            );
    }

    public IncrementalValuesProvider<Branch<TypeSpec>> Create<TParent>(
        IncrementalValuesProvider<Branch<(NestedTypeProducerContext Parameters, TParent Source)>> provider
    )
    {
        return AddNestedTypes(
            GetNode<BackLinkNode>(),
            _hierarchyProvider
                .JoinByKey(
                    provider.KeyedBy(x => x.Value.Parameters.ActorInfo),
                    (info, hierarchy, branch) => branch
                        .Mutate(BuildContext.Create(branch.Value.Parameters, hierarchy))
                )
                .ValuesProvider
                .Select(Build),
            (context, _) =>
                new NestedTypeProducerContext(
                    context.State.ActorInfo,
                    context.State.Path
                ),
            (context, specs, _) => context.Spec.AddNestedTypes(specs),
            context => context.State
        );
    }

    private StatefulGeneration<BuildContext> Build(BuildContext context, CancellationToken token)
    {
        var spec = new TypeSpec(
            Name: "Hierarchy",
            Kind: TypeKind.Interface,
            Properties: new(context.GetPropertySpecs()),
            Bases: context.Bases
        );

        return new StatefulGeneration<BuildContext>(context, spec);
    }
}