using Discord.Net.Hanz.Tasks.Actors.Common;
using Discord.Net.Hanz.Utils;
using Discord.Net.Hanz.Utils.Bakery;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using CSharpExtensions = Microsoft.CodeAnalysis.CSharp.CSharpExtensions;

namespace Discord.Net.Hanz.Tasks.ApiRoutes;

public class ApiRouteTask : GenerationTask
{
    public IncrementalKeyValueProvider<string, RouteInfo> Routes { get; }

    public ApiRouteTask(
        IncrementalGeneratorInitializationContext context,
        Logger logger
    ) : base(context, logger)
    {
        Routes = context.SyntaxProvider
            .CreateSyntaxProvider(
                IsMatch,
                Transform
            )
            .WhereNonNull()
            .KeyedBy(x => x.Name);
    }

    private RouteInfo? Transform(GeneratorSyntaxContext context, CancellationToken token)
    {
        return context.Node switch
        {
            MethodDeclarationSyntax method => TransformMethod(method, context.SemanticModel),
            PropertyDeclarationSyntax property => TransformProperty(property, context.SemanticModel),
            FieldDeclarationSyntax field => TransformField(field, context.SemanticModel),
            _ => null
        };
    }

    private RouteInfo? TransformField(FieldDeclarationSyntax field, SemanticModel model)
    {
        if (field.Declaration.Variables.Count != 1)
            return null;
        
        if (model.GetDeclaredSymbol(field.Declaration.Variables[0]) is not IFieldSymbol {Type: INamedTypeSymbol routeType} symbol)
        {
            return null;
        }

        if (!IsApiRouteType(routeType))
        {
            return null;
        }

        if (
            field.Declaration.Variables
                .FirstOrDefault()
                ?.Initializer
                ?.Value is not ObjectCreationExpressionSyntax creationExpression
        )
        {
            return null;
        }

        var builder = new RouteBuilder();

        if (!TryParseCreationExpression(builder, symbol, routeType, model, creationExpression))
            return null;

        if (!TryParseRouteTypes(builder, routeType))
            return null;

        return builder.Build(RouteKind.Field);
    }

    private static RouteInfo? TransformProperty(PropertyDeclarationSyntax property, SemanticModel model)
    {
        if (model.GetDeclaredSymbol(property) is not IPropertySymbol {Type: INamedTypeSymbol routeType} symbol)
            return null;

        if (!IsApiRouteType(routeType))
            return null;

        if (property.ExpressionBody is not {Expression: ObjectCreationExpressionSyntax creationExpression})
            return null;

        var builder = new RouteBuilder();

        if (!TryParseCreationExpression(builder, symbol, routeType, model, creationExpression))
            return null;

        if (!TryParseRouteTypes(builder, routeType))
            return null;

        return builder.Build(RouteKind.Property);
    }

    private static RouteInfo? TransformMethod(MethodDeclarationSyntax method, SemanticModel model)
    {
        if (model.GetDeclaredSymbol(method) is not IMethodSymbol {ReturnType: INamedTypeSymbol routeType} symbol)
            return null;

        if (!IsApiRouteType(routeType))
            return null;

        if (method.ExpressionBody is not {Expression: ObjectCreationExpressionSyntax creationExpression})
            return null;

        var builder = new RouteBuilder();

        if (!TryParseCreationExpression(builder, symbol, routeType, model, creationExpression))
            return null;

        if (!TryParseRouteTypes(builder, routeType))
            return null;

        foreach (var parameter in symbol.Parameters)
        {
            var heuristics = new List<TypeRef>(
                parameter.GetAttributes()
                    .Where(x => x.AttributeClass is {Name: "IdHeuristicAttribute", TypeArguments.Length: 1})
                    .Select(x => new TypeRef(x.AttributeClass.TypeArguments[0]))
            );

            var type = new TypeRef(parameter.Type);

            builder.Parameters.Add((
                parameter.Name,
                type,
                heuristics.ToImmutableEquatableArray(),
                parameter.HasExplicitDefaultValue
                    ? SyntaxUtils.FormatLiteral(parameter.ExplicitDefaultValue, type)
                    : null
            ));
        }

        return builder.Build(RouteKind.Method);
    }

    private static bool IsApiRouteType(INamedTypeSymbol symbol)
    {
        if (symbol.Name is "IApiRoute")
            return true;

        return symbol.AllInterfaces.Any(x => x.Name is "IApiRoute");
    }

