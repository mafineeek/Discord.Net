using Discord.Models;
using Discord.Rest;

namespace Discord;

public partial interface IGuildSoundboardSound :
    ISoundboardSound,
    ISnowflakeEntity<IGuildSoundboardSoundModel>,
    IGuildSoundboardSoundActor
{
    IUserActor? Creator { get; }
    
    [SourceOfTruth]
    new IGuildSoundboardSoundModel GetModel();
}