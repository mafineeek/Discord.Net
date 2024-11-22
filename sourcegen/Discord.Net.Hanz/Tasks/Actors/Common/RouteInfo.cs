using System.Text;
using Discord.Net.Hanz.Utils.Bakery;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Discord.Net.Hanz.Tasks.Actors.Common;

public readonly record struct RouteInfo(
    string Name,
    string RequestMethod,
    TypeRef? RequestBody,
    TypeRef? ResponseBody,
    string? ContentType,
    ImmutableEquatableArray<RouteParameter> Parameters,
    RouteKind Kind
)
{
    public string AsInvocation(
        Func<RouteParameter, string?>? resolveParameter = null,
        IEnumerable<string>? generics = null)
    {
        var sb = new StringBuilder();

        sb.Append($"Discord.Rest.Routes.{Name}");

        if (generics is not null)
        {
            var genericsArr = generics.ToArray();
            
            if(genericsArr.Length > 0)
                sb.Append("<").Append(string.Join(", ", genericsArr)).Append(">");
        }

        if (Kind is RouteKind.Field or RouteKind.Property)
            return sb.ToString();

        sb.Append('(');

        var parameters = new List<string>();

        for (var i = 0; i < Parameters.Count; i++)
        {
            var parameter = Parameters[i];

            var resolved = resolveParameter?.Invoke(parameter);
            
            if(resolved is null && parameter.Default is not null)
                continue;
            
            parameters.Add(resolved ?? parameter.Name);
        }

        sb.Append(string.Join(", ", parameters));

        sb.Append(')');

        return sb.ToString();
    }
}

public enum RouteKind
{
    Field,
    Property,
    Method
}

public readonly record struct RouteParameter(
    string Name,
    TypeRef Type,
    ImmutableEquatableArray<TypeRef> Heuristics,
    string? Default = null
);