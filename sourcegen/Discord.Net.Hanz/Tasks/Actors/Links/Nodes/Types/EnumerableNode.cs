using Discord.Net.Hanz.Tasks.Actors.Nodes;
using Discord.Net.Hanz.Tasks.Traits;
using Discord.Net.Hanz.Utils.Bakery;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Discord.Net.Hanz.Tasks.Actors.Links.Nodes.Types;

public class EnumerableNode : BaseLinkTypeNode
{
    private readonly IncrementalKeyValueProvider<ActorInfo, ImmutableEquatableArray<ParameterSpec>>
        _extraParametersProvider;

    public EnumerableNode(
        IncrementalGeneratorInitializationContext context,
        Logger logger
    ) : base(context, logger)
    {
        _extraParametersProvider = GetTask<ActorsTask>(context)
            .Actors
            .Select((symbols, token) => (
                Actor: symbols.Actor.ToDisplayString(),
                ExtraParameters: GetExtraParameters(symbols, token))
            )
            .KeyedBy(x => x.Actor, x => x.ExtraParameters)
            .PairKeys(GetTask<ActorsTask>().ActorInfos);
    }

    private ImmutableEquatableArray<ParameterSpec> GetExtraParameters(
        ActorsTask.ActorSymbols symbols,
        CancellationToken token)
    {
        if (symbols.Assembly is not ActorsTask.AssemblyTarget.Core)
        {
            var fetchableOfManyMethod = symbols.GetCoreEntity()
                .GetMembers("FetchManyRoute")
                .OfType<IMethodSymbol>()
                .FirstOrDefault();

            if (fetchableOfManyMethod is null || fetchableOfManyMethod.Parameters.Length == 1)
                goto returnEmpty;

            return new ImmutableEquatableArray<ParameterSpec>(
                fetchableOfManyMethod
                    .Parameters
                    .Skip(1)
                    .Where(x => x.HasExplicitDefaultValue)
                    .Select(ParameterSpec.From)
            );
        }

        var fetchableOfManyAttribute = symbols.GetCoreEntity()
            .GetAttributes()
            .FirstOrDefault(x => x.AttributeClass?.Name == "FetchableOfManyAttribute");

        if (fetchableOfManyAttribute is null)
            goto returnEmpty;

        if (EntityTraits.GetNameOfArgument(fetchableOfManyAttribute) is not MemberAccessExpressionSyntax
            routeMemberAccess)
            goto returnEmpty;

        var route = EntityTraits.GetRouteSymbol(
            routeMemberAccess,
            symbols.SemanticModel.Compilation.GetSemanticModel(routeMemberAccess.SyntaxTree)
        );

        return route is IMethodSymbol method && ParseExtraArgs(method) is { } extra
            ? new(extra.Select(ParameterSpec.From))
            : ImmutableEquatableArray<ParameterSpec>.Empty;

        returnEmpty:
        return ImmutableEquatableArray<ParameterSpec>.Empty;

        static List<IParameterSymbol> ParseExtraArgs(IMethodSymbol symbol)
        {
            var args = new List<IParameterSymbol>();

            foreach (var parameter in symbol.Parameters)
            {
                var heuristic = parameter.GetAttributes()
                    .FirstOrDefault(x => x.AttributeClass?.Name == "IdHeuristicAttribute");

                if (heuristic is not null)
                {
                    continue;
                }

                if (parameter.Name is "id") continue;

                if (!parameter.HasExplicitDefaultValue) continue;

                args.Add(parameter);
            }

            return args;
        }
    }

    protected override bool ShouldContinue(LinkTypeNode.State linkState, CancellationToken token)
        => linkState.Entry.Type.Name == "Enumerable";

