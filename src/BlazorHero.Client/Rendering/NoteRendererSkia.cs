using BlazorHero.Client.Models;

namespace BlazorHero.Client.Rendering;

/// <summary>
/// Renders notes using IGameRenderContext (Skia-backed).
/// </summary>
public class NoteRendererSkia
{
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

    public void RenderNote(IGameRenderContext ctx, Note note, double normalizedZ)
    {
        var perspective = ctx.Perspective;

        // For hold notes, check if ANY part is visible (head or tail)
        if (note.IsHoldNote)
        {
            double durationZ = note.Duration / NoteTravelTime;
            double tailZ = normalizedZ + durationZ;

            bool headOffScreen = normalizedZ > 1.1 || normalizedZ < -0.5;
            bool tailOffScreen = tailZ > 1.1 || tailZ < -0.15;

            if (headOffScreen && tailOffScreen) return;
        }
        else
        {
            if (normalizedZ > 1.1 || normalizedZ < -0.15) return;
        }

        // Get lane center position
        double laneOffset = perspective.GetLaneOffset(note.LaneIndex);
        var (x, y, scale) = perspective.Project(laneOffset, normalizedZ);

        // Calculate note width based on lane width
        double laneWidth = perspective.GetLaneWidth(normalizedZ);
        double noteWidth = laneWidth * 0.85;
        double noteHeight = 20 * scale;

        if (note.IsHoldNote)
        {
            RenderHoldNote(ctx, note, normalizedZ, x, y, noteWidth, scale);
        }
        else
        {
            RenderTapNote(ctx, note, x, y, noteWidth, noteHeight, scale);
        }
    }

    private void RenderTapNote(IGameRenderContext ctx, Note note, double x, double y,
        double width, double height, double scale)
    {
        int laneIndex = note.LaneIndex;
        string color = NoteColors[laneIndex];
        string lightColor = NoteLightColors[laneIndex];
        string darkColor = NoteDarkColors[laneIndex];

        float cornerRadius = (float)Math.Max(4, 8 * scale);
        float left = (float)(x - width / 2);
        float top = (float)(y - height / 2);
        float w = (float)width;
        float h = (float)height;

        // Note shadow
        ctx.SetShadow("rgba(0, 0, 0, 0.5)", (float)(4 * scale), 0, (float)(2 * scale));

        // Draw rounded rectangle
        ctx.FillStyle = color;
        ctx.FillRoundedRect(left, top, w, h, cornerRadius);

        // Reset shadow
        ctx.ClearShadow();

        // Star power glow
        if (note.IsStarPower)
        {
            ctx.SetShadow("#60A5FA", (float)(15 * scale));
            ctx.FillStyle = color;
            ctx.FillRoundedRect(left, top, w, h, cornerRadius);
            ctx.ClearShadow();
        }

        // Top highlight
        ctx.FillStyle = $"{lightColor}60";
        ctx.FillRoundedRect(
            (float)(left + 2 * scale),
            (float)(top + 2 * scale),
            (float)(w - 4 * scale),
            (float)(h * 0.4),
            cornerRadius * 0.5f);

        // Bottom shadow
        ctx.FillStyle = $"{darkColor}40";
        ctx.FillRoundedRect(
            (float)(left + 2 * scale),
            (float)(top + h * 0.6),
            (float)(w - 4 * scale),
            (float)(h * 0.35),
            cornerRadius * 0.5f);

        // Border
        ctx.StrokeStyle = $"{lightColor}80";
        ctx.LineWidth = (float)Math.Max(1, 1.5 * scale);
        ctx.StrokeRoundedRect(left, top, w, h, cornerRadius);
    }