    private static bool TryParseRouteTypes(RouteBuilder builder, INamedTypeSymbol routeType)
    {
        switch (routeType.Name)
        {
            case "IApiRoute": break;
            case "IApiOutRoute" when routeType.TypeArguments.Length == 1:
                builder.ResponseBody = new TypeRef(routeType.TypeArguments[0]);
                break;
            case "IApiInRoute" when routeType.TypeArguments.Length == 1:
                builder.RequestBody = new TypeRef(routeType.TypeArguments[0]);
                break;
            case "IApiInOutRoute" when routeType.TypeArguments.Length == 2:
                builder.RequestBody = new TypeRef(routeType.TypeArguments[0]);
                builder.ResponseBody = new TypeRef(routeType.TypeArguments[1]);
                break;
            default: return false;
        }

        return true;
    }

    private static bool TryParseCreationExpression(
        RouteBuilder builder,
        ISymbol symbol,
        INamedTypeSymbol routeType,
        SemanticModel model,
        ObjectCreationExpressionSyntax syntax)
    {
        if (syntax.ArgumentList is null)
            return false;

        if (model.GetOperation(syntax) is not IObjectCreationOperation {Constructor: not null} creationOperation)
            return false;

        for (var i = 0; i < syntax.ArgumentList.Arguments.Count; i++)
        {
            var argument = syntax.ArgumentList.Arguments[i];

            var name = argument.NameColon is not null
                ? argument.NameColon.Name.Identifier.ValueText
                : creationOperation.Constructor.Parameters[i].Name;

            switch (name)
            {
                case "name":
                    builder.RouteDetails[name] = GetRouteName(symbol, argument.Expression, model);
                    break;
                case "method":
                    if (!TryParseEnum(argument.Expression, model, out var method))
                        return false;
                    builder.RouteDetails[name] = method;
                    break;
                case "endpoint":
                    builder.RouteDetails[name] = GetEndpoint(argument.Expression, model);
                    break;
                case "contentType":
                    if (!TryParseEnum(argument.Expression, model, out var contentType))
                        return false;
                    builder.RouteDetails[name] = contentType;
                    break;
            }
        }

        return true;
    }

    private static string GetEndpoint(ExpressionSyntax expression, SemanticModel model)
    {
        if (expression is InterpolatedStringExpressionSyntax interpolated)
            return string.Join(string.Empty, interpolated.Contents.Select(x => x.ToString()));

        return expression.ToString();
    }

    private static bool TryParseEnum(ExpressionSyntax expression, SemanticModel model, out string result)
    {
        result = null!;

        if (expression is not MemberAccessExpressionSyntax access)
            return false;

        result = access.Name.ToString();
        return true;
    }

    private static string GetRouteName(ISymbol symbol, ExpressionSyntax expression, SemanticModel model)
    {
        if (model.GetOperation(expression) is INameOfOperation {Argument.ConstantValue.HasValue: true} nameOf)
            return nameOf.Argument.ConstantValue.Value.ToString();

        return symbol.Name;
    }

    private static bool IsMatch(SyntaxNode node, CancellationToken token)
        => node is MemberDeclarationSyntax {Parent: ClassDeclarationSyntax {Identifier.ValueText: "Routes"}};

    private sealed class RouteBuilder
    {
        public TypeRef? RequestBody { get; set; }
        public TypeRef? ResponseBody { get; set; }

        public Dictionary<string, string> RouteDetails { get; } = [];

        public List<(string Name, TypeRef Type, ImmutableEquatableArray<TypeRef> Heuristics, string? Default)>
            Parameters { get; } = [];

        public RouteInfo? Build(RouteKind kind)
        {
            if (!RouteDetails.TryGetValue("name", out var name))
                return null;

            if (!RouteDetails.TryGetValue("method", out var method))
                return null;

            return new RouteInfo(
                name,
                method,
                RequestBody,
                ResponseBody,
                RouteDetails.TryGetValue("contentType", out var contentType)
                    ? contentType
                    : null,
                Parameters
                    .Select(x =>
                        new RouteParameter(x.Name, x.Type, x.Heuristics, x.Default)
                    )
                    .ToImmutableEquatableArray(),
                kind
            );
        }
    }
}