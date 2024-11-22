using Discord.Models;
using Discord.Rest;

namespace Discord;

public partial interface IChannel :
    ISnowflakeEntity<IChannelModel>,
    IChannelActor
{
    [TypeHeuristic] ChannelType Type { get; }
}