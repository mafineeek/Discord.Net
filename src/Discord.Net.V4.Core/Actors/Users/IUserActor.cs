using Discord.Models;
using Discord.Models.Json;
using Discord.Rest;

namespace Discord;

[
    Loadable(nameof(Routes.GetUser)), 
    BackLinkable, 
    Refreshable(nameof(Routes.GetUser))
]
public partial interface IUserActor :
    IActor<ulong, IUser>
{
    [return: TypeHeuristic(nameof(CreateEntity))]
    async Task<IDMChannel> CreateDMAsync(RequestOptions? options = null, CancellationToken token = default)
    {
        var model = await Client.RestApiClient.ExecuteRequiredAsync(
            Routes.CreateDm(new CreateDMChannelParams {RecipientId = Id}),
            options ?? Client.DefaultRequestOptions,
            token
        );

        return (IDMChannel)Client.Channels.CreateEntity(model);
    }

    [BackLink<IGroupChannelActor>]
    private static async Task AddAsync(
        IGroupChannelActor channel,
        EntityOrId<ulong, IUserActor> user,
        string accessToken,
        string nickname,
        RequestOptions? options = null,
        CancellationToken token = default)
    {
        await channel.Client.RestApiClient.ExecuteAsync(
            Routes.GroupDmAddRecipient(channel.Id, user.Id, new GroupDmAddRecipientParams()
            {
                Nick = nickname,
                AccessToken = accessToken
            }),
            options ?? channel.Client.DefaultRequestOptions,
            token
        );
    }

    [BackLink<IGroupChannelActor>]
    private static async Task RemoveAsync(
        IGroupChannelActor channel,
        EntityOrId<ulong, IUserActor> user,
        RequestOptions? options = null,
        CancellationToken token = default)
    {
        await channel.Client.RestApiClient.ExecuteAsync(
            Routes.GroupDmRemoveRecipient(channel.Id, user.Id),
            options ?? channel.Client.DefaultRequestOptions,
            token
        );
    }

    [LinkExtension]
    private interface WithCurrentExtension
    {
        [LinkMirror(OnlyBackLinks = true)]
        ICurrentUserActor Current { get; }
    }
}
