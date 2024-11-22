using Discord.Models;
using Discord.Rest;

namespace Discord;

[FetchableOfMany(nameof(Routes.GetChannelInvites))]
public interface IChannelInviteActor :
    IInviteActor,
    IChannelActor.CanonicalRelationship,
    IEntityProvider<IChannelInvite, IInviteModel>,
    IActor<string, IChannelInvite>;
