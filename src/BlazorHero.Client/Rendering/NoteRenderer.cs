using Blazor.Extensions.Canvas.Canvas2D;
using BlazorHero.Client.Models;

namespace BlazorHero.Client.Rendering;

public class NoteRenderer
{
    private readonly PerspectiveCamera _camera;

    // Note travel time (set before rendering)
    public double NoteTravelTime { get; set; } = 2200;

    private static readonly string[] NoteColors =
    {
        "#22C55E", // Green
        "#EF4444", // Red
        "#EAB308", // Yellow
        "#3B82F6", // Blue
        "#F97316"  // Orange
    };

    private static readonly string[] NoteLightColors =
    {
        "#4ADE80",
        "#F87171",
        "#FACC15",
        "#60A5FA",
        "#FB923C"
    };

    private static readonly string[] NoteDarkColors =
    {
        "#166534",
        "#991B1B",
        "#A16207",
        "#1E40AF",
        "#C2410C"
    };

    public NoteRenderer(PerspectiveCamera camera)
    {
        _camera = camera;
    }

    public async Task RenderNoteAsync(Canvas2DContext ctx, Note note, double normalizedZ)
    {
        // For hold notes, check if ANY part is visible (head or tail)
        if (note.IsHoldNote)
        {
            double durationZ = note.Duration / NoteTravelTime;
            double tailZ = normalizedZ + durationZ;  // Tail is BEHIND head (higher Z)
            
            // Skip only if both head AND tail are off screen
            bool headOffScreen = normalizedZ > 1.1 || normalizedZ < -0.5;
            bool tailOffScreen = tailZ > 1.1 || tailZ < -0.15;
            
            if (headOffScreen && tailOffScreen) return;
        }
        else
        {
            // Regular tap note - skip if off screen
            if (normalizedZ > 1.1 || normalizedZ < -0.15) return;
        }

        // Get lane center position
        double laneOffset = _camera.GetLaneOffset(note.LaneIndex);
        var (x, y, scale) = _camera.Project(laneOffset, normalizedZ);

        // Calculate note width based on lane width (fill ~85% of lane)
        double laneWidth = _camera.GetLaneWidth(normalizedZ);
        double noteWidth = laneWidth * 0.85;
        double noteHeight = 20 * scale;

        if (note.IsHoldNote)
        {
            await RenderHoldNoteAsync(ctx, note, normalizedZ, x, y, noteWidth, scale);
        }
        else
        {
            await RenderTapNoteAsync(ctx, note, x, y, noteWidth, noteHeight, scale);
        }
    }

    private async Task RenderTapNoteAsync(Canvas2DContext ctx, Note note, double x, double y,
        double width, double height, double scale)
    {
        int laneIndex = note.LaneIndex;
        string color = NoteColors[laneIndex];
        string lightColor = NoteLightColors[laneIndex];
        string darkColor = NoteDarkColors[laneIndex];

        double cornerRadius = Math.Max(4, 8 * scale);
        double left = x - width / 2;
        double top = y - height / 2;

        // Note shadow
        await ctx.SetShadowColorAsync("rgba(0, 0, 0, 0.5)");
        await ctx.SetShadowBlurAsync((float)(4 * scale));
        await ctx.SetShadowOffsetYAsync((float)(2 * scale));

        // Draw rounded rectangle
        await ctx.BeginPathAsync();
        await DrawRoundedRect(ctx, left, top, width, height, cornerRadius);

        // Gradient fill for 3D effect
        await ctx.SetFillStyleAsync(color);
        await ctx.FillAsync();

        // Reset shadow
        await ctx.SetShadowBlurAsync(0f);
        await ctx.SetShadowOffsetYAsync(0f);

        // Star power glow
        if (note.IsStarPower)
        {
            await ctx.SetShadowColorAsync("#60A5FA");
            await ctx.SetShadowBlurAsync((float)(15 * scale));
            await ctx.BeginPathAsync();
            await DrawRoundedRect(ctx, left, top, width, height, cornerRadius);
            await ctx.FillAsync();
            await ctx.SetShadowBlurAsync(0f);
        }

        // Top highlight
        await ctx.BeginPathAsync();
        await DrawRoundedRect(ctx, left + 2 * scale, top + 2 * scale,
            width - 4 * scale, height * 0.4, cornerRadius * 0.5);
        await ctx.SetFillStyleAsync($"{lightColor}60");
        await ctx.FillAsync();

        // Bottom shadow
        await ctx.BeginPathAsync();
        await DrawRoundedRect(ctx, left + 2 * scale, top + height * 0.6,
            width - 4 * scale, height * 0.35, cornerRadius * 0.5);
        await ctx.SetFillStyleAsync($"{darkColor}40");
        await ctx.FillAsync();

        // Border
        await ctx.BeginPathAsync();
        await DrawRoundedRect(ctx, left, top, width, height, cornerRadius);
        await ctx.SetStrokeStyleAsync($"{lightColor}80");
        await ctx.SetLineWidthAsync((float)Math.Max(1, 1.5 * scale));
        await ctx.StrokeAsync();
    }

