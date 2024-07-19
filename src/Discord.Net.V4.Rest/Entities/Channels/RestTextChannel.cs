using Discord.Models;
using Discord.Models.Json;
using Discord.Rest.Actors;
using Discord.Rest.Guilds;

namespace Discord.Rest.Channels;

[method: TypeFactory]
[ExtendInterfaceDefaults(
    typeof(ITextChannelActor)
)]
public partial class RestTextChannelActor(
    DiscordRestClient client,
    GuildIdentity guild,
    TextChannelIdentity channel
):
    RestThreadableChannelActor(client, guild, channel),
    ITextChannelActor,
    IRestActor<ulong, RestTextChannel, TextChannelIdentity>
{
    public override TextChannelIdentity Identity { get; } = channel;

    [ProxyInterface(typeof(IMessageChannelActor))]
    internal RestMessageChannelActor MessageChannelActor { get; } = new(client, channel);

    [SourceOfTruth]
    [CovariantOverride]
    internal virtual RestTextChannel CreateEntity(IGuildTextChannelModel model)
        => RestTextChannel.Construct(Client, Guild.Identity, model);
}

public partial class RestTextChannel :
    RestThreadableChannel,
    ITextChannel,
    IContextConstructable<RestTextChannel, IGuildTextChannelModel, GuildIdentity, DiscordRestClient>
{
    public bool IsNsfw => Model.IsNsfw;

    public string? Topic => Model.Topic;

    public int SlowModeInterval => Model.RatelimitPerUser;

    [ProxyInterface(
        typeof(ITextChannelActor),
        typeof(IMessageChannelActor),
        typeof(IEntityProvider<ITextChannel, IGuildTextChannelModel>)
    )]
    internal override RestTextChannelActor Actor { get; }

    internal override IGuildTextChannelModel Model => _model;

    private IGuildTextChannelModel _model;

    internal RestTextChannel(
        DiscordRestClient client,
        GuildIdentity guild,
        IGuildTextChannelModel model,
        RestTextChannelActor? actor = null
    ) : base(client, guild, model, actor)
    {
        _model = model;
        Actor = actor ?? new(client, guild, TextChannelIdentity.Of(this));
    }

    public static RestTextChannel Construct(DiscordRestClient client, GuildIdentity guild, IGuildTextChannelModel model)
        => new(client, guild, model);

    [CovariantOverride]
    public virtual ValueTask UpdateAsync(IGuildTextChannelModel model, CancellationToken token = default)
    {
        _model = model;
        return base.UpdateAsync(model, token);
    }

    public override IGuildTextChannelModel GetModel() => Model;
}
