namespace BlazorHero.Client.Rendering;

/// <summary>
/// Handles 3D perspective projection for the highway.
/// Extracted from PerspectiveCamera for use with Tilecraft's rendering system.
/// </summary>
public class PerspectiveProjection
{
    // Highway dimensions in world space
    public double HighwayLength { get; set; } = 1000;
    public double HighwayWidth { get; set; } = 580;

    // Canvas dimensions
    public double CanvasWidth { get; private set; } = 800;
    public double CanvasHeight { get; private set; } = 600;

    // Perspective parameters
    public double HorizonY { get; private set; } = 100;
    public double HitLineY { get; private set; } = 550;
    public double VanishingPointX { get; private set; }
    public double FocalLength { get; set; } = 480;

    // Highway narrowing at horizon
    public double HorizonWidthRatio { get; } = 0.25;

    // Lane count (can be changed per difficulty)
    public int LaneCount { get; set; } = 4;

    public void Initialize(double canvasWidth, double canvasHeight)
    {
        CanvasWidth = canvasWidth;
        CanvasHeight = canvasHeight;
        VanishingPointX = canvasWidth / 2;

        bool isPortrait = canvasHeight > canvasWidth;
        Console.WriteLine($"[PerspectiveProjection] Initialize({canvasWidth}, {canvasHeight}) isPortrait={isPortrait}");

        if (isPortrait)
        {
            // Portrait mode: fill most of the space but leave room for lane buttons
            // Buttons render 12px below hit line with 32px radius = need ~50px below hit line
            HighwayWidth = canvasWidth * 0.98;  // Full width
            HitLineY = canvasHeight - 55;       // Leave room for lane buttons below
            HorizonY = canvasHeight * 0.03;     // Start near top
            FocalLength = 800;                  // Minimal perspective narrowing
            Console.WriteLine($"[PerspectiveProjection] PORTRAIT: HighwayWidth={HighwayWidth}, HorizonY={HorizonY}, HitLineY={HitLineY}");
        }
        else
        {
            // Landscape mode: original settings
            HighwayWidth = 580;
            HitLineY = canvasHeight - 60;
            HorizonY = canvasHeight * 0.12;
            FocalLength = 480;
            Console.WriteLine($"[PerspectiveProjection] LANDSCAPE: HighwayWidth={HighwayWidth}, HorizonY={HorizonY}, HitLineY={HitLineY}");
        }
    }

    /// <summary>
    /// Projects a 3D position to 2D screen coordinates.
    /// </summary>
    /// <param name="laneX">Normalized lane position (-1 to 1, where 0 is center)</param>
    /// <param name="normalizedZ">Depth (0 = hit line, 1 = horizon)</param>
    /// <returns>Screen X, Y, and scale factor</returns>
    public (double x, double y, double scale) Project(double laneX, double normalizedZ)
    {
        normalizedZ = Math.Clamp(normalizedZ, 0, 1);

        // Apply subtle perspective curve
        double perspectiveZ = Math.Pow(normalizedZ, 0.9);

        // Y position with perspective feel
        double y = HitLineY - (HitLineY - HorizonY) * perspectiveZ;

        // Perspective scale for object sizing
        double perspectiveScale = FocalLength / (FocalLength + perspectiveZ * HighwayLength);

        // X position uses LINEAR interpolation so notes travel in straight lines
        double horizonScale = FocalLength / (FocalLength + HighwayLength);
        double linearScale = 1.0 - (1.0 - horizonScale) * normalizedZ;
        double halfWidth = (HighwayWidth / 2) * linearScale;
        double x = VanishingPointX + (laneX * halfWidth);

        return (x, y, perspectiveScale);
    }

    /// <summary>
    /// Gets the normalized X offset for a lane center (-1 to 1 range).
    /// Always uses fixed 5-lane layout regardless of difficulty.
    /// </summary>
    public double GetLaneOffset(int laneIndex)
    {
        return (laneIndex - 2) / 2.5;
    }

    /// <summary>
    /// Gets the width of a single lane at a given depth in screen pixels.
    /// </summary>
    public double GetLaneWidth(double normalizedZ)
    {
        double horizonScale = FocalLength / (FocalLength + HighwayLength);
        double linearScale = 1.0 - (1.0 - horizonScale) * normalizedZ;
        double laneNormalizedWidth = 0.4;
        return (HighwayWidth / 2) * laneNormalizedWidth * linearScale;
    }

    /// <summary>
    /// Gets the left and right X coordinates of the highway at a given depth.
    /// </summary>
    public (double left, double right) GetHighwayEdges(double normalizedZ)
    {
        var (leftX, _, _) = Project(-1, normalizedZ);
        var (rightX, _, _) = Project(1, normalizedZ);
        return (leftX, rightX);
    }

    /// <summary>
    /// Converts a time offset to a normalized Z position.
    /// </summary>
    public double TimeToNormalizedZ(double timeUntilHit, double noteTravelTime = 2000)
    {
        double t = Math.Clamp(timeUntilHit / noteTravelTime, 0, 1);
        return Math.Pow(t, 0.8);
    }
}
