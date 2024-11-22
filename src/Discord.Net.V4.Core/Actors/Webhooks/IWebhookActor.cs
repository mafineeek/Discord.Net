using Discord.Models;
using Discord.Models.Json;
using Discord.Rest;

namespace Discord;

[
    Loadable(nameof(Routes.GetWebhook)),
    Modifiable<ModifyWebhookProperties>(nameof(Routes.ModifyWebhook)),
    Deletable(nameof(Routes.DeleteWebhook)),
    FetchableOfMany(nameof(Routes.GetGuildWebhooks)),
    FetchableOfMany(nameof(Routes.GetChannelWebhooks)),
    Refreshable(nameof(Routes.GetWebhook))
]
public partial interface IWebhookActor :
    IActor<ulong, IWebhook>;