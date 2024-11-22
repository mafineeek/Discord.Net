using Discord.Models;
using Discord.Rest;

namespace Discord;

public partial interface IIntegration :
    ISnowflakeEntity<IIntegrationModel>,
    IIntegrationActor
{
    string Name { get; }
    IntegrationType Type { get; }
    bool IsEnabled { get; }
    bool? IsSyncing { get; }
    IRoleActor? Role { get; }
    bool? EmoticonsEnabled { get; }
    IntegrationExpireBehavior? ExpireBehavior { get; }
    int? ExpiryGracePeriod { get; }
    IUserActor? User { get; }
    IntegrationAccount? Account { get; }
    DateTimeOffset? SyncedAt { get; }
    int? SubscriberCount { get; }
    bool? IsRevoked { get; }
    IntegrationApplication? Application { get; }
    IReadOnlyCollection<string> Scopes { get; }
}
