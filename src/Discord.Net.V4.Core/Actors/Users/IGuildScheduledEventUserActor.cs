using Discord.Models;
using Discord.Rest;

namespace Discord;

[PagedFetchableOfMany<PageGuildScheduledEventUsersParams>(nameof(Routes.GetGuildScheduledEventUsers))]
public partial interface IGuildScheduledEventUserActor :
    IGuildScheduledEventActor.CanonicalRelationship,
    IMemberActor.Relationship,
    IActor<ulong, IGuildScheduledEventUser>;
