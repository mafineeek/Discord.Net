using Discord.Models;
using Discord.Rest;

namespace Discord;

[
    Deletable(nameof(Routes.DeleteGuildIntegration)), 
    FetchableOfMany(nameof(Routes.GetGuildIntegrations))
]
public partial interface IIntegrationActor :
    IGuildActor.CanonicalRelationship,
    IEntityProvider<IIntegration, IIntegrationModel>,
    IActor<ulong, IIntegration>;