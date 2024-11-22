using Discord.Models;
using Discord.Rest;

namespace Discord;

public partial interface IThreadMember :
    ISnowflakeEntity<IThreadMemberModel>,
    IThreadMemberActor
{
    DateTimeOffset JoinedAt { get; }
}
