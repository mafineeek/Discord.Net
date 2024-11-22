using Discord.Models;
using Discord.Rest;

namespace Discord;

public partial interface IGuildTemplate :
    IEntity<string, IGuildTemplateModel>,
    IGuildTemplateFromGuildActor
{
    IUserActor Creator { get; }
    
    string Name { get; }
    string? Description { get; }
    int UsageCount { get; }
    DateTimeOffset CreatedAt { get; }
    DateTimeOffset UpdatedAt { get; }
    bool IsDirty { get; }
}