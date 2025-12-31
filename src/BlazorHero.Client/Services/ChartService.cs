using System.Net.Http.Json;
using BlazorHero.Client.Core;
using BlazorHero.Client.Models;

namespace BlazorHero.Client.Services;

public class ChartService
{
    private readonly HttpClient _http;
    private readonly Dictionary<string, Chart> _chartCache = new();
    private List<SongInfo>? _songList;

    public ChartService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<SongInfo>> GetSongListAsync()
    {
        if (_songList != null) return _songList;

        try
        {
            var index = await _http.GetFromJsonAsync("charts/index.json", BlazorHeroJsonContext.Default.SongIndex);
            if (index == null)
            {
                _songList = new List<SongInfo>();
                return _songList;
            }

            _songList = new List<SongInfo>();

            foreach (var entry in index.Songs)
            {
                try
                {
                    var chart = await LoadChartAsync(entry.ChartFile);
                    if (chart != null)
                    {
                        var difficulties = chart.GetAvailableDifficulties();
                        var noteCounts = new Dictionary<Difficulty, int>();
                        foreach (var diff in difficulties)
                        {
                            var track = chart.GetTrack(diff);
                            noteCounts[diff] = track?.Notes.Count ?? 0;
                        }

                        _songList.Add(new SongInfo
                        {
                            ChartFile = entry.ChartFile,
                            Meta = chart.Meta,
                            AvailableDifficulties = difficulties,
                            NoteCounts = noteCounts
                        });
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to load chart {entry.ChartFile}: {ex.Message}");
                }
            }

            return _songList;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load song index: {ex.Message}");
            _songList = new List<SongInfo>();
            return _songList;
        }
    }

    public async Task<Chart?> LoadChartAsync(string chartFile)
    {
        if (_chartCache.TryGetValue(chartFile, out var cached))
        {
            return cached;
        }

        try
        {
            var chart = await _http.GetFromJsonAsync($"charts/{chartFile}", BlazorHeroJsonContext.Default.Chart);
            if (chart == null) return null;

            // Sort notes by time for each difficulty
            foreach (var track in chart.Difficulties.Values)
            {
                track.Notes.Sort((a, b) => a.Time.CompareTo(b.Time));
            }

            _chartCache[chartFile] = chart;
            return chart;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load chart {chartFile}: {ex.Message}");
            return null;
        }
    }

    public List<Note> GetNotesForDifficulty(Chart chart, Difficulty difficulty)
    {
        var track = chart.GetTrack(difficulty);
        if (track == null) return new List<Note>();

        // Clone notes for gameplay (so we don't modify cached data)
        return track.Notes.Select(n => n.Clone()).ToList();
    }

    public void ClearCache()
    {
        _chartCache.Clear();
        _songList = null;
    }
}
