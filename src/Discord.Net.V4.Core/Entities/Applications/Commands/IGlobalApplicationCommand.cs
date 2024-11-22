using Discord.Rest;

namespace Discord;

public partial interface IGlobalApplicationCommand :
    IApplicationCommand,
    IGlobalApplicationCommandActor;