namespace Discord;

/// <summary>
///     Represents a class containing the strings related to various Content Delivery Networks (CDNs).
/// </summary>
public static class CDN
{
    /// <summary>
    ///     Returns a team icon URL.
    /// </summary>
    /// <param name="config">The client configuration.</param>
    /// <param name="teamId">The team identifier.</param>
    /// <param name="iconId">The icon identifier.</param>
    /// <returns>
    ///     A URL pointing to the team's icon.
    /// </returns>
    public static string? GetTeamIconUrl(DiscordConfig config, ulong teamId, string? iconId)
        => iconId != null ? $"{config.CDNUrl}team-icons/{teamId}/{iconId}.jpg" : null;

    /// <summary>
    ///     Returns an application icon URL.
    /// </summary>
    /// <param name="config">The client configuration.</param>
    /// <param name="appId">The application identifier.</param>
    /// <param name="iconId">The icon identifier.</param>
    /// <returns>
    ///     A URL pointing to the application's icon.
    /// </returns>
    public static string? GetApplicationIconUrl(DiscordConfig config, ulong appId, string? iconId)
        => iconId != null ? $"{config.CDNUrl}app-icons/{appId}/{iconId}.jpg" : null;

    /// <summary>
    ///     Returns a user avatar URL.
    /// </summary>
    /// <param name="config">The client configuration.</param>
    /// <param name="userId">The user snowflake identifier.</param>
    /// <param name="avatarId">The avatar identifier.</param>
    /// <param name="size">
    ///     The size of the image to return in horizontal pixels. This can be any power of two between 16 and
    ///     2048.
    /// </param>
    /// <param name="format">The format to return.</param>
    /// <returns>
    ///     A URL pointing to the user's avatar in the specified size.
    /// </returns>
    public static string? GetUserAvatarUrl(DiscordConfig config, ulong userId, string? avatarId, ushort size,
        ImageFormat format)
    {
        if (avatarId == null)
            return null;
        var extension = FormatToExtension(format, avatarId);
        return $"{config.CDNUrl}avatars/{userId}/{avatarId}.{extension}?size={size}";
    }

    /// <summary>
    ///     Returns a guild users avatar URL.
    /// </summary>
    /// <param name="config">The client configuration.</param>
    /// <param name="userId">The user snowflake identifier.</param>
    /// <param name="guildId">The guild snowflake identifier.</param>
    /// <param name="avatarId">The avatar identifier.</param>
    /// <param name="size">
    ///     The size of the image to return in horizontal pixels. This can be any power of two between 16 and
    ///     2048.
    /// </param>
    /// <param name="format">The format to return.</param>
    /// <returns>
    ///     A URL pointing to the user's avatar in the specified size.
    /// </returns>
    public static string? GetGuildUserAvatarUrl(DiscordConfig config, ulong userId, ulong guildId, string? avatarId,
        ushort size, ImageFormat format)
    {
        if (avatarId == null)
            return null;
        var extension = FormatToExtension(format, avatarId);
        return $"{config.CDNUrl}guilds/{guildId}/users/{userId}/avatars/{avatarId}.{extension}?size={size}";
    }

    /// <summary>
    ///     Returns a user banner URL.
    /// </summary>
    /// <param name="config">The client configuration.</param>
    /// <param name="userId">The user snowflake identifier.</param>
    /// <param name="bannerId">The banner identifier.</param>
    /// <param name="size">
    ///     The size of the image to return in horizontal pixels. This can be any power of two between 16 and
    ///     2048.
    /// </param>
    /// <param name="format">The format to return.</param>
    /// <returns>
    ///     A URL pointing to the user's banner in the specified size.
    /// </returns>
    public static string? GetUserBannerUrl(DiscordConfig config, ulong userId, string? bannerId, ushort size,
        ImageFormat format)
    {
        if (bannerId == null)
            return null;
        var extension = FormatToExtension(format, bannerId);
        return $"{config.CDNUrl}banners/{userId}/{bannerId}.{extension}?size={size}";
    }

    /// <summary>
    ///     Returns the default user avatar URL.
    /// </summary>
    /// <param name="config">The client configuration.</param>
    /// <param name="discriminator">The discriminator value of a user.</param>
    /// <returns>
    ///     A URL pointing to the user's default avatar when one isn't set.
    /// </returns>
    public static string GetDefaultUserAvatarUrl(DiscordConfig config, ushort discriminator) =>
        $"{config.CDNUrl}embed/avatars/{discriminator % 5}.png";

