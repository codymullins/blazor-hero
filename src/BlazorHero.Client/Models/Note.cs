using System.Text.Json.Serialization;

namespace BlazorHero.Client.Models;

public class Note
{
    [JsonPropertyName("time")]
    public double Time { get; set; }  // Time in milliseconds

    [JsonPropertyName("lane")]
    public int LaneIndex { get; set; }

    [JsonPropertyName("duration")]
    public double Duration { get; set; }  // For hold notes (0 = tap note)

    [JsonPropertyName("isStarPower")]
    public bool IsStarPower { get; set; }

    // Computed property
    [JsonIgnore]
    public Lane Lane => (Lane)LaneIndex;

    // Runtime state (for active notes during gameplay)
    [JsonIgnore]
    public bool IsHit { get; set; }

    [JsonIgnore]
    public bool IsMissed { get; set; }

    [JsonIgnore]
    public double HoldProgress { get; set; }  // 0-1 for hold notes

    [JsonIgnore]
    public bool IsHoldActive { get; set; }  // Currently being held

    [JsonIgnore]
    public double CurrentZ { get; set; }  // Current position (0=hit line, 1=horizon)

    public bool IsHoldNote => Duration > 0;
    public bool IsActive => !IsHit && !IsMissed;

    public Note Clone() => new()
    {
        Time = Time,
        LaneIndex = LaneIndex,
        Duration = Duration,
        IsStarPower = IsStarPower,
        IsHit = false,
        IsMissed = false,
        HoldProgress = 0,
        IsHoldActive = false,
        CurrentZ = 1
    };
}
