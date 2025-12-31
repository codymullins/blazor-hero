using BlazorHero.Client.Models;

namespace BlazorHero.Client.Rendering;

/// <summary>
/// Renders visual effects using IGameRenderContext (Skia-backed).
/// </summary>
public class EffectRendererSkia
{
    private readonly List<HitEffectData> _hitEffects = new();
    private readonly List<JudgmentPopupData> _judgmentPopups = new();
    private readonly List<ComboFlameData> _flames = new();
    private readonly Random _random = new();

    // Track which lanes are actively holding (5 for Expert mode)
    private readonly bool[] _activeHolds = new bool[5];
    private readonly double[] _holdStartTimes = new double[5];

    private static readonly string[] LaneColors = { "#22C55E", "#EF4444", "#EAB308", "#3B82F6", "#F97316" };
    private static readonly string[] LaneLightColors = { "#4ADE80", "#F87171", "#FACC15", "#60A5FA", "#FB923C" };

    public void SetHoldActive(int lane, bool active)
    {
        if (lane >= 0 && lane < 5)
        {
            _activeHolds[lane] = active;
            if (active)
            {
                _holdStartTimes[lane] = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
            }
        }
    }

    public void TriggerHitEffect(Lane lane, HitJudgment judgment)
    {
        _hitEffects.Add(new HitEffectData
        {
            Lane = lane,
            Judgment = judgment,
            StartTime = DateTime.UtcNow,
            Duration = TimeSpan.FromMilliseconds(400)
        });

        _judgmentPopups.Add(new JudgmentPopupData
        {
            Lane = lane,
            Judgment = judgment,
            StartTime = DateTime.UtcNow,
            Duration = TimeSpan.FromMilliseconds(600)
        });
    }

    public void UpdateComboFlames(int combo)
    {
        int targetFlameCount = Math.Min(combo / 8, 24);

        while (_flames.Count < targetFlameCount)
        {
            _flames.Add(new ComboFlameData
            {
                LaneIndex = _random.Next(5),
                OffsetX = (_random.NextDouble() - 0.5) * 40,
                Speed = 0.8 + _random.NextDouble() * 0.4,
                Phase = _random.NextDouble() * Math.PI * 2,
                Size = 0.7 + _random.NextDouble() * 0.6
            });
        }

        while (_flames.Count > targetFlameCount)
        {
            _flames.RemoveAt(_flames.Count - 1);
        }
    }

    public void ClearFlames()
    {
        _flames.Clear();
    }

    public void ClearAllHolds()
    {
        for (int i = 0; i < 5; i++)
        {
            _activeHolds[i] = false;
        }
    }

    public void Render(IGameRenderContext ctx, double deltaMs)
    {
        RenderActiveHolds(ctx, deltaMs);
        RenderHitEffects(ctx, deltaMs);
        RenderComboFlames(ctx, deltaMs);
        RenderJudgmentPopups(ctx, deltaMs);
    }

    private void RenderActiveHolds(IGameRenderContext ctx, double deltaMs)
    {
        var perspective = ctx.Perspective;
        double now = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;

        for (int lane = 0; lane < 5; lane++)
        {
            if (!_activeHolds[lane]) continue;

            double elapsed = now - _holdStartTimes[lane];
            double pulse = 0.7 + 0.3 * Math.Sin(elapsed * 0.01);

            double laneOffset = perspective.GetLaneOffset(lane);
            var (x, y, _) = perspective.Project(laneOffset, 0);

            string lightColor = LaneLightColors[lane];

            // Glowing ring at hit line
            ctx.SetShadow(lightColor, (float)(20 * pulse));

            // Outer pulsing ring
            ctx.BeginPath();
            ctx.Arc((float)x, (float)y, (float)(35 + 5 * pulse), 0, (float)(Math.PI * 2));
            ctx.StrokeStyle = $"{lightColor}AA";
            ctx.LineWidth = 4;
            ctx.Stroke();

            // Inner solid ring
            ctx.BeginPath();
            ctx.Arc((float)x, (float)y, 25, 0, (float)(Math.PI * 2));
            ctx.StrokeStyle = lightColor;
            ctx.LineWidth = 3;
            ctx.Stroke();

            // Center glow
            ctx.BeginPath();
            ctx.Arc((float)x, (float)y, (float)(15 * pulse), 0, (float)(Math.PI * 2));
            ctx.FillStyle = $"{lightColor}60";
            ctx.Fill();

            // Rising particles effect
            int particleCount = 6;
            for (int i = 0; i < particleCount; i++)
            {
                double angle = (Math.PI * 2 / particleCount) * i + elapsed * 0.003;
                double particleProgress = ((elapsed * 0.002 + i * 0.15) % 1.0);
                double particleY = y - particleProgress * 60;
                double particleX = x + Math.Cos(angle) * 15;
                double alpha = 1 - particleProgress;
                int alphaInt = (int)(alpha * 180);

                ctx.BeginPath();
                ctx.Arc((float)particleX, (float)particleY, (float)(3 * (1 - particleProgress * 0.5)), 0, (float)(Math.PI * 2));
                ctx.FillStyle = $"{lightColor}{alphaInt:X2}";
                ctx.Fill();
            }

            ctx.ClearShadow();
        }
    }

