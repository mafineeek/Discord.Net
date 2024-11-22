using System.Text;
using Discord.Net.Hanz.Tasks.Actors.Common;
using Discord.Net.Hanz.Tasks.Actors.Nodes;
using Discord.Net.Hanz.Utils;
using Discord.Net.Hanz.Utils.Bakery;
using Microsoft.CodeAnalysis;

namespace Discord.Net.Hanz.Tasks.Actors.TraitsV2.Nodes;

using ExtensionGenerationDetails =
    (
    CreatableTraitNode.CreatableTraitState State,
    ImmutableEquatableArray<(
        ActorInfo BackLink,
        CreatableTraitNode.TraitDetails Details
        )> Targets
    );

public sealed partial class CreatableTraitNode
{
    private void CreateExtensions(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterSourceOutput(
            State
                .Map((key, state) =>
                    (
                        State: state,
                        BackLinks: state.Details
                            .SelectMany(x => x
                                .FromBackLinks
                                .Select(y =>
                                    (BackLink: y, Details: x)
                                )
                            )
                            .ToImmutableEquatableArray()
                    )
                )
                .Where(x => x.BackLinks.Count > 0)
                .Map(CreateExtensionSpec)
                .ValuesProvider,
            (sourceContext, extension) => sourceContext.AddSource(
                extension.Path,
                extension.ToString()
            )
        );
    }

    private SourceSpec CreateExtensionSpec(
        ActorInfo actorInfo,
        ExtensionGenerationDetails details
    )
    {
        var (state, targets) = details;

        Logger.Log($"Creating extension for {state.Actor}, {targets.Count} targets");
        Logger.Flush();


        return new SourceSpec(
            $"CreatableTrait/{actorInfo.Actor.MetadataName}Extension",
            "Discord",
            new([
                "Discord",
                "System.Threading.Tasks"
            ]),
            new([
                new TypeSpec(
                    $"Creatable{GetFriendlyName(actorInfo.Actor)}Extensions",
                    TypeKind.Class,
                    Modifiers: new(["static"]),
                    Methods: targets
                        .Select(x => CreateExtensionMethodSpec(actorInfo, x.Details, x.BackLink))
                        .ToImmutableEquatableArray()
                )
            ])
        );
    }

    private MethodSpec CreateExtensionMethodSpec(
        ActorInfo actorInfo,
        TraitDetails detail,
        ActorInfo backlink)
    {
        var parameters = new List<ParameterSpec>()
        {
            ($"this {actorInfo.FormattedBackLinkOfType(backlink.Actor)}", "link")
        };

        var body = new StringBuilder();

        if (detail.Properties is not null)
        {
            foreach (var property in detail.Properties.Value.AllProperties.OrderByDescending(x => x.IsRequired))
            {
                parameters.Add(
                    new(
                        property.Type.DisplayString,
                        property.Name.ToParameterName(),
                        property.IsRequired ? null : SyntaxUtils.FormatLiteral(null, property.Type)
                    )
                );
            }

            body.AppendLine(
                $$"""
                  var args = new {{detail.Properties.Value.Source.Type}}()
                  {
                      {{
                          string
                              .Join(
                                  $",{Environment.NewLine}",
                                  detail.Properties.Value.AllProperties.Select(x => $"{x.Name} = {x.Name.ToParameterName()}")
                              )
                              .WithNewlinePadding(4)
                      }}
                  };
                  """
            );
        }

        parameters.AddRange([
            ("RequestOptions", "options", "null"),
            ("CancellationToken", "token", "default"),
        ]);

        body.AppendLine(
            $$"""
              var model = await link.Client.RestApiClient.ExecuteRequiredAsync(
                  {{detail.Route.AsInvocation(ResolveRouteParameter)}},
                  options,
                  token
              );

              return link.CreateEntity(model);
              """
        );

        return new MethodSpec(
            detail.MethodName,
            $"Task<{actorInfo.Entity}>",
            Accessibility.Public,
            new(["static", "async"]),
            new(parameters),
            Body: body.ToString()
        );

        string? ResolveRouteParameter(RouteParameter parameter)
        {
            foreach (var heuristic in parameter.Heuristics)
            {
                if (
                    GetNode<ActorNode>().TryGetPathingTo(
                        backlink,
                        x =>
                            x.Actor.Equals(heuristic) ||
                            x.Entity.Equals(heuristic),
                        out var pathing
                    )
                )
                {
                    var sb = new StringBuilder("link.Source");
            
                    foreach (var part in pathing)
                    {
                        sb.Append('.').Append(GetNode<ActorNode>().GetRelationshipName(part.To));
                    }
            
                    return sb.Append(".Id").ToString();
                }
            }

            return null;
        }
    }
}