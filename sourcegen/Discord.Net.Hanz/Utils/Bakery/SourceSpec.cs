using System.Text;

namespace Discord.Net.Hanz.Utils.Bakery;

public readonly record struct SourceSpec(
    string Path,
    string Namespace,
    ImmutableEquatableArray<string>? Usings = null,
    ImmutableEquatableArray<TypeSpec>? Types = null, 
    ImmutableEquatableArray<string>? DisabledWarnings = null)
{
    public ImmutableEquatableArray<string> Usings { get; init; } = Usings ?? ImmutableEquatableArray<string>.Empty;
    public ImmutableEquatableArray<TypeSpec> Types { get; init; } = Types ?? ImmutableEquatableArray<TypeSpec>.Empty;
    public ImmutableEquatableArray<string> DisabledWarnings { get; init; } = DisabledWarnings ?? ImmutableEquatableArray<string>.Empty;

    public override string ToString()
    {
        var builder = new StringBuilder();

        foreach (var usingDirective in Usings)
        {
            builder.Append("using ").Append(usingDirective).AppendLine(";");
        }

        if (Usings.Count > 0)
            builder.AppendLine();

        builder.Append("namespace ").Append(Namespace).AppendLine(";").AppendLine();

        foreach (var disabledWarning in DisabledWarnings)
        {
            builder.Append("#pragma warning disable ").AppendLine(disabledWarning);
        }

        foreach (var type in Types)
        {
            builder.AppendLine(type.ToString());
        }
        
        foreach (var disabledWarning in DisabledWarnings)
        {
            builder.Append("#pragma warning restore ").AppendLine(disabledWarning);
        }

        return builder.ToString();
    }
}