using Discord.Models;
using Discord.Models.Json;
using Discord.Rest.Channels;
using Discord.Rest.Extensions;

namespace Discord.Rest.Guilds;

[method: TypeFactory]
[ExtendInterfaceDefaults(typeof(IGuildScheduledEventActor))]
public partial class RestGuildScheduledEventActor(
    DiscordRestClient client,
    GuildIdentity guild,
    GuildScheduledEventIdentity scheduledEvent
) :
    RestActor<ulong, RestGuildScheduledEvent, GuildScheduledEventIdentity>(client, scheduledEvent),
    IGuildScheduledEventActor
{
    [SourceOfTruth]
    public RestGuildActor Guild { get; } = new(client, guild);

    public IEnumerableIndexableActor<IGuildScheduledEventUserActor, ulong,
        IGuildScheduledEventUser> RSVPs => throw new NotImplementedException();

    [SourceOfTruth]
    internal RestGuildScheduledEvent CreateEntity(
        IGuildScheduledEventModel model
    ) => RestGuildScheduledEvent.Construct(Client, Guild.Identity, model);
}

public sealed partial class RestGuildScheduledEvent :
    RestEntity<ulong>,
    IGuildScheduledEvent,
    IContextConstructable<RestGuildScheduledEvent, IGuildScheduledEventModel, GuildIdentity, DiscordRestClient>
{
    [SourceOfTruth]
    public RestGuildChannelActor? Channel { get; private set; }

    [SourceOfTruth]
    public RestUserActor Creator { get; }

    public string Name => Model.Name;

    public string? Description => Model.Description;

    public string? CoverImageId => Model.Image;

    public DateTimeOffset ScheduledStartTime => Model.ScheduledStartTime;

    public DateTimeOffset? ScheduledEndTime => Model.ScheduledEndTime;

    public GuildScheduledEventPrivacyLevel PrivacyLevel => (GuildScheduledEventPrivacyLevel)Model.PrivacyLevel;

    public GuildScheduledEventStatus Status => (GuildScheduledEventStatus)Model.Status;

    public GuildScheduledEntityType Type => (GuildScheduledEntityType)Model.EntityType;

    public ulong? EntityId => Model.EntityId;

    public string? Location => Model.Location;

    public int? UserCount => Model.UserCount;

    [ProxyInterface(
        typeof(IGuildScheduledEventActor),
        typeof(IGuildRelationship),
        typeof(IEntityProvider<IGuildScheduledEvent, IGuildScheduledEventModel>)
    )]
    internal RestGuildScheduledEventActor Actor { get; }

    internal IGuildScheduledEventModel Model { get; private set; }

    internal RestGuildScheduledEvent(
        DiscordRestClient client,
        GuildIdentity guild,
        IGuildScheduledEventModel model,
        RestGuildScheduledEventActor? actor = null
    ) : base(client, model.Id)
    {
        Actor = actor ?? new(client, guild, GuildScheduledEventIdentity.Of(this));
        Model = model;

        Creator = new RestUserActor(
            client,
            UserIdentity.FromReferenced(model, model.CreatorId, model => RestUser.Construct(client, model))
        );

        Channel = model.ChannelId.Map(
            static (id, client, guild)
                => new RestGuildChannelActor(client, guild, GuildChannelIdentity.Of(id)),
            client,
            guild
        );
    }

    public static RestGuildScheduledEvent Construct(DiscordRestClient client,
        GuildIdentity guild, IGuildScheduledEventModel model) =>
        new(client, guild, model);

    public ValueTask UpdateAsync(IGuildScheduledEventModel model, CancellationToken token = default)
    {
        Channel = Channel.UpdateFrom(
            model.ChannelId,
            RestGuildChannelActor.Factory,
            Client,
            Actor.Guild.Identity
        );

        Model = model;

        return ValueTask.CompletedTask;
    }

    public IGuildScheduledEventModel GetModel() => Model;
}
