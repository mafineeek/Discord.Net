using Discord.Models;
using Discord.Models.Json;
using Discord.Rest.Extensions;

namespace Discord.Rest;

[ExtendInterfaceDefaults]
public partial class RestGuildEmoteActor :
    RestActor<ulong, RestGuildEmote, GuildEmoteIdentity>,
    IGuildEmoteActor
{
    [SourceOfTruth] public RestGuildActor Guild { get; }

    internal override GuildEmoteIdentity Identity { get; }

    [method: TypeFactory]
    public RestGuildEmoteActor(
        DiscordRestClient client,
        GuildIdentity guild,
        GuildEmoteIdentity emote
    ) : base(client, emote)
    {
        Identity = emote | this;
        Guild = guild.Actor ?? new(client, guild);
    }

    [SourceOfTruth]
    internal RestGuildEmote CreateEntity(ICustomEmoteModel model)
        => RestGuildEmote.Construct(Client, Guild.Identity, model);
}

public sealed partial class RestGuildEmote :
    RestEntity<ulong>,
    IGuildEmote,
    IRestConstructable<RestGuildEmote, RestGuildEmoteActor, ICustomEmoteModel>
{
    public IDefinedLoadableEntityEnumerable<ulong, IRole> Roles => throw new NotImplementedException();

    [SourceOfTruth] public RestUserActor? Creator { get; private set; }

    public string Name => Model.Name;

    public bool IsManaged => Model.IsManaged;

    public bool RequireColons => Model.RequireColons;

    public bool IsAnimated => Model.IsAnimated;

    public bool IsAvailable => Model.IsAvailable;

    [ProxyInterface(
        typeof(IGuildEmoteActor),
        typeof(IGuildRelationship),
        typeof(IEntityProvider<IGuildEmote, ICustomEmoteModel>)
    )]
    internal RestGuildEmoteActor Actor { get; }

    internal ICustomEmoteModel Model { get; private set; }

    internal RestGuildEmote(
        DiscordRestClient client,
        GuildIdentity guild,
        ICustomEmoteModel model,
        RestGuildEmoteActor? actor = null
    ) : base(client, model.Id)
    {
        Actor = actor ?? new(client, guild, GuildEmoteIdentity.Of(this));
        Model = model;
    }

    public static RestGuildEmote Construct(DiscordRestClient client, GuildIdentity guild, ICustomEmoteModel model)
        => new(client, guild, model);

    public ValueTask UpdateAsync(ICustomEmoteModel model, CancellationToken token = default)
    {
        Creator = Creator.UpdateFrom(
            model.UserId,
            RestUserActor.Factory,
            Client
        );

        Model = model;

        return ValueTask.CompletedTask;
    }

    public ICustomEmoteModel GetModel() => Model;
}
