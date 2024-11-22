using Discord.Rest;

namespace Discord;

[
    Creatable<CreateChannelInviteProperties>(nameof(Routes.CreateChannelInvite)),
    FetchableOfMany(nameof(Routes.GetChannelInvites))
]
public partial interface IGuildChannelInviteActor :
    IGuildInviteActor,
    IChannelInviteActor,
    IGuildChannelActor.CanonicalRelationship,
    IActor<string, IGuildChannelInvite>
{
    [SourceOfTruth] new IGuildChannelActor Channel { get; }
}