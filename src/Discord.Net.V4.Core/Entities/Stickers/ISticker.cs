using Discord.Models;
using Discord.Rest;

namespace Discord;

/// <summary>
///     Represents a discord sticker.
/// </summary>
public partial interface ISticker :
    ISnowflakeEntity<IStickerModel>,
    IStickerActor
{
    /// <summary>
    ///     The name of the sticker.
    /// </summary>
    string Name { get; }

    /// <summary>
    ///     The format of the sticker.
    /// </summary>
    StickerFormatType Format { get; }

    IStickerPackActor? Pack { get; }

    /// <summary>
    ///     Gets the description of this sticker.
    /// </summary>
    /// <returns>
    ///     A <see langword="string" /> with the description of this sticker.
    /// </returns>
    string? Description { get; }

    /// <summary>
    ///     Gets the list of tags of this sticker.
    /// </summary>
    /// <returns>
    ///     A read-only list with the tags of this sticker.
    /// </returns>
    IReadOnlyCollection<string> Tags { get; }

    /// <summary>
    ///     Gets the type of this sticker.
    /// </summary>
    StickerType Type { get; }

    /// <summary>
    ///     Gets the standard sticker's sort order within its pack.
    /// </summary>
    int? SortOrder { get; }
}