    private void RenderHitEffects(IGameRenderContext ctx, double deltaMs)
    {
        var now = DateTime.UtcNow;

        for (int i = _hitEffects.Count - 1; i >= 0; i--)
        {
            var effect = _hitEffects[i];
            var elapsed = now - effect.StartTime;

            if (elapsed > effect.Duration)
            {
                _hitEffects.RemoveAt(i);
                continue;
            }

            double progress = elapsed.TotalMilliseconds / effect.Duration.TotalMilliseconds;
            RenderHitSpark(ctx, effect, progress);
        }
    }

    private void RenderHitSpark(IGameRenderContext ctx, HitEffectData effect, double progress)
    {
        var perspective = ctx.Perspective;

        double laneOffset = perspective.GetLaneOffset((int)effect.Lane);
        var (x, y, _) = perspective.Project(laneOffset, 0);

        string color = effect.Judgment switch
        {
            HitJudgment.Perfect => "#FFD700",
            HitJudgment.Great => "#22C55E",
            HitJudgment.Good => "#3B82F6",
            _ => "#EF4444"
        };

        double alpha = 1 - progress;
        double easeOut = 1 - Math.Pow(1 - progress, 3);

        // Expanding ring
        double radius = 15 + (easeOut * 50);
        int alphaInt = (int)(alpha * 255);
        string alphaHex = alphaInt.ToString("X2");

        ctx.BeginPath();
        ctx.Arc((float)x, (float)y, (float)radius, 0, (float)(Math.PI * 2));
        ctx.StrokeStyle = $"{color}{alphaHex}";
        ctx.LineWidth = (float)(4 * (1 - progress * 0.7));
        ctx.Stroke();

        // Inner flash
        if (progress < 0.3)
        {
            double flashAlpha = (1 - progress / 0.3) * 0.6;
            int flashAlphaInt = (int)(flashAlpha * 255);
            ctx.BeginPath();
            ctx.Arc((float)x, (float)y, (float)(20 * (1 - progress)), 0, (float)(Math.PI * 2));
            ctx.FillStyle = $"{color}{flashAlphaInt:X2}";
            ctx.Fill();
        }

        // Particle burst
        int particleCount = 8;
        for (int i = 0; i < particleCount; i++)
        {
            double angle = (Math.PI * 2 / particleCount) * i + progress * 0.5;
            double distance = easeOut * 45;
            double px = x + Math.Cos(angle) * distance;
            double py = y + Math.Sin(angle) * distance * 0.6;
            double size = Math.Max(2, 5 * (1 - progress));

            ctx.BeginPath();
            ctx.Arc((float)px, (float)py, (float)size, 0, (float)(Math.PI * 2));
            ctx.FillStyle = $"{color}{alphaHex}";
            ctx.Fill();
        }
    }

    private void RenderJudgmentPopups(IGameRenderContext ctx, double deltaMs)
    {
        var now = DateTime.UtcNow;

        for (int i = _judgmentPopups.Count - 1; i >= 0; i--)
        {
            var popup = _judgmentPopups[i];
            var elapsed = now - popup.StartTime;

            if (elapsed > popup.Duration)
            {
                _judgmentPopups.RemoveAt(i);
                continue;
            }

            double progress = elapsed.TotalMilliseconds / popup.Duration.TotalMilliseconds;
            RenderJudgmentText(ctx, popup, progress);
        }
    }

