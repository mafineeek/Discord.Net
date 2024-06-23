using Discord.Models.Json;

namespace Discord.Models;

public interface IGuildForumChannelModel : IGuildChannelModel
{
    bool IsNsfw { get; }
    string? Topic { get; }
    int DefaultAutoArchiveDuration { get; }
    int? RatelimitPerUser { get; }
    int? DefaultThreadRateLimitPerUser { get; }
    IEmote? DefaultReactionEmoji { get; }
    IEnumerable<ITagModel> AvailableTags { get; }
    int? DefaultSortOrder { get; }
    int DefaultForumLayout { get; }
}