    private void RenderHoldNote(IGameRenderContext ctx, Note note, double noteZ,
        double ignoredX, double ignoredY, double ignoredWidth, double ignoredScale)
    {
        var perspective = ctx.Perspective;

        int laneIndex = note.LaneIndex;
        string color = NoteColors[laneIndex];
        string lightColor = NoteLightColors[laneIndex];
        bool isBeingHeld = note.IsHoldActive;

        double durationZ = note.Duration / NoteTravelTime;
        double frontZ = noteZ;

        double backZ;
        if (isBeingHeld)
        {
            backZ = (1 - note.HoldProgress) * durationZ;
        }
        else
        {
            backZ = noteZ + durationZ;
        }

        double renderFrontZ = Math.Min(1.0, frontZ);
        if (isBeingHeld)
        {
            renderFrontZ = Math.Max(0, renderFrontZ);
        }
        double renderBackZ = Math.Min(1.0, backZ);

        double laneOffset = perspective.GetLaneOffset(laneIndex);

        // Calculate FRONT position
        var (frontX, frontY, frontScale) = perspective.Project(laneOffset, renderFrontZ);
        double frontLaneWidth = perspective.GetLaneWidth(renderFrontZ);
        double frontWidth = frontLaneWidth * 0.85;

        // Calculate BACK position
        var (backX, backY, backScale) = perspective.Project(laneOffset, renderBackZ);
        double backLaneWidth = perspective.GetLaneWidth(renderBackZ);
        double backWidth = backLaneWidth * 0.85;

        // Hold body widths
        double frontBodyWidth = frontWidth * 0.35;
        double backBodyWidth = backWidth * 0.35;

        // If being held, add a glow effect
        if (isBeingHeld)
        {
            ctx.SetShadow(lightColor, 20);
        }

        // Draw the hold body
        ctx.BeginPath();
        ctx.MoveTo((float)(frontX - frontBodyWidth), (float)frontY);
        ctx.LineTo((float)(backX - backBodyWidth), (float)backY);
        ctx.LineTo((float)(backX + backBodyWidth), (float)backY);
        ctx.LineTo((float)(frontX + frontBodyWidth), (float)frontY);
        ctx.ClosePath();

        ctx.FillStyle = isBeingHeld ? $"{lightColor}CC" : $"{color}60";
        ctx.Fill();

        // Draw filled progress portion if being held
        if (isBeingHeld && note.HoldProgress > 0 && note.HoldProgress < 1)
        {
            double progressZ = renderFrontZ + (durationZ * note.HoldProgress);
            progressZ = Math.Min(renderBackZ, progressZ);

            var (progressX, progressY, _) = perspective.Project(laneOffset, progressZ);
            double progressLaneWidth = perspective.GetLaneWidth(progressZ);
            double progressBodyWidth = progressLaneWidth * 0.85 * 0.35;

            ctx.BeginPath();
            ctx.MoveTo((float)(frontX - frontBodyWidth), (float)frontY);
            ctx.LineTo((float)(progressX - progressBodyWidth), (float)progressY);
            ctx.LineTo((float)(progressX + progressBodyWidth), (float)progressY);
            ctx.LineTo((float)(frontX + frontBodyWidth), (float)frontY);
            ctx.ClosePath();

            ctx.FillStyle = lightColor;
            ctx.Fill();
        }

        // Body border
        ctx.StrokeStyle = isBeingHeld ? lightColor : color;
        ctx.LineWidth = (float)(isBeingHeld ? 3 * frontScale : 2 * frontScale);
        ctx.BeginPath();
        ctx.MoveTo((float)(frontX - frontBodyWidth), (float)frontY);
        ctx.LineTo((float)(backX - backBodyWidth), (float)backY);
        ctx.LineTo((float)(backX + backBodyWidth), (float)backY);
        ctx.LineTo((float)(frontX + frontBodyWidth), (float)frontY);
        ctx.ClosePath();
        ctx.Stroke();

        ctx.ClearShadow();

        // Draw FRONT note
        if (isBeingHeld)
        {
            ctx.SetShadow(lightColor, 15);
            ctx.BeginPath();
            ctx.Arc((float)frontX, (float)frontY, (float)(frontWidth * 0.3), 0, (float)(Math.PI * 2));
            ctx.FillStyle = lightColor;
            ctx.Fill();
            ctx.ClearShadow();
        }
        else
        {
            double frontNoteHeight = 28 * frontScale;
            double frontNoteWidth = frontWidth * 1.1;

            ctx.SetShadow(lightColor, 12);
            RenderTapNote(ctx, note, frontX, frontY, frontNoteWidth, frontNoteHeight, frontScale);
            ctx.ClearShadow();

            // Arrow indicator
            if (renderFrontZ > 0.05 && renderBackZ < 1.0)
            {
                ctx.FillStyle = $"{lightColor}80";
                ctx.BeginPath();
                double arrowY = frontY - frontNoteHeight / 2 - 8 * frontScale;
                ctx.MoveTo((float)frontX, (float)(arrowY - 10 * frontScale));
                ctx.LineTo((float)(frontX - 8 * frontScale), (float)arrowY);
                ctx.LineTo((float)(frontX + 8 * frontScale), (float)arrowY);
                ctx.ClosePath();
                ctx.Fill();
            }
        }

        // Draw BACK note
        if (renderBackZ <= 1.0 && backZ >= 0)
        {
            double backNoteHeight = 16 * backScale;
            double backNoteWidth = backWidth * 0.8;

            if (isBeingHeld)
            {
                ctx.SetShadow(lightColor, 10);
            }
            RenderTapNote(ctx, note, backX, backY, backNoteWidth, backNoteHeight, backScale);
            ctx.ClearShadow();
        }
    }

    public void RenderAllNotes(IGameRenderContext ctx, IEnumerable<Note> notes)
    {
        // Sort by Z so closer notes render on top
        var sortedNotes = notes
            .Where(n => n.IsActive)
            .OrderByDescending(n => n.CurrentZ);

        foreach (var note in sortedNotes)
        {
            RenderNote(ctx, note, note.CurrentZ);
        }
    }
}
