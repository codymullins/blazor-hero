using Blazor.Extensions.Canvas.Canvas2D;
using BlazorHero.Client.Models;

namespace BlazorHero.Client.Rendering;

public class EffectRenderer
{
    private readonly PerspectiveCamera _camera;
    private readonly List<HitEffect> _hitEffects = new();
    private readonly List<JudgmentPopup> _judgmentPopups = new();
    private readonly List<ComboFlame> _flames = new();
    private readonly Random _random = new();
    
    // Track which lanes are actively holding (5 for Expert mode)
    private readonly bool[] _activeHolds = new bool[5];
    private readonly double[] _holdStartTimes = new double[5];

    public EffectRenderer(PerspectiveCamera camera)
    {
        _camera = camera;
    }
    
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
        _hitEffects.Add(new HitEffect
        {
            Lane = lane,
            Judgment = judgment,
            StartTime = DateTime.UtcNow,
            Duration = TimeSpan.FromMilliseconds(400)
        });

        _judgmentPopups.Add(new JudgmentPopup
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
            _flames.Add(new ComboFlame
            {
                LaneIndex = _random.Next(5),  // Fixed 5-lane layout
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

    public async Task RenderAsync(Canvas2DContext ctx, double deltaMs)
    {
        // Render active hold indicators
        await RenderActiveHolds(ctx, deltaMs);
        
        // Render hit effects
        await RenderHitEffects(ctx, deltaMs);

        // Render combo flames
        await RenderComboFlames(ctx, deltaMs);

        // Render judgment popups
        await RenderJudgmentPopups(ctx, deltaMs);
    }
    
    private static readonly string[] LaneColors = { "#22C55E", "#EF4444", "#EAB308", "#3B82F6", "#F97316" };
    private static readonly string[] LaneLightColors = { "#4ADE80", "#F87171", "#FACC15", "#60A5FA", "#FB923C" };
    
    private async Task RenderActiveHolds(Canvas2DContext ctx, double deltaMs)
    {
        double now = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
        
        // Check all 5 lanes for active holds
        for (int lane = 0; lane < 5; lane++)
        {
            if (!_activeHolds[lane]) continue;
            
            double elapsed = now - _holdStartTimes[lane];
            double pulse = 0.7 + 0.3 * Math.Sin(elapsed * 0.01); // Pulsing effect
            
            double laneOffset = _camera.GetLaneOffset(lane);
            var (x, y, _) = _camera.Project(laneOffset, 0);
            
            string color = LaneColors[lane];
            string lightColor = LaneLightColors[lane];
            
            // Glowing ring at hit line
            await ctx.SetShadowColorAsync(lightColor);
            await ctx.SetShadowBlurAsync((float)(20 * pulse));
            
            // Outer pulsing ring
            await ctx.BeginPathAsync();
            await ctx.ArcAsync(x, y, 35 + 5 * pulse, 0, Math.PI * 2);
            await ctx.SetStrokeStyleAsync($"{lightColor}AA");
            await ctx.SetLineWidthAsync(4);
            await ctx.StrokeAsync();
            
            // Inner solid ring
            await ctx.BeginPathAsync();
            await ctx.ArcAsync(x, y, 25, 0, Math.PI * 2);
            await ctx.SetStrokeStyleAsync(lightColor);
            await ctx.SetLineWidthAsync(3);
            await ctx.StrokeAsync();
            
            // Center glow
            await ctx.BeginPathAsync();
            await ctx.ArcAsync(x, y, 15 * pulse, 0, Math.PI * 2);
            await ctx.SetFillStyleAsync($"{lightColor}60");
            await ctx.FillAsync();
            
            // Rising particles effect
            int particleCount = 6;
            for (int i = 0; i < particleCount; i++)
            {
                double angle = (Math.PI * 2 / particleCount) * i + elapsed * 0.003;
                double particleProgress = ((elapsed * 0.002 + i * 0.15) % 1.0);
                double distance = 20 + particleProgress * 40;
                double particleY = y - particleProgress * 60; // Rise upward
                double particleX = x + Math.Cos(angle) * 15;
                double alpha = 1 - particleProgress;
                int alphaInt = (int)(alpha * 180);
                
                await ctx.BeginPathAsync();
                await ctx.ArcAsync(particleX, particleY, 3 * (1 - particleProgress * 0.5), 0, Math.PI * 2);
                await ctx.SetFillStyleAsync($"{lightColor}{alphaInt:X2}");
                await ctx.FillAsync();
            }
            
            await ctx.SetShadowBlurAsync(0);
        }
    }

    private async Task RenderHitEffects(Canvas2DContext ctx, double deltaMs)
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
            await RenderHitSpark(ctx, effect, progress);
        }
    }

    private async Task RenderHitSpark(Canvas2DContext ctx, HitEffect effect, double progress)
    {
        double laneOffset = _camera.GetLaneOffset((int)effect.Lane);
        var (x, y, _) = _camera.Project(laneOffset, 0);

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

        await ctx.BeginPathAsync();
        await ctx.ArcAsync(x, y, radius, 0, Math.PI * 2);
        await ctx.SetStrokeStyleAsync($"{color}{alphaHex}");
        await ctx.SetLineWidthAsync((float)(4 * (1 - progress * 0.7)));
        await ctx.StrokeAsync();

        // Inner flash
        if (progress < 0.3)
        {
            double flashAlpha = (1 - progress / 0.3) * 0.6;
            int flashAlphaInt = (int)(flashAlpha * 255);
            await ctx.BeginPathAsync();
            await ctx.ArcAsync(x, y, 20 * (1 - progress), 0, Math.PI * 2);
            await ctx.SetFillStyleAsync($"{color}{flashAlphaInt:X2}");
            await ctx.FillAsync();
        }

        // Particle burst
        int particleCount = 8;
        for (int i = 0; i < particleCount; i++)
        {
            double angle = (Math.PI * 2 / particleCount) * i + progress * 0.5;
            double distance = easeOut * 45;
            double px = x + Math.Cos(angle) * distance;
            double py = y + Math.Sin(angle) * distance * 0.6; // Flatten vertically
            double size = Math.Max(2, 5 * (1 - progress));

            await ctx.BeginPathAsync();
            await ctx.ArcAsync(px, py, size, 0, Math.PI * 2);
            await ctx.SetFillStyleAsync($"{color}{alphaHex}");
            await ctx.FillAsync();
        }
    }

    private async Task RenderJudgmentPopups(Canvas2DContext ctx, double deltaMs)
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
            await RenderJudgmentText(ctx, popup, progress);
        }
    }

    private async Task RenderJudgmentText(Canvas2DContext ctx, JudgmentPopup popup, double progress)
    {
        double laneOffset = _camera.GetLaneOffset((int)popup.Lane);
        var (x, baseY, _) = _camera.Project(laneOffset, 0);

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

        await ctx.SetFontAsync($"bold {fontSize}px 'Segoe UI', sans-serif");
        await ctx.SetTextAlignAsync(TextAlign.Center);
        await ctx.SetTextBaselineAsync(TextBaseline.Middle);

        // Shadow
        await ctx.SetFillStyleAsync($"rgba(0, 0, 0, {alpha * 0.5})");
        await ctx.FillTextAsync(text, x + 2, y + 2);

        // Text
        await ctx.SetFillStyleAsync($"{color}{alphaInt:X2}");
        await ctx.FillTextAsync(text, x, y);
    }

    private async Task RenderComboFlames(Canvas2DContext ctx, double deltaMs)
    {
        foreach (var flame in _flames)
        {
            flame.Phase += deltaMs * 0.008 * flame.Speed;
            await RenderFlame(ctx, flame);
        }
    }

    private async Task RenderFlame(Canvas2DContext ctx, ComboFlame flame)
    {
        double laneOffset = _camera.GetLaneOffset(flame.LaneIndex);
        var (baseX, baseY, _) = _camera.Project(laneOffset, 0);
        double x = baseX + flame.OffsetX;

        double baseSize = 12 * flame.Size + Math.Sin(flame.Phase) * 4;

        // Draw flame layers
        string[] colors = { "#F97316", "#FBBF24", "#FEF3C7" };
        double[] alphas = { 0.7, 0.5, 0.3 };

        for (int i = 0; i < 4; i++)
        {
            double layerY = baseY - 15 - (i * 10) - Math.Sin(flame.Phase + i * 0.6) * 6;
            double layerSize = baseSize * (1 - i * 0.2);
            double layerAlpha = (0.7 - i * 0.15) * (0.6 + Math.Sin(flame.Phase * 2) * 0.4);

            string color = colors[Math.Min(i, colors.Length - 1)];
            int alphaInt = (int)(layerAlpha * 255);

            await ctx.BeginPathAsync();
            await ctx.ArcAsync(x, layerY, layerSize, 0, Math.PI * 2);
            await ctx.SetFillStyleAsync($"{color}{alphaInt:X2}");
            await ctx.FillAsync();
        }
    }

    public void Clear()
    {
        _hitEffects.Clear();
        _judgmentPopups.Clear();
        _flames.Clear();
    }
}

public class HitEffect
{
    public Lane Lane { get; set; }
    public HitJudgment Judgment { get; set; }
    public DateTime StartTime { get; set; }
    public TimeSpan Duration { get; set; }
}

public class JudgmentPopup
{
    public Lane Lane { get; set; }
    public HitJudgment Judgment { get; set; }
    public DateTime StartTime { get; set; }
    public TimeSpan Duration { get; set; }
}

public class ComboFlame
{
    public int LaneIndex { get; set; }
    public double OffsetX { get; set; }
    public double Speed { get; set; }
    public double Phase { get; set; }
    public double Size { get; set; }
}
