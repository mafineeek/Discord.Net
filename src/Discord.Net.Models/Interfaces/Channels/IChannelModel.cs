namespace Discord.Models;

[ModelEquality]
public partial interface IChannelModel : IEntityModel<ulong>
{
    int Type { get; }
}
