using Discord.Models.Json;
using Discord.Rest;

namespace Discord;

public interface IRoleActor :
    IGuildRelationship,
    IModifiable<ulong, IRoleActor, ModifyRoleProperties, ModifyGuildRoleParams>,
    IDeletable<ulong, IRoleActor>,
    IActor<ulong, IRole>
{
    static IApiRoute IDeletable<ulong, IRoleActor>.DeleteRoute(IPathable path, ulong id)
        => Routes.DeleteGuildRole(path.Require<IGuild>(), id);

    static IApiInRoute<ModifyGuildRoleParams>
        IModifiable<ulong, IRoleActor, ModifyRoleProperties, ModifyGuildRoleParams>.ModifyRoute(IPathable path,
            ulong id,
            ModifyGuildRoleParams args)
        => Routes.ModifyGuildRole(path.Require<IGuild>(), id, args);
}
