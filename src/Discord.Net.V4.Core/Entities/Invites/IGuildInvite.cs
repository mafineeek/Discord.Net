using Discord.Rest;

namespace Discord;

public partial interface IGuildInvite : IInvite, IGuildInviteActor
{
    IGuildScheduledEventActor? GuildScheduledEvent { get; }
}
