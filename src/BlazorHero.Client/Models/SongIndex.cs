using System.Text.Json.Serialization;
using BlazorHero.Client.Core;

namespace BlazorHero.Client.Models;

public class SongIndex
{
    [JsonPropertyName("songs")]
    public List<SongIndexEntry> Songs { get; set; } = new();
}

public class SongIndexEntry
{
    [JsonPropertyName("chartFile")]
    public string ChartFile { get; set; } = "";
}

public class SongInfo
{
    public string ChartFile { get; set; } = "";
    public SongMeta Meta { get; set; } = new();
    public List<Difficulty> AvailableDifficulties { get; set; } = new();
}
