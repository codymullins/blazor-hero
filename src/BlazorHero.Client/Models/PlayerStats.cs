namespace BlazorHero.Client.Models;

public record PlayerStats
{
    public int Score { get; init; }
    public int MaxCombo { get; init; }
    public int PerfectCount { get; init; }
    public int GreatCount { get; init; }
    public int GoodCount { get; init; }
    public int MissCount { get; init; }
    public int TotalNotes { get; init; }
    public double Accuracy { get; init; }
    public int StarRating { get; init; }  // 0-5 stars

    public int NotesHit => PerfectCount + GreatCount + GoodCount;
    public double HitPercentage => TotalNotes > 0 ? (NotesHit * 100.0 / TotalNotes) : 0;
    public bool IsFullCombo => MissCount == 0 && TotalNotes > 0;
    public bool IsPerfectGame => MissCount == 0 && GoodCount == 0 && GreatCount == 0 && TotalNotes > 0;

    public string GetGrade()
    {
        if (IsPerfectGame) return "S+";
        if (IsFullCombo) return "S";

        return Accuracy switch
        {
            >= 95 => "A+",
            >= 90 => "A",
            >= 85 => "B+",
            >= 80 => "B",
            >= 70 => "C",
            >= 60 => "D",
            _ => "F"
        };
    }

    public static PlayerStats Empty => new()
    {
        Score = 0,
        MaxCombo = 0,
        PerfectCount = 0,
        GreatCount = 0,
        GoodCount = 0,
        MissCount = 0,
        TotalNotes = 0,
        Accuracy = 0,
        StarRating = 0
    };
}
