using System.Diagnostics.CodeAnalysis;
using Discord.Models.Json;
using Discord.Rest;

namespace Discord;

[
    Loadable(nameof(Routes.GetChannel), typeof(ThreadChannelBase)),
    Modifiable<ModifyThreadChannelProperties>(nameof(Routes.ModifyChannel)),
    Creatable<CreateThreadFromMessageProperties>(
        nameof(Routes.StartThreadFromMessage),
        nameof(IThreadableChannelActor),
        MethodName = "CreateFromMessageAsync"
    ),
    Creatable<CreateThreadWithoutMessageProperties>(
        nameof(Routes.StartThreadWithoutMessage),
        nameof(IThreadableChannelActor),
        MethodName = "CreateAsync"
    ),
    Creatable<CreateThreadInForumOrMediaProperties>(
        nameof(Routes.StartThreadInForum),
        nameof(IForumChannelActor),
        nameof(IMediaChannelActor),
        MethodName = "CreateAsync"
    ),
    RelationshipName("Thread"),
    SuppressMessage("ReSharper", "PossibleInterfaceMemberAmbiguity")
]
public partial interface IGuildThreadChannelActor :
    IThreadChannelActor,
    IGuildActor.CanonicalRelationship
{
    //[SourceOfTruth]
    new IGuildThreadMemberActor.Enumerable.Indexable.WithCurrentMember.WithPagedVariant.BackLink<IGuildThreadChannelActor> Members { get; }
    //
    // override 

    IThreadMemberActor.Enumerable.Indexable.WithCurrentMember.WithPagedVariant.BackLink<IThreadChannelActor> IThreadChannelActor.Members => Members;

    [LinkExtension]
    private interface WithActiveExtension
    {
        IGuildThreadChannelActor.Enumerable Active { get; }
    }

    [LinkExtension]
    private interface WithNestedThreadsExtension
    {
        IGuildThreadChannelActor.Paged<PagePublicArchivedThreadsParams> PublicArchivedThreads { get; }
        IGuildThreadChannelActor.Paged<PagePrivateArchivedThreadsParams> PrivateArchivedThreads { get; }
        IGuildThreadChannelActor.Paged<PageJoinedPrivateArchivedThreadsParams> JoinedPrivateArchivedThreads { get; }
    }
}