    /// <summary>
    ///     Returns the default user avatar URL.
    /// </summary>
    /// <param name="config">The client configuration.</param>
    /// <param name="userId">The Id of a user.</param>
    /// <returns>
    ///     A URL pointing to the user's default avatar when one isn't set.
    /// </returns>
    public static string GetDefaultUserAvatarUrl(DiscordConfig config, ulong userId) =>
        $"{config.CDNUrl}embed/avatars/{(userId >> 22) % 6}.png";

    /// <summary>
    ///     Returns an icon URL.
    /// </summary>
    /// <param name="config">The client configuration.</param>
    /// <param name="guildId">The guild snowflake identifier.</param>
    /// <param name="iconId">The icon identifier.</param>
    /// <returns>
    ///     A URL pointing to the guild's icon.
    /// </returns>
    public static string? GetGuildIconUrl(DiscordConfig config, ulong guildId, string? iconId)
        => iconId != null ? $"{config.CDNUrl}icons/{guildId}/{iconId}.jpg" : null;

    /// <summary>
    ///     Returns a guild role's icon URL.
    /// </summary>
    /// <param name="config">The client configuration.</param>
    /// <param name="roleId">The role identifier.</param>
    /// <param name="roleHash">The icon hash.</param>
    /// <returns>
    ///     A URL pointing to the guild role's icon.
    /// </returns>
    public static string? GetGuildRoleIconUrl(DiscordConfig config, ulong roleId, string? roleHash)
        => roleHash != null ? $"{config.CDNUrl}role-icons/{roleId}/{roleHash}.png" : null;

    /// <summary>
    ///     Returns a guild splash URL.
    /// </summary>
    /// <param name="config">The client configuration.</param>
    /// <param name="guildId">The guild snowflake identifier.</param>
    /// <param name="splashId">The splash icon identifier.</param>
    /// <returns>
    ///     A URL pointing to the guild's splash.
    /// </returns>
    public static string? GetGuildSplashUrl(DiscordConfig config, ulong guildId, string? splashId)
        => splashId != null ? $"{config.CDNUrl}splashes/{guildId}/{splashId}.jpg" : null;

    /// <summary>
    ///     Returns a guild discovery splash URL.
    /// </summary>
    /// <param name="config">The client configuration.</param>
    /// <param name="guildId">The guild snowflake identifier.</param>
    /// <param name="discoverySplashId">The discovery splash icon identifier.</param>
    /// <returns>
    ///     A URL pointing to the guild's discovery splash.
    /// </returns>
    public static string? GetGuildDiscoverySplashUrl(DiscordConfig config, ulong guildId, string? discoverySplashId)
        => discoverySplashId != null ? $"{config.CDNUrl}discovery-splashes/{guildId}/{discoverySplashId}.jpg" : null;

    /// <summary>
    ///     Returns a channel icon URL.
    /// </summary>
    /// <param name="config">The client configuration.</param>
    /// <param name="channelId">The channel snowflake identifier.</param>
    /// <param name="iconId">The icon identifier.</param>
    /// <returns>
    ///     A URL pointing to the channel's icon.
    /// </returns>
    public static string? GetChannelIconUrl(DiscordConfig config, ulong channelId, string? iconId)
        => iconId != null ? $"{config.CDNUrl}channel-icons/{channelId}/{iconId}.jpg" : null;

    /// <summary>
    ///     Returns a guild banner URL.
    /// </summary>
    /// <param name="config">The client configuration.</param>
    /// <param name="guildId">The guild snowflake identifier.</param>
    /// <param name="bannerId">The banner image identifier.</param>
    /// <param name="format">The format to return.</param>
    /// <param name="size">
    ///     The size of the image to return in horizontal pixels. This can be any power of two between 16 and
    ///     2048 inclusive.
    /// </param>
    /// <returns>
    ///     A URL pointing to the guild's banner image.
    /// </returns>
    public static string? GetGuildBannerUrl(DiscordConfig config, ulong guildId, string? bannerId, ImageFormat format,
        ushort? size = null)
    {
        if (string.IsNullOrEmpty(bannerId))
            return null;
        var extension = FormatToExtension(format, bannerId);
        return $"{config.CDNUrl}banners/{guildId}/{bannerId}.{extension}" +
               (size.HasValue ? $"?size={size}" : string.Empty);
    }

