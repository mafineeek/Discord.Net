using Discord.Entities.Channels.Threads;
using Discord.Models;
using Discord.Models.Json;
using Discord.Rest;

namespace Discord;

/// <summary>
///     Represents a thread channel inside a guild.
/// </summary>
public partial interface IThreadChannel :
    ISnowflakeEntity<IThreadChannelModel>,
    IMessageChannel,
    IGuildChannel,
    IThreadChannelActor
{
    [SourceOfTruth]
    new IThreadChannelModel GetModel();

    IThreadableChannelTrait<IThreadChannelActor.Indexable> Parent { get; }
    
    IUserActor Creator { get; }

    /// <summary>
    ///     Gets the type of the current thread channel.
    /// </summary>
    new ThreadType Type { get; }

    /// <summary>
    ///     Gets whether or not the current user has joined this thread.
    /// </summary>
    bool HasJoined { get; }

    /// <summary>
    ///     Gets whether or not the current thread is archived.
    /// </summary>
    bool IsArchived { get; }

    /// <summary>
    ///     Gets the duration of time before the thread is automatically archived after no activity.
    /// </summary>
    ThreadArchiveDuration AutoArchiveDuration { get; }

    /// <summary>
    ///     Gets the timestamp when the thread's archive status was last changed, used for calculating recent activity.
    /// </summary>
    DateTimeOffset ArchiveTimestamp { get; }

    /// <summary>
    ///     Gets whether or not the current thread is locked.
    /// </summary>
    bool IsLocked { get; }

    /// <summary>
    ///     Gets an approximate count of users in a thread, stops counting after 50.
    /// </summary>
    int MemberCount { get; }

    /// <summary>
    ///     Gets an approximate count of messages in a thread, stops counting after 50.
    /// </summary>
    int MessageCount { get; }

    /// <summary>
    ///     Gets whether non-moderators can add other non-moderators to a thread.
    /// </summary>
    /// <remarks>
    ///     This property is only available on private threads.
    /// </remarks>
    bool? IsInvitable { get; }

    /// <summary>
    ///     Gets ids of tags applied to a forum thread
    /// </summary>
    /// <remarks>
    ///     This property is only available on forum threads.
    /// </remarks>
    IReadOnlyCollection<ulong> AppliedTags { get; }

    /// <summary>
    ///     Gets when the thread was created.
    /// </summary>
    /// <remarks>
    ///     This property is only populated for threads created after 2022-01-09, hence the default date of this
    ///     property will be that date.
    /// </remarks>
    new DateTimeOffset CreatedAt { get; }
}
