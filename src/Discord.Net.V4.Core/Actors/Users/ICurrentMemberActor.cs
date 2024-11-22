using Discord.Rest;
using System.Diagnostics.CodeAnalysis;

namespace Discord;

[
    Loadable(nameof(Routes.GetCurrentUserGuildMember)),
    Modifiable<ModifyCurrentMemberProperties>(nameof(Routes.ModifyCurrentMember)),
    Refreshable(nameof(Routes.GetCurrentUserGuildMember))
]
public partial interface ICurrentMemberActor :
    IMemberActor,
    ICurrentUserActor,
    IActor<ulong, ICurrentMember>
{
    [SourceOfTruth] new ICurrentUserVoiceStateActor VoiceState { get; }
}