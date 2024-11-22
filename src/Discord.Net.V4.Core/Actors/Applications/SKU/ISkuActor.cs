using Discord.Rest;

namespace Discord;

[FetchableOfMany(nameof(Routes.ListSKUs))]
public partial interface ISkuActor :
    IActor<ulong, ISku>,
    IApplicationActor.CanonicalRelationship;