    private void RenderJudgmentText(IGameRenderContext ctx, JudgmentPopupData popup, double progress)
    {
        var perspective = ctx.Perspective;

        double laneOffset = perspective.GetLaneOffset((int)popup.Lane);
        var (x, baseY, _) = perspective.Project(laneOffset, 0);

        // Float upward
        double floatDistance = 60 * progress;
        double y = baseY - 40 - floatDistance;

        // Scale animation
        double scaleProgress = progress < 0.15 ? progress / 0.15 : 1;
        double scale = 0.5 + scaleProgress * 0.5;

        // Fade out
        double alpha = progress > 0.6 ? 1 - (progress - 0.6) / 0.4 : 1;

        string text = popup.Judgment switch
        {
            HitJudgment.Perfect => "PERFECT",
            HitJudgment.Great => "GREAT",
            HitJudgment.Good => "GOOD",
            HitJudgment.Miss => "MISS",
            _ => ""
        };

        string color = popup.Judgment switch
        {
            HitJudgment.Perfect => "#FFD700",
            HitJudgment.Great => "#22C55E",
            HitJudgment.Good => "#3B82F6",
            _ => "#EF4444"
        };

        int fontSize = (int)(18 * scale);
        int alphaInt = (int)(alpha * 255);

        ctx.Font = $"bold {fontSize}px sans-serif";

        // Center text manually (approximate)
        float textX = (float)x - (text.Length * fontSize * 0.3f);

        // Shadow
        ctx.FillStyle = $"rgba(0, 0, 0, {alpha * 0.5})";
        ctx.FillText(text, textX + 2, (float)y + 2);

        // Text
        ctx.FillStyle = $"{color}{alphaInt:X2}";
        ctx.FillText(text, textX, (float)y);
    }

    private void RenderComboFlames(IGameRenderContext ctx, double deltaMs)
    {
        foreach (var flame in _flames)
        {
            flame.Phase += deltaMs * 0.008 * flame.Speed;
            RenderFlame(ctx, flame);
        }
    }

    private void RenderFlame(IGameRenderContext ctx, ComboFlameData flame)
    {
        var perspective = ctx.Perspective;

        double laneOffset = perspective.GetLaneOffset(flame.LaneIndex);
        var (baseX, baseY, _) = perspective.Project(laneOffset, 0);
        double x = baseX + flame.OffsetX;

        double baseSize = 12 * flame.Size + Math.Sin(flame.Phase) * 4;

        // Draw flame layers
        string[] colors = { "#F97316", "#FBBF24", "#FEF3C7" };

        for (int i = 0; i < 4; i++)
        {
            double layerY = baseY - 15 - (i * 10) - Math.Sin(flame.Phase + i * 0.6) * 6;
            double layerSize = baseSize * (1 - i * 0.2);
            double layerAlpha = (0.7 - i * 0.15) * (0.6 + Math.Sin(flame.Phase * 2) * 0.4);

            string color = colors[Math.Min(i, colors.Length - 1)];
            int alphaInt = (int)(layerAlpha * 255);

            ctx.BeginPath();
            ctx.Arc((float)x, (float)layerY, (float)layerSize, 0, (float)(Math.PI * 2));
            ctx.FillStyle = $"{color}{alphaInt:X2}";
            ctx.Fill();
        }
    }

    public void Clear()
    {
        _hitEffects.Clear();
        _judgmentPopups.Clear();
        _flames.Clear();
    }
}

// Data classes (renamed to avoid conflicts with existing types)
public class HitEffectData
{
    public Lane Lane { get; set; }
    public HitJudgment Judgment { get; set; }
    public DateTime StartTime { get; set; }
    public TimeSpan Duration { get; set; }
}

public class JudgmentPopupData
{
    public Lane Lane { get; set; }
    public HitJudgment Judgment { get; set; }
    public DateTime StartTime { get; set; }
    public TimeSpan Duration { get; set; }
}

public class ComboFlameData
{
    public int LaneIndex { get; set; }
    public double OffsetX { get; set; }
    public double Speed { get; set; }
    public double Phase { get; set; }
    public double Size { get; set; }
}
