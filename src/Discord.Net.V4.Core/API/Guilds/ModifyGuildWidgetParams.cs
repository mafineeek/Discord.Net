using System.Text.Json.Serialization;

namespace Discord.API;

public sealed class ModifyGuildWidgetParams
{
    [JsonPropertyName("enabled")]
    public Optional<bool> IsEnabled { get; set; }

    [JsonPropertyName("channel_id")]
    public Optional<ulong?> ChannelId { get; set; }
}
