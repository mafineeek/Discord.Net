using System.Text.Json.Serialization;

namespace Discord.API;

public sealed class MessageInteraction
{
    [JsonPropertyName("id")]
    public ulong Id { get; set; }

    [JsonPropertyName("type")]
    public InteractionType Type { get; set; }

    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("user")]
    public required User User { get; set; }

    [JsonPropertyName("member")]
    public Optional<GuildMember> Member { get; set; }
}
