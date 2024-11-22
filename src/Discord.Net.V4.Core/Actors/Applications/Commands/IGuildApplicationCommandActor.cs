using Discord.Rest;

namespace Discord;

[
    Loadable(nameof(Routes.GetGuildApplicationCommand)),
    Deletable(nameof(Routes.DeleteGuildApplicationCommand)),
    Modifiable<ModifyGuildApplicationCommandProperties>(nameof(Routes.ModifyGuildApplicationCommand)),
    Creatable<CreateGuildApplicationCommandProperties>(nameof(Routes.CreateGuildApplicationCommand)),
    Refreshable(nameof(Routes.GetGuildApplicationCommand)),
    FetchableOfMany(nameof(Routes.GetGuildApplicationCommands))
]
public partial interface IGuildApplicationCommandActor :
    IApplicationCommandActor,
    IGuildActor.CanonicalRelationship,
    IActor<ulong, IGuildApplicationCommand>
{
    IGuildApplicationCommandPermissionsActor.Enumerable.Indexable Permissions { get; }
}