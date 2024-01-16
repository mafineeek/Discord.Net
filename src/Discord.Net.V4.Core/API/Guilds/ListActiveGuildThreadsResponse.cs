using System.Text.Json.Serialization;

namespace Discord.API;

public sealed class ListActiveGuildThreadsResponse
{
    [JsonPropertyName("threads")]
    public required Channel[] Threads { get; set; }

    [JsonPropertyName("members")]
    public required ThreadMember[] Members { get; set; }
}
