using Discord.Models;
using Discord.Models.Json;
using Discord.Rest;

namespace Discord;

using IModifiable = IModifiable<ulong, IGuildEmote, EmoteProperties, ModifyEmojiParams, IGuildEmoteModel>;

/// <summary>
///     An image-based emote that is attached to a guild.
/// </summary>
public interface IGuildEmote :
    IEmote,
    ISnowflakeEntity,
    IGuildEmoteActor,
    IRefreshable<IGuildEmote, ulong, IGuildEmoteModel>,
    IModifiable
{
    static IApiInOutRoute<ModifyEmojiParams, IEntityModel> IModifiable.ModifyRoute(
        IPathable path,
        ulong id,
        ModifyEmojiParams args
    ) => Routes.ModifyGuildEmoji(path.Require<IGuild>(), id, args);

    static IApiOutRoute<IGuildEmoteModel> IRefreshable<IGuildEmote, ulong, IGuildEmoteModel>.RefreshRoute(
        IGuildEmote self, ulong id)
        => Routes.GetGuildEmoji(self.Require<IGuild>(), id);

    /// <summary>
    ///     Gets whether this emoji is managed by an integration.
    /// </summary>
    bool IsManaged { get; }

    /// <summary>
    ///     Gets whether this emoji must be wrapped in colons.
    /// </summary>
    bool RequireColons { get; }

    /// <summary>
    ///     Gets whether this emoji is animated.
    /// </summary>
    bool IsAnimated { get; }

    /// <summary>
    ///     Gets whether this emoji is available for use, may be <see langword="false" /> due to loss of Server Boosts
    /// </summary>
    bool IsAvailable { get; }

    IDefinedLoadableEntityEnumerable<ulong, IRole> Roles { get; }

    ILoadableEntity<ulong, IUser>? Creator { get; }

    IEmoteModel IEntityProperties<IEmoteModel>.ToApiModel(IEmoteModel? existing)
        => ToApiModel(existing);

    new IEmoteModel ToApiModel(IEmoteModel? existing = null)
        => existing ?? new Models.Json.GuildEmote
        {
            Id = Id,
            RequireColons = RequireColons,
            Animated = IsAnimated,
            Available = IsAvailable,
            Managed = IsManaged,
            Name = Name,
            RoleIds = Roles.Ids.ToArray()
        };
}
