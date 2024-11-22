using Discord.Rest;

namespace Discord;

public partial interface IGuildApplicationCommand :
    IApplicationCommand,
    IGuildApplicationCommandActor
{
    [SourceOfTruth]
    new IGuildActor Guild { get; }
}