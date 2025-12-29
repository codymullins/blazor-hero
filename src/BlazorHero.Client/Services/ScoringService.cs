using BlazorHero.Client.Models;

namespace BlazorHero.Client.Services;

public class ScoringService
{
    // Hit windows (milliseconds) - generous for casual play
    public const double WINDOW_PERFECT = 60;
    public const double WINDOW_GREAT = 120;
    public const double WINDOW_GOOD = 180;
    public const double WINDOW_MISS = 250;

    // Base scores per judgment
    private const int SCORE_PERFECT = 100;
    private const int SCORE_GREAT = 75;
    private const int SCORE_GOOD = 50;

    // Multiplier settings
    private const int MAX_MULTIPLIER = 4;
    private const int NOTES_PER_MULTIPLIER = 10;

    // Current state
    private int _score;
    private int _combo;
    private int _maxCombo;
    private int _multiplier = 1;
    private int _notesSinceMultiplierIncrease;

    // Stats tracking
    private int _perfectCount;
    private int _greatCount;
    private int _goodCount;
    private int _missCount;

    // Star power
    private double _starPowerMeter;
    private bool _starPowerActive;
    private const double STAR_POWER_GAIN = 0.20;  // 5 star notes = full meter (easier to fill)
    private const double STAR_POWER_DURATION = 16000;  // 16 seconds drain time
    private const int STAR_POWER_BONUS = 25;  // Bonus points per hit during star power

    // Public properties
    public int Score => _score;
    public int Combo => _combo;
    public int MaxCombo => _maxCombo;
    public int Multiplier => _starPowerActive ? _multiplier * 2 : _multiplier;
    public int BaseMultiplier => _multiplier;
    public double StarPowerMeter => _starPowerMeter;
    public bool IsStarPowerActive => _starPowerActive;
    public bool CanActivateStarPower => _starPowerMeter >= 0.5 && !_starPowerActive;

    // Events
    public event Action<int>? ComboChanged;
    public event Action<int>? MultiplierChanged;
    public event Action<bool>? StarPowerChanged;
    public event Action? ComboBreak;

    public void Reset()
    {
        _score = 0;
        _combo = 0;
        _maxCombo = 0;
        _multiplier = 1;
        _notesSinceMultiplierIncrease = 0;
        _perfectCount = 0;
        _greatCount = 0;
        _goodCount = 0;
        _missCount = 0;
        _starPowerMeter = 0;
        _starPowerActive = false;
    }

    public HitResult ProcessHit(Note note, double songTime)
    {
        double timingOffset = songTime - note.Time;
        double absoluteOffset = Math.Abs(timingOffset);

        HitJudgment judgment;
        int baseScore;

        if (absoluteOffset <= WINDOW_PERFECT)
        {
            judgment = HitJudgment.Perfect;
            baseScore = SCORE_PERFECT;
            _perfectCount++;
        }
        else if (absoluteOffset <= WINDOW_GREAT)
        {
            judgment = HitJudgment.Great;
            baseScore = SCORE_GREAT;
            _greatCount++;
        }
        else if (absoluteOffset <= WINDOW_GOOD)
        {
            judgment = HitJudgment.Good;
            baseScore = SCORE_GOOD;
            _goodCount++;
        }
        else
        {
            judgment = HitJudgment.Miss;
            baseScore = 0;
            _missCount++;
        }

        int scoreAwarded = 0;

        if (judgment != HitJudgment.Miss)
        {
            // Calculate score with multiplier + star power bonus
            int starBonus = _starPowerActive ? STAR_POWER_BONUS : 0;
            scoreAwarded = (baseScore + starBonus) * Multiplier;
            _score += scoreAwarded;

            // Update combo
            int oldCombo = _combo;
            _combo++;
            _maxCombo = Math.Max(_maxCombo, _combo);

            if (_combo != oldCombo)
                ComboChanged?.Invoke(_combo);

            // Update multiplier
            _notesSinceMultiplierIncrease++;
            if (_notesSinceMultiplierIncrease >= NOTES_PER_MULTIPLIER &&
                _multiplier < MAX_MULTIPLIER)
            {
                _multiplier++;
                _notesSinceMultiplierIncrease = 0;
                MultiplierChanged?.Invoke(_multiplier);
            }

            // Star power gain
            if (note.IsStarPower)
            {
                _starPowerMeter = Math.Min(1.0, _starPowerMeter + STAR_POWER_GAIN);
            }
        }
        else
        {
            // Star power protects combo from breaking!
            if (_starPowerActive)
            {
                // Still counts as a miss for stats, but combo is protected
                // No score awarded, but no penalty either
            }
            else
            {
                // Break combo
                if (_combo > 0)
                {
                    ComboBreak?.Invoke();
                }
                _combo = 0;
                _multiplier = 1;
                _notesSinceMultiplierIncrease = 0;
                ComboChanged?.Invoke(_combo);
                MultiplierChanged?.Invoke(_multiplier);
            }
        }

        return new HitResult
        {
            Judgment = judgment,
            TimingOffset = timingOffset,
            ScoreAwarded = scoreAwarded,
            Note = note,
            Lane = note.Lane
        };
    }

