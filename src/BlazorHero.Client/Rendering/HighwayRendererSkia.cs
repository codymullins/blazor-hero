namespace BlazorHero.Client.Rendering;

/// <summary>
/// Renders the game highway using IGameRenderContext (Skia-backed).
/// </summary>
public class HighwayRendererSkia
{
    // Lane colors (5 lanes for Expert mode)
    private static readonly string[] LaneColors =
    {
        "#22C55E", // Green (D)
        "#EF4444", // Red (F)
        "#EAB308", // Yellow (J)
        "#3B82F6", // Blue (K)
        "#F97316"  // Orange (L)
    };

    private static readonly string[] LaneDarkColors =
    {
        "#166534",
        "#991B1B",
        "#A16207",
        "#1E40AF",
        "#C2410C"
    };

    public void Render(IGameRenderContext ctx, bool[] laneStates, int laneCount)
    {
        RenderHighwayBackground(ctx);
        RenderLaneDividers(ctx);
        RenderHitLine(ctx);
        RenderLaneButtons(ctx, laneStates, laneCount);
    }

    private void RenderHighwayBackground(IGameRenderContext ctx)
    {
        var perspective = ctx.Perspective;

        // Get highway corners
        var (leftHorizon, horizonY, _) = perspective.Project(-1, 1);
        var (rightHorizon, _, _) = perspective.Project(1, 1);
        var (leftHit, hitY, _) = perspective.Project(-1, 0);
        var (rightHit, _, _) = perspective.Project(1, 0);

        // Draw highway trapezoid
        ctx.BeginPath();
        ctx.MoveTo((float)leftHorizon, (float)horizonY);
        ctx.LineTo((float)rightHorizon, (float)horizonY);
        ctx.LineTo((float)rightHit, (float)hitY);
        ctx.LineTo((float)leftHit, (float)hitY);
        ctx.ClosePath();

        // Dark gradient background
        ctx.FillStyle = "rgba(15, 15, 25, 0.95)";
        ctx.Fill();

        // Subtle edge glow
        ctx.StrokeStyle = "rgba(100, 100, 180, 0.3)";
        ctx.LineWidth = 2;
        ctx.Stroke();

        // Always draw all 5 lanes
        for (int i = 0; i < 5; i++)
        {
            RenderLaneBackground(ctx, i);
        }
    }

    private void RenderLaneBackground(IGameRenderContext ctx, int laneIndex)
    {
        var perspective = ctx.Perspective;

        // Fixed 5-lane layout (each lane = 0.4 in normalized space)
        double laneWidth = 0.4;
        double leftOffset = -1.0 + laneIndex * laneWidth;
        double rightOffset = leftOffset + laneWidth;

        var (leftTop, topY, _) = perspective.Project(leftOffset, 1);
        var (rightTop, _, _) = perspective.Project(rightOffset, 1);
        var (leftBottom, bottomY, _) = perspective.Project(leftOffset, 0);
        var (rightBottom, _, _) = perspective.Project(rightOffset, 0);

        ctx.BeginPath();
        ctx.MoveTo((float)leftTop, (float)topY);
        ctx.LineTo((float)rightTop, (float)topY);
        ctx.LineTo((float)rightBottom, (float)bottomY);
        ctx.LineTo((float)leftBottom, (float)bottomY);
        ctx.ClosePath();

        // Very subtle lane color tint
        string color = LaneColors[laneIndex];
        ctx.FillStyle = $"{color}08";
        ctx.Fill();
    }

    private void RenderLaneDividers(IGameRenderContext ctx)
    {
        var perspective = ctx.Perspective;

        ctx.StrokeStyle = "rgba(255, 255, 255, 0.2)";
        ctx.LineWidth = 2;

        // Always draw 4 dividers between 5 lanes (fixed layout)
        double laneWidth = 0.4;
        for (int i = 1; i < 5; i++)
        {
            double laneX = -1.0 + i * laneWidth;

            var (topX, topY, _) = perspective.Project(laneX, 1);
            var (bottomX, bottomY, _) = perspective.Project(laneX, 0);

            ctx.BeginPath();
            ctx.MoveTo((float)topX, (float)topY);
            ctx.LineTo((float)bottomX, (float)bottomY);
            ctx.Stroke();
        }

        // Draw outer edges
        var (leftTop, ltY, _) = perspective.Project(-1, 1);
        var (leftBottom, lbY, _) = perspective.Project(-1, 0);
        var (rightTop, rtY, _) = perspective.Project(1, 1);
        var (rightBottom, rbY, _) = perspective.Project(1, 0);

        ctx.StrokeStyle = "rgba(255, 255, 255, 0.35)";
        ctx.LineWidth = 3;

        ctx.BeginPath();
        ctx.MoveTo((float)leftTop, (float)ltY);
        ctx.LineTo((float)leftBottom, (float)lbY);
        ctx.Stroke();

        ctx.BeginPath();
        ctx.MoveTo((float)rightTop, (float)rtY);
        ctx.LineTo((float)rightBottom, (float)rbY);
        ctx.Stroke();
    }

    private void RenderHitLine(IGameRenderContext ctx)
    {
        var perspective = ctx.Perspective;

        var (left, y, _) = perspective.Project(-1, 0);
        var (right, _, _) = perspective.Project(1, 0);

        // Glowing hit line
        ctx.SetShadow("rgba(255, 255, 255, 0.9)", 20);

        ctx.StrokeStyle = "rgba(255, 255, 255, 1.0)";
        ctx.LineWidth = 4;

        ctx.BeginPath();
        ctx.MoveTo((float)left, (float)y);
        ctx.LineTo((float)right, (float)y);
        ctx.Stroke();

        // Reset shadow
        ctx.ClearShadow();
    }

    private void RenderLaneButtons(IGameRenderContext ctx, bool[] laneStates, int laneCount)
    {
        for (int i = 0; i < laneCount; i++)
        {
            bool isPressed = i < laneStates.Length && laneStates[i];
            RenderLaneButton(ctx, i, isPressed);
        }
    }

    private void RenderLaneButton(IGameRenderContext ctx, int laneIndex, bool isPressed)
    {
        var perspective = ctx.Perspective;

        double laneOffset = perspective.GetLaneOffset(laneIndex);
        var (x, y, _) = perspective.Project(laneOffset, 0);
        float radius = 32f;
        float buttonY = (float)y + 12;

        // Button base
        ctx.BeginPath();
        ctx.Arc((float)x, buttonY, radius, 0, (float)(Math.PI * 2));

        if (isPressed)
        {
            // Pressed state - bright and glowing
            ctx.FillStyle = LaneColors[laneIndex];
            ctx.SetShadow(LaneColors[laneIndex], 25);
        }
        else
        {
            // Unpressed state - darker
            ctx.FillStyle = LaneDarkColors[laneIndex];
            ctx.ClearShadow();
        }

        ctx.Fill();

        // Button border
        ctx.ClearShadow();
        ctx.StrokeStyle = "rgba(255, 255, 255, 0.5)";
        ctx.LineWidth = 2;
        ctx.Stroke();

        // Key label
        string[] keyLabels = { "D", "F", "J", "K", "L" };
        ctx.FillStyle = "rgba(255, 255, 255, 0.9)";
        ctx.Font = "bold 16px sans-serif";

        // Center the text manually (approximate)
        float textX = (float)x - 5;
        float textY = buttonY + 5;
        ctx.FillText(keyLabels[laneIndex], textX, textY);
    }
}
