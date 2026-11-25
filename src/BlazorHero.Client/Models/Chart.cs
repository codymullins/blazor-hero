using System.Text.Json.Serialization;
using BlazorHero.Client.Core;

namespace BlazorHero.Client.Models;

public class Chart
{
    [JsonPropertyName("meta")]
    public SongMeta Meta { get; set; } = new();

    [JsonPropertyName("noteSounds")]
    public NoteSounds? NoteSounds { get; set; }

    [JsonPropertyName("syncTrack")]
    public List<SyncEvent> SyncTrack { get; set; } = new();

    [JsonPropertyName("difficulties")]
    public Dictionary<string, NoteTrack> Difficulties { get; set; } = new();

    public NoteTrack? GetTrack(Difficulty difficulty)
    {
        var key = difficulty.ToString().ToLowerInvariant();
        return Difficulties.TryGetValue(key, out var track) ? track : null;
    }

    public List<Difficulty> GetAvailableDifficulties()
    {
        var available = new List<Difficulty>();
        foreach (Difficulty diff in Enum.GetValues<Difficulty>())
        {
            if (GetTrack(diff)?.Notes.Count > 0)
            {
                available.Add(diff);
            }
        }
        return available;
    }
}

public class SongMeta
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("artist")]
    public string Artist { get; set; } = "";

    [JsonPropertyName("album")]
    public string Album { get; set; } = "";

    [JsonPropertyName("charter")]
    public string Charter { get; set; } = "";

    [JsonPropertyName("audioFile")]
    public string AudioFile { get; set; } = "";

    [JsonPropertyName("coverImage")]
    public string? CoverImage { get; set; }

    [JsonPropertyName("previewStart")]
    public double PreviewStart { get; set; }

    [JsonPropertyName("previewEnd")]
    public double PreviewEnd { get; set; }

    [JsonPropertyName("bpm")]
    public double Bpm { get; set; }

    [JsonPropertyName("offset")]
    public double Offset { get; set; }
}

public class SyncEvent
{
    [JsonPropertyName("time")]
    public double Time { get; set; }

    [JsonPropertyName("bpm")]
    public double Bpm { get; set; }

    [JsonPropertyName("timeSignature")]
    public int[]? TimeSignature { get; set; }
}

public class NoteTrack
{
    [JsonPropertyName("notes")]
    public List<Note> Notes { get; set; } = new();

    [JsonPropertyName("starPowerPhrases")]
    public List<StarPowerPhrase> StarPowerPhrases { get; set; } = new();
}

public class StarPowerPhrase
{
    [JsonPropertyName("startTime")]
    public double StartTime { get; set; }

    [JsonPropertyName("endTime")]
    public double EndTime { get; set; }
}

public class NoteSounds
{
    [JsonPropertyName("style")]
    public string Style { get; set; } = "guitar";

    [JsonPropertyName("lanes")]
    public List<LaneSound> Lanes { get; set; } = new();

    [JsonPropertyName("brightness")]
    public double Brightness { get; set; } = 1.0;

    [JsonPropertyName("attack")]
    public double Attack { get; set; } = 0.01;

    [JsonPropertyName("sustain")]
    public double Sustain { get; set; } = 0.6;
}

public class LaneSound
{
    [JsonPropertyName("freq")]
    public double Freq { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
}
