using Discord.Models;
using Discord.Models.Json;
using Discord.Rest;

namespace Discord;

[Loadable(nameof(Routes.GetGuildRole))]
[Deletable(nameof(Routes.DeleteGuildRole))]
[Modifiable<ModifyRoleProperties>(nameof(Routes.ModifyGuildRole))]
public partial interface IRoleActor :
    IGuildRelationship,
    IActor<ulong, IRole>;
