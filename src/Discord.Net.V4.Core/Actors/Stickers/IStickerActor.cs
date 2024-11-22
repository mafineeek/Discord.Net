using Discord.Rest;

namespace Discord;

[
    Loadable(nameof(Routes.GetSticker)),
    Refreshable(nameof(Routes.GetSticker))
]
public partial interface IStickerActor :
    IActor<ulong, ISticker>;