    private async Task RenderHoldNoteAsync(Canvas2DContext ctx, Note note, double noteZ,
        double ignoredX, double ignoredY, double ignoredWidth, double ignoredScale)
    {
        int laneIndex = note.LaneIndex;
        string color = NoteColors[laneIndex];
        string lightColor = NoteLightColors[laneIndex];
        bool isBeingHeld = note.IsHoldActive;

        // noteZ is based on note.Time - this is when you PRESS (the front/start)
        // The back/end is Duration later in time, which means it appears from horizon first (higher Z)
        double durationZ = note.Duration / NoteTravelTime;
        
        // FRONT (press point) = where you press, reaches hit line first = noteZ
        double frontZ = noteZ;

        // BACK (release point) = end of hold
        // When holding, use HoldProgress to make tail move toward hit line
        // (noteZ is clamped to 0+ by TimeToNormalizedZ, so we can't rely on it going negative)
        double backZ;
        if (isBeingHeld)
        {
            // Tail moves from durationZ toward 0 as HoldProgress goes from 0 to 1
            backZ = (1 - note.HoldProgress) * durationZ;
        }
        else
        {
            backZ = noteZ + durationZ;
        }
        
        // Clamp for rendering
        double renderFrontZ = Math.Min(1.0, frontZ);
        // When being held, keep the head fixed at the hit line (Z=0)
        // while the tail continues moving toward it (bar "shrinks")
        if (isBeingHeld)
        {
            renderFrontZ = Math.Max(0, renderFrontZ);
        }
        double renderBackZ = Math.Min(1.0, backZ);
        
        double laneOffset = _camera.GetLaneOffset(laneIndex);
        
        // Calculate FRONT position (where you press, closer to/at hit line)
        var (frontX, frontY, frontScale) = _camera.Project(laneOffset, renderFrontZ);
        double frontLaneWidth = _camera.GetLaneWidth(renderFrontZ);
        double frontWidth = frontLaneWidth * 0.85;
        
        // Calculate BACK position (end of hold, further from hit line)
        var (backX, backY, backScale) = _camera.Project(laneOffset, renderBackZ);
        double backLaneWidth = _camera.GetLaneWidth(renderBackZ);
        double backWidth = backLaneWidth * 0.85;

        // Hold body widths
        double frontBodyWidth = frontWidth * 0.35;
        double backBodyWidth = backWidth * 0.35;

        // If being held, add a glow effect
        if (isBeingHeld)
        {
            await ctx.SetShadowColorAsync(lightColor);
            await ctx.SetShadowBlurAsync(20);
        }

        // Draw the hold body (from front to back)
        // Front is at bottom (at/near hit line), back is at top (toward horizon)
        await ctx.BeginPathAsync();
        await ctx.MoveToAsync(frontX - frontBodyWidth, frontY);   // Front left
        await ctx.LineToAsync(backX - backBodyWidth, backY);       // Back left
        await ctx.LineToAsync(backX + backBodyWidth, backY);       // Back right
        await ctx.LineToAsync(frontX + frontBodyWidth, frontY);   // Front right
        await ctx.ClosePathAsync();

        // Fill color depends on hold state
        if (isBeingHeld)
        {
            // Bright fill when actively holding
            await ctx.SetFillStyleAsync($"{lightColor}CC");
        }
        else
        {
            // Semi-transparent when not held
            await ctx.SetFillStyleAsync($"{color}60");
        }
        await ctx.FillAsync();

        // Draw filled progress portion if being held
        if (isBeingHeld && note.HoldProgress > 0 && note.HoldProgress < 1)
        {
            // Progress goes from front toward back
            double progressZ = renderFrontZ + (durationZ * note.HoldProgress);
            progressZ = Math.Min(renderBackZ, progressZ);
            
            var (progressX, progressY, _) = _camera.Project(laneOffset, progressZ);
            double progressLaneWidth = _camera.GetLaneWidth(progressZ);
            double progressBodyWidth = progressLaneWidth * 0.85 * 0.35;
            
            // Draw filled portion (from front to progress point)
            await ctx.BeginPathAsync();
            await ctx.MoveToAsync(frontX - frontBodyWidth, frontY);
            await ctx.LineToAsync(progressX - progressBodyWidth, progressY);
            await ctx.LineToAsync(progressX + progressBodyWidth, progressY);
            await ctx.LineToAsync(frontX + frontBodyWidth, frontY);
            await ctx.ClosePathAsync();
            
            await ctx.SetFillStyleAsync(lightColor);
            await ctx.FillAsync();
        }

        // Body border
        await ctx.SetStrokeStyleAsync(isBeingHeld ? lightColor : color);
        await ctx.SetLineWidthAsync((float)(isBeingHeld ? 3 * frontScale : 2 * frontScale));
        await ctx.BeginPathAsync();
        await ctx.MoveToAsync(frontX - frontBodyWidth, frontY);
        await ctx.LineToAsync(backX - backBodyWidth, backY);
        await ctx.LineToAsync(backX + backBodyWidth, backY);
        await ctx.LineToAsync(frontX + frontBodyWidth, frontY);
        await ctx.ClosePathAsync();
        await ctx.StrokeAsync();

        // Reset shadow
        await ctx.SetShadowBlurAsync(0);

        // Draw FRONT note (the press point, at/near hit line)
        if (isBeingHeld)
        {
            // Held: draw a glowing ring at the hit line
            await ctx.SetShadowColorAsync(lightColor);
            await ctx.SetShadowBlurAsync(15);
            await ctx.BeginPathAsync();
            await ctx.ArcAsync(frontX, frontY, frontWidth * 0.3, 0, Math.PI * 2);
            await ctx.SetFillStyleAsync(lightColor);
            await ctx.FillAsync();
            await ctx.SetShadowBlurAsync(0);
        }
        else
        {
            // Not held: draw FRONT note with extra emphasis (this is where player presses!)
            double frontNoteHeight = 28 * frontScale;
            double frontNoteWidth = frontWidth * 1.1;
            
            await ctx.SetShadowColorAsync(lightColor);
            await ctx.SetShadowBlurAsync(12);
            await RenderTapNoteAsync(ctx, note, frontX, frontY, frontNoteWidth, frontNoteHeight, frontScale);
            await ctx.SetShadowBlurAsync(0);
            
            // Add arrow indicator pointing toward the back (showing hold direction)
            if (renderFrontZ > 0.05 && renderBackZ < 1.0)
            {
                await ctx.SetFillStyleAsync($"{lightColor}80");
                await ctx.BeginPathAsync();
                double arrowY = frontY - frontNoteHeight/2 - 8*frontScale;
                await ctx.MoveToAsync(frontX, arrowY - 10*frontScale);
                await ctx.LineToAsync(frontX - 8*frontScale, arrowY);
                await ctx.LineToAsync(frontX + 8*frontScale, arrowY);
                await ctx.ClosePathAsync();
                await ctx.FillAsync();
            }
        }

        // Draw BACK note (end of hold, smaller) - only if visible
        if (renderBackZ <= 1.0 && backZ >= 0)
        {
            double backNoteHeight = 16 * backScale;
            double backNoteWidth = backWidth * 0.8;
            
            if (isBeingHeld)
            {
                await ctx.SetShadowColorAsync(lightColor);
                await ctx.SetShadowBlurAsync(10);
            }
            await RenderTapNoteAsync(ctx, note, backX, backY, backNoteWidth, backNoteHeight, backScale);
            await ctx.SetShadowBlurAsync(0);
        }
    }

    private async Task DrawRoundedRect(Canvas2DContext ctx, double x, double y,
        double w, double h, double r)
    {
        r = Math.Min(r, Math.Min(w / 2, h / 2));

        await ctx.MoveToAsync(x + r, y);
        await ctx.LineToAsync(x + w - r, y);
        await ctx.QuadraticCurveToAsync(x + w, y, x + w, y + r);
        await ctx.LineToAsync(x + w, y + h - r);
        await ctx.QuadraticCurveToAsync(x + w, y + h, x + w - r, y + h);
        await ctx.LineToAsync(x + r, y + h);
        await ctx.QuadraticCurveToAsync(x, y + h, x, y + h - r);
        await ctx.LineToAsync(x, y + r);
        await ctx.QuadraticCurveToAsync(x, y, x + r, y);
    }

    public async Task RenderAllNotesAsync(Canvas2DContext ctx, IEnumerable<Note> notes)
    {
        // Sort by Z so closer notes render on top
        var sortedNotes = notes
            .Where(n => n.IsActive)
            .OrderByDescending(n => n.CurrentZ);

        foreach (var note in sortedNotes)
        {
            await RenderNoteAsync(ctx, note, note.CurrentZ);
        }
    }
}
