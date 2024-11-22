using Discord.Models;
using Discord.Models.Json;
using Discord.Rest;

namespace Discord;

public partial interface IGuildScheduledEvent :
    ISnowflakeEntity<IGuildScheduledEventModel>,
    IGuildScheduledEventActor
{
    IGuildChannelActor? Channel { get; }
    IUserActor Creator { get; }
    string Name { get; }
    string? Description { get; }
    string? CoverImageId { get; }
    DateTimeOffset ScheduledStartTime { get; }
    DateTimeOffset? ScheduledEndTime { get; }
    GuildScheduledEventPrivacyLevel PrivacyLevel { get; }
    GuildScheduledEventStatus Status { get; }
    GuildScheduledEventEntityType Type { get; }
    ulong? EntityId { get; }
    string? Location { get; }
    int? UserCount { get; }
}
