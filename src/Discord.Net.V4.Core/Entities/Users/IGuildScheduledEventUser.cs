using Discord.Models;
using Discord.Rest;

namespace Discord;

public partial interface IGuildScheduledEventUser :
    ISnowflakeEntity<IGuildScheduledEventUserModel>,
    IGuildScheduledEventUserActor;