    public void ProcessMiss(Note note)
    {
        _missCount++;

        // Star power protects combo from breaking!
        if (_starPowerActive)
        {
            // Still counts as a miss for stats, but combo is protected
            return;
        }

        if (_combo > 0)
        {
            ComboBreak?.Invoke();
        }

        _combo = 0;
        _multiplier = 1;
        _notesSinceMultiplierIncrease = 0;

        ComboChanged?.Invoke(_combo);
        MultiplierChanged?.Invoke(_multiplier);
    }

    public bool ActivateStarPower()
    {
        if (_starPowerMeter >= 0.5 && !_starPowerActive)
        {
            _starPowerActive = true;
            StarPowerChanged?.Invoke(true);
            return true;
        }
        return false;
    }

    public void UpdateStarPower(double deltaMs)
    {
        if (_starPowerActive)
        {
            _starPowerMeter -= deltaMs / STAR_POWER_DURATION;
            if (_starPowerMeter <= 0)
            {
                _starPowerMeter = 0;
                _starPowerActive = false;
                StarPowerChanged?.Invoke(false);
            }
        }
    }

    public double GetAccuracyPercentage()
    {
        int totalNotes = _perfectCount + _greatCount + _goodCount + _missCount;
        if (totalNotes == 0) return 100.0;

        double weightedScore =
            (_perfectCount * 1.0) +
            (_greatCount * 0.75) +
            (_goodCount * 0.5);

        return (weightedScore / totalNotes) * 100.0;
    }

    public int CalculateStarRating(int totalChartNotes)
    {
        if (totalChartNotes == 0) return 0;

        // Max score = all notes perfect at max multiplier
        int maxPossibleScore = totalChartNotes * SCORE_PERFECT * MAX_MULTIPLIER;
        double scorePercentage = (double)_score / maxPossibleScore;

        double accuracy = GetAccuracyPercentage();

        // Combined rating (60% score, 40% accuracy)
        double rating = (scorePercentage * 0.6) + (accuracy / 100.0 * 0.4);

        return rating switch
        {
            >= 0.95 => 5,
            >= 0.85 => 4,
            >= 0.70 => 3,
            >= 0.50 => 2,
            >= 0.30 => 1,
            _ => 0
        };
    }

    public PlayerStats GetFinalStats(int totalChartNotes) => new()
    {
        Score = _score,
        MaxCombo = _maxCombo,
        PerfectCount = _perfectCount,
        GreatCount = _greatCount,
        GoodCount = _goodCount,
        MissCount = _missCount,
        TotalNotes = totalChartNotes,
        Accuracy = GetAccuracyPercentage(),
        StarRating = CalculateStarRating(totalChartNotes)
    };

    public static HitJudgment GetJudgment(double absoluteOffset)
    {
        if (absoluteOffset <= WINDOW_PERFECT) return HitJudgment.Perfect;
        if (absoluteOffset <= WINDOW_GREAT) return HitJudgment.Great;
        if (absoluteOffset <= WINDOW_GOOD) return HitJudgment.Good;
        return HitJudgment.Miss;
    }
}
