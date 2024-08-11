using Discord.Gateway.State;
using Discord.Models;
using static Discord.Template;

namespace Discord.Gateway;


public partial class GatewayTextChannelActor :
    GatewayThreadableChannelActor,
    ITextChannelActor,
    IGatewayCachedActor<ulong, GatewayTextChannel, TextChannelIdentity, IGuildTextChannelModel>
{
    [SourceOfTruth]
    internal override TextChannelIdentity Identity { get; }

    [ProxyInterface]
    internal GatewayMessageChannelTrait MessageChannelActor { get; }

    [ProxyInterface]
    internal GatewayIntegrationChannelTrait IntegrationChannelActor { get; }

    [method: TypeFactory]
    public GatewayTextChannelActor(DiscordGatewayClient client,
        GuildIdentity guild,
        TextChannelIdentity channel) : base(client, guild, channel)
    {
        Identity = channel | this;
        MessageChannelActor = new GatewayMessageChannelTrait(client, channel, guild);
        IntegrationChannelActor = new GatewayIntegrationChannelTrait(client, guild, channel);
    }

    [SourceOfTruth]
    internal GatewayTextChannel CreateEntity(IGuildTextChannelModel model)
        => Client.StateController.CreateLatent(this, model, CachePath);
}

public partial class GatewayTextChannel :
    GatewayThreadableChannel,
    ITextChannel,
    ICacheableEntity<GatewayTextChannel, ulong, IGuildTextChannelModel>
{
    public bool IsNsfw => Model.IsNsfw;

    public string? Topic => Model.Topic;

    public int SlowModeInterval => Model.RatelimitPerUser;

    [ProxyInterface]
    internal override GatewayTextChannelActor Actor { get; }

    internal override IGuildTextChannelModel Model => _model;

    private IGuildTextChannelModel _model;

    public GatewayTextChannel(
        DiscordGatewayClient client,
        GuildIdentity guild,
        IGuildTextChannelModel model,
        GatewayTextChannelActor? actor = null
    ) : base(client, guild, model, actor)
    {
        _model = model;

        Actor = actor ?? new(client, guild, TextChannelIdentity.Of(this));

        IFetchableOfMany<ulong, IGuildChannelModel> a = this;
    }

    public static GatewayTextChannel Construct(
        DiscordGatewayClient client,
        IGatewayConstructionContext context,
        IGuildTextChannelModel model
    ) => new(
        client,
        context.Path.GetIdentity(T<GuildIdentity>(), model.GuildId),
        model,
        context.TryGetActor<GatewayTextChannelActor>()
    );

    [CovariantOverride]
    public virtual ValueTask UpdateAsync(
        IGuildTextChannelModel model,
        bool updateCache = true,
        CancellationToken token = default)
    {
        if (updateCache) return UpdateCacheAsync(this, model, token);

        _model = model;

        return base.UpdateAsync(model, false, token);
    }

    public override IGuildTextChannelModel GetModel() => Model;
}
