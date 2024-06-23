namespace Discord;

/// <summary>
///     Represents a generic private group channel.
/// </summary>
public interface IGroupChannel :
    IMessageChannel,
    IAudioChannel,
    IGroupChannelActor
{
    /// <summary>
    ///     Gets the users that can access this channel.
    /// </summary>
    /// <returns>
    ///     A <see cref="IDefinedLoadableEntityEnumerable{TId,TEntity}" /> of users that can access this channel.
    /// </returns>
    IDefinedLoadableEntityEnumerable<ulong, IUser> Recipients { get; }
}