    /// <summary>
    ///     Returns an emoji URL.
    /// </summary>
    /// <param name="config">The client configuration.</param>
    /// <param name="emojiId">The emoji snowflake identifier.</param>
    /// <param name="animated">Whether this emoji is animated.</param>
    /// <returns>
    ///     A URL pointing to the custom emote.
    /// </returns>
    public static string GetEmojiUrl(DiscordConfig config, ulong emojiId, bool animated)
        => $"{config.CDNUrl}emojis/{emojiId}.{(animated ? "gif" : "png")}";

    /// <summary>
    ///     Returns a Rich Presence asset URL.
    /// </summary>
    /// <param name="config">The client configuration.</param>
    /// <param name="appId">The application identifier.</param>
    /// <param name="assetId">The asset identifier.</param>
    /// <param name="size">The size of the image to return in. This can be any power of two between 16 and 2048.</param>
    /// <param name="format">The format to return.</param>
    /// <returns>
    ///     A URL pointing to the asset image in the specified size.
    /// </returns>
    public static string GetRichAssetUrl(DiscordConfig config, ulong appId, string assetId, ushort size,
        ImageFormat format)
    {
        var extension = FormatToExtension(format, "");
        return $"{config.CDNUrl}app-assets/{appId}/{assetId}.{extension}?size={size}";
    }

    /// <summary>
    ///     Returns a Spotify album URL.
    /// </summary>
    /// <param name="albumArtId">The identifier for the album art (e.g. 6be8f4c8614ecf4f1dd3ebba8d8692d8ce4951ac).</param>
    /// <returns>
    ///     A URL pointing to the Spotify album art.
    /// </returns>
    public static string GetSpotifyAlbumArtUrl(string albumArtId)
        => $"https://i.scdn.co/image/{albumArtId}";

    /// <summary>
    ///     Returns a Spotify direct URL for a track.
    /// </summary>
    /// <param name="trackId">The identifier for the track (e.g. 4uLU6hMCjMI75M1A2tKUQC).</param>
    /// <returns>
    ///     A URL pointing to the Spotify track.
    /// </returns>
    public static string GetSpotifyDirectUrl(string trackId)
        => $"https://open.spotify.com/track/{trackId}";

    /// <summary>
    ///     Gets a stickers url based off the id and format.
    /// </summary>
    /// <param name="config">The client configuration.</param>
    /// <param name="stickerId">The id of the sticker.</param>
    /// <param name="format">The format of the sticker.</param>
    /// <returns>
    ///     A URL to the sticker.
    /// </returns>
    public static string GetStickerUrl(DiscordConfig config, ulong stickerId,
        StickerFormatType format = StickerFormatType.Png)
        => $"{config.CDNUrl}stickers/{stickerId}.{FormatToExtension(format)}";

    /// <summary>
    ///     Returns an events cover image url.
    /// </summary>
    /// <param name="config">The client configuration.</param>
    /// <param name="guildId">The guild id that the event is in.</param>
    /// <param name="eventId">The id of the event.</param>
    /// <param name="assetId">The id of the cover image asset.</param>
    /// <param name="format">The format of the image.</param>
    /// <param name="size">The size of the image.</param>
    /// <returns></returns>
    public static string GetEventCoverImageUrl(DiscordConfig config, ulong guildId, ulong eventId, string assetId,
        ImageFormat format = ImageFormat.Auto, ushort size = 1024)
        => $"{config.CDNUrl}guild-events/{eventId}/{assetId}.{FormatToExtension(format, assetId)}?size={size}";

    private static string FormatToExtension(StickerFormatType format) =>
        format switch
        {
            StickerFormatType.None or StickerFormatType.Png
                or StickerFormatType.Apng =>
                "png", // In the case of the Sticker endpoint, the sticker will be available as PNG if its format_type is PNG or APNG, and as Lottie if its format_type is LOTTIE.
            StickerFormatType.Lottie => "lottie",
            _ => throw new ArgumentException(nameof(format))
        };

    private static string FormatToExtension(ImageFormat format, string imageId)
    {
        if (format == ImageFormat.Auto)
            format = imageId.StartsWith("a_") ? ImageFormat.Gif : ImageFormat.Png;
        return format switch
        {
            ImageFormat.Gif => "gif",
            ImageFormat.Jpeg => "jpeg",
            ImageFormat.Png => "png",
            ImageFormat.WebP => "webp",
            _ => throw new ArgumentException(nameof(format))
        };
    }
}
