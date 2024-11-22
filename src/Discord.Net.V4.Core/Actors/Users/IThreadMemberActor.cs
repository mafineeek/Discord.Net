using Discord.Rest;

namespace Discord;

[
    Loadable(nameof(Routes.GetThreadMember)),
    Deletable(nameof(Routes.RemoveThreadMember)),
    FetchableOfMany(nameof(Routes.ListThreadMembers)),
    PagedFetchableOfMany<PageThreadMembersParams>(nameof(Routes.ListThreadMembersPaged)),
    Refreshable(nameof(Routes.GetThreadMember))
]
public partial interface IThreadMemberActor :
    IActor<ulong, IThreadMember>,
    IThreadChannelActor.CanonicalRelationship,
    IUserActor.Relationship
{
    [LinkExtension]
    protected interface WithCurrentMemberExtension
    {
        IThreadMemberActor Current { get; }
    }

    [LinkExtension]
    protected interface WithPagedVariantExtension
    {
        IThreadMemberActor.Paged<PageThreadMembersParams>.Indexable AsPaged { get; }
    }

    [BackLink<IThreadChannelActor>]
    private static Task AddAsync(
        IThreadChannelActor thread,
        EntityOrId<ulong, IUserActor> user,
        RequestOptions? options = null,
        CancellationToken token = default)
    {
        return thread.Client.RestApiClient.ExecuteAsync(
            Routes.AddThreadMember(thread.Id, user.Id),
            options ?? thread.Client.DefaultRequestOptions,
            token
        );
    }

    [BackLink<IThreadChannelActor>]
    private static Task RemoveAsync(
        IThreadChannelActor thread,
        EntityOrId<ulong, IUserActor> user,
        RequestOptions? options = null,
        CancellationToken token = default)
    {
        return thread.Client.RestApiClient.ExecuteAsync(
            Routes.RemoveThreadMember(thread.Id, user.Id),
            options ?? thread.Client.DefaultRequestOptions,
            token
        );
    }
}