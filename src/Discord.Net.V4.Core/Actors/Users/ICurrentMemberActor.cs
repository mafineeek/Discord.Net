using Discord.Rest;
using System.Diagnostics.CodeAnalysis;

namespace Discord;

[Loadable(nameof(Routes.GetCurrentUserGuildMember))]
[Modifiable<ModifyCurrentMemberProperties>(nameof(Routes.ModifyCurrentMember))]
[SuppressMessage("ReSharper", "PossibleInterfaceMemberAmbiguity")]
public partial interface ICurrentMemberActor :
    IMemberActor,
    IActor<ulong, ICurrentMember>
{
    [SourceOfTruth]
    new ICurrentUserActor User { get; }

    [SourceOfTruth]
    new ICurrentUserVoiceStateActor VoiceState { get; }
}
