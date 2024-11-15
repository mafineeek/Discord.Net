using System.Reflection.Metadata;
using Discord.Net.Hanz.Tasks.Actors.Links.V5.Nodes.Common;
using Discord.Net.Hanz.Tasks.Actors.V3;
using Discord.Net.Hanz.Tasks.Traits;
using Discord.Net.Hanz.Utils.Bakery;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Discord.Net.Hanz.Tasks.Actors.Links.V5.Nodes.Types;

public class EnumerableNode : BaseLinkNode
{
    private readonly record struct State(
        LinkInfo Info,
        ExtraParameters ExtraParameters,
        ImmutableEquatableArray<ExtraParameters> AncestorExtraParameters
    )
    {
        public ActorInfo ActorInfo => Info.ActorInfo;

        public bool RedefinesLinkMembers
            => ExtraParameters.Parameters.Count > 0 ||
               Info.IsTemplate ||
               Info.Ancestors.Count > 0;
    }

    public readonly record struct ExtraParameters(
        string Actor,
        ImmutableEquatableArray<ParameterSpec> Parameters
    )
    {
        public bool HasAny => Parameters.Count > 0;

        public static ExtraParameters Create(
            ActorsTask.ActorSymbols target,
            CancellationToken token)
        {
            if (target.Assembly is not ActorsTask.AssemblyTarget.Core)
            {
                var fetchableOfManyMethod = target.GetCoreEntity()
                    .GetMembers("FetchManyRoute")
                    .OfType<IMethodSymbol>()
                    .FirstOrDefault();

                if (fetchableOfManyMethod is null || fetchableOfManyMethod.Parameters.Length == 1)
                    goto returnEmpty;

                return new ExtraParameters(
                    target.Actor.ToDisplayString(),
                    new ImmutableEquatableArray<ParameterSpec>(
                        fetchableOfManyMethod
                            .Parameters
                            .Skip(1)
                            .Where(x => x.HasExplicitDefaultValue)
                            .Select(ParameterSpec.From)
                    )
                );
            }

            var fetchableOfManyAttribute = target.GetCoreEntity()
                .GetAttributes()
                .FirstOrDefault(x => x.AttributeClass?.Name == "FetchableOfManyAttribute");

            if (fetchableOfManyAttribute is null)
                goto returnEmpty;

            if (EntityTraits.GetNameOfArgument(fetchableOfManyAttribute) is not MemberAccessExpressionSyntax
                routeMemberAccess)
                goto returnEmpty;

            var route = EntityTraits.GetRouteSymbol(
                routeMemberAccess,
                target.SemanticModel.Compilation.GetSemanticModel(routeMemberAccess.SyntaxTree)
            );

            return new ExtraParameters(
                target.Actor.ToDisplayString(),
                route is IMethodSymbol method && ParseExtraArgs(method) is { } extra
                    ? new(extra.Select(ParameterSpec.From))
                    : ImmutableEquatableArray<ParameterSpec>.Empty
            );

            returnEmpty:
            return new(target.Actor.ToDisplayString(), ImmutableEquatableArray<ParameterSpec>.Empty);

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
    }


    private readonly IncrementalValueProvider<Keyed<string, ExtraParameters>> _extraParametersProvider;
    private readonly IncrementalValueProvider<Grouping<string, ActorInfo>> _ancestors;
    private readonly IncrementalValueProvider<Grouping<string, ExtraParameters>> _ancestorExtraParameters;

    public EnumerableNode(
        NodeProviders providers,
        Logger logger
    ) : base(providers, logger)
    {
        _ancestors = providers.ActorAncestors;

        _extraParametersProvider = providers
            .Actors
            .Select(ExtraParameters.Create)
            .Where(x => x.Parameters.Count > 0)
            .ToKeyed(x => x.Actor);

        _ancestorExtraParameters = _ancestors
            .Mixin(
                _extraParametersProvider,
                (key, ancestors, keyed) => ancestors
                    .Select(ancestor =>
                        keyed.GetValueOrDefault(ancestor.Actor.DisplayString)
                    )
            );
    }

    protected override bool ShouldContinue(LinkNode.State linkState, CancellationToken token)
        => linkState.Entry.Type.Name == "Enumerable";

    protected override IncrementalValuesProvider<Branch<ILinkImplmenter.LinkImplementation>> CreateImplementation(
        IncrementalValuesProvider<Branch<LinkInfo>> provider
    )
    {
        return provider
            .Combine(
                _extraParametersProvider,
                branch => branch.Value.ActorInfo.Actor.DisplayString,
                (_, extraParameters, branch) => branch.Mutate(
                    (Info: branch.Value, ExtraParameters: extraParameters)
                )
            )
            .Combine(
                _ancestorExtraParameters,
                branch => branch.Value.Info.ActorInfo.Actor.DisplayString,
                (branch, ancestorExtraParameters) => branch.Mutate(
                    new State(branch.Value.Info, branch.Value.ExtraParameters, ancestorExtraParameters)
                )
            )
            .Where((x, _) => x.RedefinesLinkMembers)
            .Select(CreateImplementation);
    }

    private static string GetOverrideTarget(LinkInfo info, AncestorInfo ancestor)
        => ancestor.HasAncestors
            ? $"{ancestor.ActorInfo.Actor}.{info.State.Path.FormatRelative()}"
            : $"{ancestor.ActorInfo.FormattedLinkType}.Enumerable";
    
    private ILinkImplmenter.LinkImplementation CreateImplementation(
        State state,
        CancellationToken token
    ) => new(
        CreateInterfaceSpec(state, token),
        CreateImplementationSpec(state, token)
    );

    private ILinkImplmenter.LinkSpec CreateInterfaceSpec(
        State state,
        CancellationToken token)
    {
        using var logger = Logger
            .GetSubLogger(state.ActorInfo.Assembly.ToString())
            .GetSubLogger(nameof(CreateInterfaceSpec))
            .GetSubLogger(state.ActorInfo.Actor.MetadataName);

        logger.Log($"{state.ActorInfo.Actor}");
        logger.Log($" - {state.RedefinesLinkMembers}");
        logger.Log($" - {state.ExtraParameters}");

        var parameters = new ImmutableEquatableArray<ParameterSpec>([
            ("RequestOptions?", "options", "null"),
            ("CancellationToken", "token", "default")
        ]);

        var parametersWithExtra = parameters;

        if (state.ExtraParameters.HasAny)
        {
            parametersWithExtra = new([
                ..state.ExtraParameters.Parameters,
                ..parameters
            ]);
        }

        var spec = new ILinkImplmenter.LinkSpec(
            Methods: new ImmutableEquatableArray<MethodSpec>([
                new MethodSpec(
                    Name: "AllAsync",
                    ReturnType: $"ITask<IReadOnlyCollection<{state.ActorInfo.Entity}>>",
                    Parameters: parametersWithExtra,
                    Modifiers: new(["new"])
                ),
                new MethodSpec(
                    Name: "AllAsync",
                    ReturnType: $"ITask<IReadOnlyCollection<{state.ActorInfo.Entity}>>",
                    Parameters: parameters,
                    ExplicitInterfaceImplementation: $"{state.ActorInfo.FormattedLinkType}.Enumerable",
                    Expression: "AllAsync(options: options, token: token)"
                )
            ])
        );

        foreach (var ancestor in state.Info.Ancestors)
        {
            var overrideParameters = parameters;

            if (
                state.ExtraParameters.HasAny &&
                state.AncestorExtraParameters
                        .FirstOrDefault(x => x.Actor == ancestor.ActorInfo.Actor.DisplayString)
                    is { } ancestorExtra &&
                ancestorExtra.Parameters.Equals(state.ExtraParameters.Parameters)
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
                        ExplicitInterfaceImplementation: GetOverrideTarget(state.Info, ancestor),
                        Expression:
                        $"AllAsync({string.Join(", ", overrideParameters.Select(x => $"{x.Name}: {x.Name}"))})"
                    )
                )
            };
        }

        return spec;
    }

    private ILinkImplmenter.LinkSpec CreateImplementationSpec(
        State context,
        CancellationToken token)
    {
        return ILinkImplmenter.LinkSpec.Empty;
    }
}