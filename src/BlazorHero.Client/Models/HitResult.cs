namespace BlazorHero.Client.Models;

public enum HitJudgment
{
    Perfect,
    Great,
    Good,
    Miss
}

public record HitResult
{
    public HitJudgment Judgment { get; init; }
    public double TimingOffset { get; init; }  // Negative = early, positive = late
    public int ScoreAwarded { get; init; }
    public Note Note { get; init; } = null!;
    public Lane Lane { get; init; }

    public bool IsHit => Judgment != HitJudgment.Miss;

    public string GetJudgmentText() => Judgment switch
    {
        HitJudgment.Perfect => "PERFECT",
        HitJudgment.Great => "GREAT",
        HitJudgment.Good => "GOOD",
        HitJudgment.Miss => "MISS",
        _ => ""
    };

    public string GetJudgmentColor() => Judgment switch
    {
        HitJudgment.Perfect => "#FFD700",  // Gold
        HitJudgment.Great => "#22C55E",     // Green
        HitJudgment.Good => "#3B82F6",      // Blue
        HitJudgment.Miss => "#EF4444",      // Red
        _ => "#FFFFFF"
    };
}
