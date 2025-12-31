namespace BlazorHero.Client.Rendering;

/// <summary>
/// Game-specific render context that provides both low-level drawing operations
/// and game-specific helpers like perspective projection and effects.
/// </summary>
public interface IGameRenderContext
{
    /// <summary>
    /// The perspective projection system for 3D-to-2D transforms.
    /// </summary>
    PerspectiveProjection Perspective { get; }

    /// <summary>
    /// Canvas width in pixels.
    /// </summary>
    double Width { get; }

    /// <summary>
    /// Canvas height in pixels.
    /// </summary>
    double Height { get; }

    #region Basic Drawing Operations

    void ClearRect(float x, float y, float width, float height);
    void FillRect(float x, float y, float width, float height);
    void StrokeRect(float x, float y, float width, float height);

    #endregion

    #region Path Operations

    void BeginPath();
    void MoveTo(float x, float y);
    void LineTo(float x, float y);
    void QuadraticCurveTo(float cpx, float cpy, float x, float y);
    void BezierCurveTo(float cp1x, float cp1y, float cp2x, float cp2y, float x, float y);
    void Arc(float x, float y, float radius, float startAngle, float endAngle, bool anticlockwise = false);
    void ArcTo(float x1, float y1, float x2, float y2, float radius);
    void ClosePath();
    void Fill();
    void Stroke();
    void Clip();

    #endregion

    #region Style Properties

    string? FillStyle { get; set; }
    string? StrokeStyle { get; set; }
    float LineWidth { get; set; }
    string? LineCap { get; set; }
    string? LineJoin { get; set; }
    float GlobalAlpha { get; set; }
    string? Font { get; set; }

    #endregion

    #region Transform Operations

    void Save();
    void Restore();
    void Translate(float x, float y);
    void Rotate(float angle);
    void Scale(float x, float y);

    #endregion

    #region Text Operations

    void FillText(string text, float x, float y);
    void StrokeText(string text, float x, float y);

    #endregion

    #region Game-Specific Extensions

    /// <summary>
    /// Draws a rounded rectangle.
    /// </summary>
    void DrawRoundedRect(float x, float y, float width, float height, float radius);

    /// <summary>
    /// Fills a rounded rectangle.
    /// </summary>
    void FillRoundedRect(float x, float y, float width, float height, float radius);

    /// <summary>
    /// Strokes a rounded rectangle.
    /// </summary>
    void StrokeRoundedRect(float x, float y, float width, float height, float radius);

    /// <summary>
    /// Sets a shadow/glow effect for subsequent drawing operations.
    /// </summary>
    void SetShadow(string color, float blur, float offsetX = 0, float offsetY = 0);

    /// <summary>
    /// Clears the shadow effect.
    /// </summary>
    void ClearShadow();

    /// <summary>
    /// Projects world coordinates through the perspective system and executes a draw callback.
    /// </summary>
    /// <param name="laneOffset">Normalized lane position (-1 to 1)</param>
    /// <param name="normalizedZ">Depth (0 = hit line, 1 = horizon)</param>
    /// <param name="draw">Callback receiving (screenX, screenY, scale)</param>
    void ProjectAndDraw(double laneOffset, double normalizedZ, Action<double, double, double> draw);

    /// <summary>
    /// Gets lane information at a given depth.
    /// </summary>
    (double x, double width) GetLaneAt(int laneIndex, double normalizedZ);

    #endregion
}