    protected override IncrementalValuesProvider<Branch<ILinkImplmenter.LinkImplementation>> CreateImplementation(
        IncrementalValuesProvider<Branch<LinkInfo>> provider
    )
    {
        return provider
            .KeyedBy(x => x.Value.ActorInfo)
            .JoinByKey(
                _extraParametersProvider,
                (info, branch, extraParameters) => branch
                    .Mutate(
                        (
                            Path: branch.Value.State.Path,
                            ExtraParameters: extraParameters,
                            Ancestors: branch.Value.Ancestors
                        )
                    )
            )
            .Select((info, branch) => branch
                .Mutate(
                    CreateImplementation(
                        info,
                        branch.Value.Path,
                        branch.Value.ExtraParameters,
                        branch.Value.Ancestors
                    )
                )
            );
    }

    private static string GetOverrideTarget(TypePath path, AncestorInfo ancestor)
        => ancestor.HasAncestors
            ? $"{ancestor.ActorInfo.Actor}.{path.FormatRelative()}"
            : $"{ancestor.ActorInfo.FormattedLinkType}.Enumerable";

    private ILinkImplmenter.LinkImplementation CreateImplementation(
        ActorInfo info,
        TypePath path,
        ImmutableEquatableArray<ParameterSpec> parameters,
        ImmutableEquatableArray<AncestorInfo> ancestors
    ) => new(
        CreateInterfaceSpec(info, path, parameters, ancestors),
        CreateImplementationSpec(info, path, parameters, ancestors)
    );

    private static ImmutableEquatableArray<ParameterSpec> DefaultParameters =
        new ImmutableEquatableArray<ParameterSpec>([
            ("RequestOptions?", "options", "null"),
            ("CancellationToken", "token", "default")
        ]);

    private ILinkImplmenter.LinkSpec CreateInterfaceSpec(
        ActorInfo info,
        TypePath path,
        ImmutableEquatableArray<ParameterSpec> extraParameters,
        ImmutableEquatableArray<AncestorInfo> ancestors)
    {
        var parameters = DefaultParameters;

        var parametersWithExtra = parameters;

        if (extraParameters.Count > 0)
        {
            parametersWithExtra = new([
                ..extraParameters,
                ..parameters
            ]);
        }

        var spec = new ILinkImplmenter.LinkSpec(
            Methods: new ImmutableEquatableArray<MethodSpec>([
                new MethodSpec(
                    Name: "AllAsync",
                    ReturnType: $"ITask<IReadOnlyCollection<{info.Entity}>>",
                    Parameters: parametersWithExtra,
                    Modifiers: new(["new"])
                ),
                new MethodSpec(
                    Name: "AllAsync",
                    ReturnType: $"ITask<IReadOnlyCollection<{info.Entity}>>",
                    Parameters: parameters,
                    ExplicitInterfaceImplementation: $"{info.FormattedLinkType}.Enumerable",
                    Expression: "AllAsync(options: options, token: token)"
                )
            ])
        );

        foreach (var ancestor in ancestors)
        {
            if (!_extraParametersProvider.TryGetValue(ancestor.ActorInfo, out var ancestorExtraParameters))
                ancestorExtraParameters = ImmutableEquatableArray<ParameterSpec>.Empty;

            var overrideParameters = DefaultParameters;

            if (
                extraParameters.Count > 0 &&
                ancestorExtraParameters.Count > 0 &&
                extraParameters.SequenceEqual(ancestorExtraParameters)
            )
            {
                overrideParameters = parametersWithExtra;
            }

            spec = spec with
            {
                Methods = spec.Methods.AddRange(
                    new MethodSpec(
                        Name: "AllAsync",
                        ReturnType: $"ITask<IReadOnlyCollection<{ancestor.ActorInfo.Entity}>>",
                        Parameters: overrideParameters,
                        ExplicitInterfaceImplementation: GetOverrideTarget(path, ancestor),
                        Expression:
                        $"AllAsync({string.Join(", ", overrideParameters.Select(x => $"{x.Name}: {x.Name}"))})"
                    )
                )
            };
        }


        return spec;
    }

    private ILinkImplmenter.LinkSpec CreateImplementationSpec(
        ActorInfo info,
        TypePath path,
        ImmutableEquatableArray<ParameterSpec> extraParameters,
        ImmutableEquatableArray<AncestorInfo> ancestors)
    {
        return ILinkImplmenter.LinkSpec.Empty;
    }
}