using Discord.Rest;

namespace Discord;

[
    Loadable(nameof(Routes.GetStickerPack)),
    FetchableOfMany(nameof(Routes.ListStickerPacks)),
    Refreshable(nameof(Routes.GetStickerPack)),
    Fetchable(nameof(Routes.GetStickerPack))
]
public partial interface IStickerPackActor :
    IActor<ulong, IStickerPack>;