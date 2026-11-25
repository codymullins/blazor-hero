using Blazor.Extensions.Canvas.Canvas2D;
using BlazorHero.Client.Models;

namespace BlazorHero.Client.Rendering;

public class HighwayRenderer
{
    private readonly PerspectiveCamera _camera;

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

    public HighwayRenderer(PerspectiveCamera camera)
    {
        _camera = camera;
    }

    public async Task RenderAsync(Canvas2DContext ctx, bool[] laneStates)
    {
        await RenderHighwayBackground(ctx);
        await RenderLaneDividers(ctx);
        await RenderHitLine(ctx);
        await RenderLaneButtons(ctx, laneStates);
    }

    private async Task RenderHighwayBackground(Canvas2DContext ctx)
    {
        // Get highway corners
        var (leftHorizon, horizonY, _) = _camera.Project(-1, 1);
        var (rightHorizon, _, _) = _camera.Project(1, 1);
        var (leftHit, hitY, _) = _camera.Project(-1, 0);
        var (rightHit, _, _) = _camera.Project(1, 0);

        // Draw highway trapezoid
        await ctx.BeginPathAsync();
        await ctx.MoveToAsync(leftHorizon, horizonY);
        await ctx.LineToAsync(rightHorizon, horizonY);
        await ctx.LineToAsync(rightHit, hitY);
        await ctx.LineToAsync(leftHit, hitY);
        await ctx.ClosePathAsync();

        // Dark gradient background
        await ctx.SetFillStyleAsync("rgba(15, 15, 25, 0.95)");
        await ctx.FillAsync();

        // Subtle edge glow
        await ctx.SetStrokeStyleAsync("rgba(100, 100, 180, 0.3)");
        await ctx.SetLineWidthAsync(2);
        await ctx.StrokeAsync();

        // Always draw all 5 lanes
        for (int i = 0; i < 5; i++)
        {
            await RenderLaneBackground(ctx, i);
        }
    }

    private async Task RenderLaneBackground(Canvas2DContext ctx, int laneIndex)
    {
        // Fixed 5-lane layout (each lane = 0.4 in normalized space)
        double laneWidth = 0.4;
        double leftOffset = -1.0 + laneIndex * laneWidth;
        double rightOffset = leftOffset + laneWidth;

        var (leftTop, topY, _) = _camera.Project(leftOffset, 1);
        var (rightTop, _, _) = _camera.Project(rightOffset, 1);
        var (leftBottom, bottomY, _) = _camera.Project(leftOffset, 0);
        var (rightBottom, _, _) = _camera.Project(rightOffset, 0);

        await ctx.BeginPathAsync();
        await ctx.MoveToAsync(leftTop, topY);
        await ctx.LineToAsync(rightTop, topY);
        await ctx.LineToAsync(rightBottom, bottomY);
        await ctx.LineToAsync(leftBottom, bottomY);
        await ctx.ClosePathAsync();

        // Very subtle lane color tint
        string color = LaneColors[laneIndex];
        await ctx.SetFillStyleAsync($"{color}08");
        await ctx.FillAsync();
    }

    private async Task RenderLaneDividers(Canvas2DContext ctx)
    {
        await ctx.SetStrokeStyleAsync("rgba(255, 255, 255, 0.2)");
        await ctx.SetLineWidthAsync(2);  // Thicker dividers

        // Always draw 4 dividers between 5 lanes (fixed layout)
        double laneWidth = 0.4;  // 2.0 / 5 lanes
        for (int i = 1; i < 5; i++)
        {
            double laneX = -1.0 + i * laneWidth;

            var (topX, topY, _) = _camera.Project(laneX, 1);
            var (bottomX, bottomY, _) = _camera.Project(laneX, 0);

            await ctx.BeginPathAsync();
            await ctx.MoveToAsync(topX, topY);
            await ctx.LineToAsync(bottomX, bottomY);
            await ctx.StrokeAsync();
        }

        // Draw outer edges
        var (leftTop, ltY, _) = _camera.Project(-1, 1);
        var (leftBottom, lbY, _) = _camera.Project(-1, 0);
        var (rightTop, rtY, _) = _camera.Project(1, 1);
        var (rightBottom, rbY, _) = _camera.Project(1, 0);

        await ctx.SetStrokeStyleAsync("rgba(255, 255, 255, 0.35)");
        await ctx.SetLineWidthAsync(3);  // Thicker outer edges

        await ctx.BeginPathAsync();
        await ctx.MoveToAsync(leftTop, ltY);
        await ctx.LineToAsync(leftBottom, lbY);
        await ctx.StrokeAsync();

        await ctx.BeginPathAsync();
        await ctx.MoveToAsync(rightTop, rtY);
        await ctx.LineToAsync(rightBottom, rbY);
        await ctx.StrokeAsync();
    }

    private async Task RenderHitLine(Canvas2DContext ctx)
    {
        var (left, y, _) = _camera.Project(-1, 0);
        var (right, _, _) = _camera.Project(1, 0);

        // Glowing hit line
        await ctx.SetShadowColorAsync("rgba(255, 255, 255, 0.9)");
        await ctx.SetShadowBlurAsync(20);

        await ctx.SetStrokeStyleAsync("rgba(255, 255, 255, 1.0)");
        await ctx.SetLineWidthAsync(4);  // Thicker hit line

        await ctx.BeginPathAsync();
        await ctx.MoveToAsync(left, y);
        await ctx.LineToAsync(right, y);
        await ctx.StrokeAsync();

        // Reset shadow
        await ctx.SetShadowBlurAsync(0);
    }

    private async Task RenderLaneButtons(Canvas2DContext ctx, bool[] laneStates)
    {
        for (int i = 0; i < _camera.LaneCount; i++)
        {
            bool isPressed = i < laneStates.Length && laneStates[i];
            await RenderLaneButton(ctx, i, isPressed);
        }
    }

    private async Task RenderLaneButton(Canvas2DContext ctx, int laneIndex, bool isPressed)
    {
        double laneOffset = _camera.GetLaneOffset(laneIndex);
        var (x, y, _) = _camera.Project(laneOffset, 0);
        // Fixed button size (fits 5-lane layout)
        double radius = 32;

        // Button base
        await ctx.BeginPathAsync();
        await ctx.ArcAsync(x, y + 12, radius, 0, Math.PI * 2);

        if (isPressed)
        {
            // Pressed state - bright and glowing
            await ctx.SetFillStyleAsync(LaneColors[laneIndex]);
            await ctx.SetShadowColorAsync(LaneColors[laneIndex]);
            await ctx.SetShadowBlurAsync(25);
        }
        else
        {
            // Unpressed state - darker
            await ctx.SetFillStyleAsync(LaneDarkColors[laneIndex]);
            await ctx.SetShadowBlurAsync(0);
        }

        await ctx.FillAsync();

        // Button border
        await ctx.SetStrokeStyleAsync("rgba(255, 255, 255, 0.5)");
        await ctx.SetLineWidthAsync(2);
        await ctx.StrokeAsync();

        // Reset shadow
        await ctx.SetShadowBlurAsync(0);

        // Key label
        string[] keyLabels = { "D", "F", "J", "K", "L" };
        await ctx.SetFillStyleAsync("rgba(255, 255, 255, 0.9)");
        await ctx.SetFontAsync("bold 16px 'Segoe UI', sans-serif");
        await ctx.SetTextAlignAsync(TextAlign.Center);
        await ctx.SetTextBaselineAsync(TextBaseline.Middle);
        await ctx.FillTextAsync(keyLabels[laneIndex], x, y + 12);
    }
}
