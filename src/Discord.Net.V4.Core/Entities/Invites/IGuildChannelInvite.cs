using Discord.Rest;

namespace Discord;

public partial interface IGuildChannelInvite :
    IGuildInvite,
    IChannelInvite,
    IGuildChannelInviteActor;
