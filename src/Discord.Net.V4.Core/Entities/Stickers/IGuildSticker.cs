using Discord.Models;
using Discord.Rest;
using System.Diagnostics.CodeAnalysis;

namespace Discord;

/// <summary>
///     Represents a custom sticker within a guild.
/// </summary>
public partial interface IGuildSticker :
    ISnowflakeEntity<IGuildStickerModel>,
    ISticker,
    IGuildStickerActor
{
    [SourceOfTruth]
    new IGuildStickerModel GetModel();

    /// <summary>
    ///     Gets the user that uploaded the guild sticker.
    /// </summary>
    IMemberActor? Author { get; }

    /// <summary>
    ///     Gets whether this guild sticker can be used, may be false due to loss of Server Boosts.
    /// </summary>
    bool? IsAvailable { get; }
}
