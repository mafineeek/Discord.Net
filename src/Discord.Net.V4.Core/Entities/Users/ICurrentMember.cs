using Discord.Rest;
using System.Diagnostics.CodeAnalysis;

namespace Discord;

public partial interface ICurrentMember :
    IMember,
    ICurrentMemberActor;
