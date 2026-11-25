namespace BlazorHero.Client.Rendering;

/// <summary>
/// Handles 3D perspective projection for the highway.
/// Notes spawn at the horizon (far away) and move toward the hit line at the bottom.
/// </summary>
public class PerspectiveCamera
{
    // Highway dimensions in world space
    public double HighwayLength { get; } = 1000;
    public double HighwayWidth { get; } = 580;  // Wider highway for better visibility

    // Canvas dimensions
    public double CanvasWidth { get; private set; } = 800;
    public double CanvasHeight { get; private set; } = 600;

    // Perspective parameters
    public double HorizonY { get; private set; } = 100;
    public double HitLineY { get; private set; } = 550;
    public double VanishingPointX { get; private set; }
    public double FocalLength { get; set; } = 480;  // Controls perspective intensity

    // Highway narrowing at horizon
    public double HorizonWidthRatio { get; } = 0.25;

    // Lane count (can be changed per difficulty)
    public int LaneCount { get; set; } = 4;

    public void Initialize(double canvasWidth, double canvasHeight)
    {
        CanvasWidth = canvasWidth;
        CanvasHeight = canvasHeight;
        VanishingPointX = canvasWidth / 2;
        HitLineY = canvasHeight - 60;
        HorizonY = canvasHeight * 0.12;
    }

    /// <summary>
    /// Projects a 3D position to 2D screen coordinates.
    /// Uses linear interpolation for X so notes travel in straight lines matching lane dividers.
    /// </summary>
    /// <param name="laneX">Normalized lane position (-1 to 1, where 0 is center)</param>
    /// <param name="normalizedZ">Depth (0 = hit line, 1 = horizon)</param>
    /// <returns>Screen X, Y, and scale factor</returns>
    public (double x, double y, double scale) Project(double laneX, double normalizedZ)
    {
        normalizedZ = Math.Clamp(normalizedZ, 0, 1);

        // Apply subtle perspective curve - notes gradually accelerate as they approach
        // (exponent closer to 1.0 = more subtle, closer to 0.5 = more dramatic)
        double perspectiveZ = Math.Pow(normalizedZ, 0.9);

        // Y position with perspective feel
        double y = HitLineY - (HitLineY - HorizonY) * perspectiveZ;

        // Perspective scale for object sizing (objects shrink toward horizon)
        double perspectiveScale = FocalLength / (FocalLength + normalizedZ * HighwayLength);

        // X position uses LINEAR interpolation so notes travel in straight lines
        // This ensures notes stay aligned with lane dividers throughout their journey
        double horizonScale = FocalLength / (FocalLength + HighwayLength);
        double linearScale = 1.0 - (1.0 - horizonScale) * normalizedZ;
        double halfWidth = (HighwayWidth / 2) * linearScale;
        double x = VanishingPointX + (laneX * halfWidth);

        return (x, y, perspectiveScale);
    }

    /// <summary>
    /// Gets the screen X position for a specific lane at a given depth.
    /// </summary>
    public double GetLaneX(int laneIndex, double normalizedZ)
    {
        double laneOffset = GetLaneOffset(laneIndex);
        var (x, _, _) = Project(laneOffset, normalizedZ);
        return x;
    }

    /// <summary>
    /// Gets the normalized X offset for a lane center (-1 to 1 range).
    /// Always uses fixed 5-lane layout regardless of difficulty.
    /// </summary>
    public double GetLaneOffset(int laneIndex)
    {
        // Fixed 5-lane layout: positions at -0.8, -0.4, 0, 0.4, 0.8
        return (laneIndex - 2) / 2.5;
    }

    /// <summary>
    /// Gets the width of a single lane at a given depth in screen pixels.
    /// Always uses fixed 5-lane layout.
    /// </summary>
    public double GetLaneWidth(double normalizedZ)
    {
        // Use the same linear scale as Project() for consistency
        double horizonScale = FocalLength / (FocalLength + HighwayLength);
        double linearScale = 1.0 - (1.0 - horizonScale) * normalizedZ;
        
        // Fixed 5-lane width (each lane = 2/5 = 0.4 in normalized space)
        double laneNormalizedWidth = 0.4;
        return (HighwayWidth / 2) * laneNormalizedWidth * linearScale;
    }

    /// <summary>
    /// Gets the width of a note at a given depth.
    /// </summary>
    public double GetNoteWidth(double normalizedZ, double baseWidth = 70)
    {
        var (_, _, scale) = Project(0, normalizedZ);
        return baseWidth * scale;
    }

    /// <summary>
    /// Gets the height of a note at a given depth.
    /// </summary>
    public double GetNoteHeight(double normalizedZ, double baseHeight = 22)
    {
        var (_, _, scale) = Project(0, normalizedZ);
        return baseHeight * scale;
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
    /// Converts a time offset (in ms) to a normalized Z position.
    /// </summary>
    /// <param name="timeUntilHit">Time in ms until the note reaches the hit line</param>
    /// <param name="noteTravelTime">Total time for a note to travel from horizon to hit line</param>
    public double TimeToNormalizedZ(double timeUntilHit, double noteTravelTime = 2000)
    {
        return Math.Clamp(timeUntilHit / noteTravelTime, 0, 1);
    }
}
