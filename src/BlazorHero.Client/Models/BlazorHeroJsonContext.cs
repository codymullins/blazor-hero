using System.Text.Json.Serialization;

namespace BlazorHero.Client.Models;

[JsonSerializable(typeof(Chart))]
[JsonSerializable(typeof(SongMeta))]
[JsonSerializable(typeof(SyncEvent))]
[JsonSerializable(typeof(NoteTrack))]
[JsonSerializable(typeof(StarPowerPhrase))]
[JsonSerializable(typeof(Note))]
[JsonSerializable(typeof(NoteSounds))]
[JsonSerializable(typeof(LaneSound))]
[JsonSerializable(typeof(SongIndex))]
[JsonSerializable(typeof(SongIndexEntry))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class BlazorHeroJsonContext : JsonSerializerContext
{
}
