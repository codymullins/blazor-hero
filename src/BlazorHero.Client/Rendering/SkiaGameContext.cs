using System.Globalization;
using System.Text.RegularExpressions;
using SkiaSharp;

namespace BlazorHero.Client.Rendering;

/// <summary>
/// SkiaSharp-backed implementation of IGameRenderContext for high-performance game rendering.
/// </summary>
public sealed class SkiaGameContext : IGameRenderContext, IDisposable
{
    private readonly SKCanvas _canvas;
    private readonly PerspectiveProjection _perspective;

    // Paint objects
    private readonly SKPaint _fillPaint;
    private readonly SKPaint _strokePaint;
    private readonly SKPaint _textPaint;

    // Shadow state
    private SKImageFilter? _shadowFilter;
    private bool _shadowEnabled;

    // Current path
    private SKPath? _path;

    // Canvas dimensions
    public double Width { get; }
    public double Height { get; }

    public PerspectiveProjection Perspective => _perspective;

    public SkiaGameContext(SKCanvas canvas, double width, double height, PerspectiveProjection? perspective = null)
    {
        _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
        Width = width;
        Height = height;

        _perspective = perspective ?? new PerspectiveProjection();
        if (perspective == null)
        {
            // Only initialize if no projection was provided
            Console.WriteLine($"[SkiaGameContext] No projection provided, initializing new one with {width}x{height}");
            _perspective.Initialize(width, height);
        }
        else
        {
            Console.WriteLine($"[SkiaGameContext] Using provided projection (HighwayWidth={perspective.HighwayWidth}, HorizonY={perspective.HorizonY}, HitLineY={perspective.HitLineY})");
        }

        _fillPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = SKColors.Black
        };

        _strokePaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1f,
            Color = SKColors.Black
        };

        _textPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = SKColors.Black
        };
    }

    #region Basic Drawing Operations

    public void ClearRect(float x, float y, float width, float height)
    {
        using var clearPaint = new SKPaint { BlendMode = SKBlendMode.Clear };
        _canvas.DrawRect(new SKRect(x, y, x + width, y + height), clearPaint);
    }

    public void FillRect(float x, float y, float width, float height)
    {
        ApplyShadowToPaint(_fillPaint);
        _canvas.DrawRect(new SKRect(x, y, x + width, y + height), _fillPaint);
    }

    public void StrokeRect(float x, float y, float width, float height)
    {
        ApplyShadowToPaint(_strokePaint);
        _canvas.DrawRect(new SKRect(x, y, x + width, y + height), _strokePaint);
    }

    #endregion

    #region Path Operations

    public void BeginPath()
    {
        _path?.Dispose();
        _path = new SKPath();
    }

    public void MoveTo(float x, float y)
    {
        EnsurePath();
        _path!.MoveTo(x, y);
    }

    public void LineTo(float x, float y)
    {
        EnsurePath();
        _path!.LineTo(x, y);
    }

    public void QuadraticCurveTo(float cpx, float cpy, float x, float y)
    {
        EnsurePath();
        _path!.QuadTo(cpx, cpy, x, y);
    }

    public void BezierCurveTo(float cp1x, float cp1y, float cp2x, float cp2y, float x, float y)
    {
        EnsurePath();
        _path!.CubicTo(cp1x, cp1y, cp2x, cp2y, x, y);
    }

    public void Arc(float x, float y, float radius, float startAngle, float endAngle, bool anticlockwise = false)
    {
        EnsurePath();
        float startDeg = RadiansToDegrees(startAngle);
        float endDeg = RadiansToDegrees(endAngle);
        float sweep = endDeg - startDeg;

        if (anticlockwise)
        {
            sweep = sweep > 0 ? sweep - 360f : 360f + sweep;
        }

        var rect = new SKRect(x - radius, y - radius, x + radius, y + radius);
        _path!.AddArc(rect, startDeg, sweep);
    }

    public void ArcTo(float x1, float y1, float x2, float y2, float radius)
    {
        EnsurePath();
        _path!.ArcTo(new SKPoint(x1, y1), new SKPoint(x2, y2), radius);
    }

    public void ClosePath()
    {
        _path?.Close();
    }

    public void Fill()
    {
        if (_path == null) return;
        ApplyShadowToPaint(_fillPaint);
        _canvas.DrawPath(_path, _fillPaint);
    }

    public void Stroke()
    {
        if (_path == null) return;
        ApplyShadowToPaint(_strokePaint);
        _canvas.DrawPath(_path, _strokePaint);
    }

    public void Clip()
    {
        if (_path == null) return;
        _canvas.ClipPath(_path, antialias: true);
    }

    private void EnsurePath()
    {
        _path ??= new SKPath();
    }

    #endregion

    #region Style Properties

    private string? _fillStyle;
    public string? FillStyle
    {
        get => _fillStyle;
        set
        {
            _fillStyle = value;
            if (TryParseColor(value, out var color))
            {
                _fillPaint.Color = color;
            }
        }
    }

    private string? _strokeStyle;
    public string? StrokeStyle
    {
        get => _strokeStyle;
        set
        {
            _strokeStyle = value;
            if (TryParseColor(value, out var color))
            {
                _strokePaint.Color = color;
            }
        }
    }

    public float LineWidth
    {
        get => _strokePaint.StrokeWidth;
        set => _strokePaint.StrokeWidth = value;
    }

    private string? _lineCap;
    public string? LineCap
    {
        get => _lineCap;
        set
        {
            _lineCap = value;
            _strokePaint.StrokeCap = value?.ToLowerInvariant() switch
            {
                "round" => SKStrokeCap.Round,
                "square" => SKStrokeCap.Square,
                _ => SKStrokeCap.Butt
            };
        }
    }

    private string? _lineJoin;
    public string? LineJoin
    {
        get => _lineJoin;
        set
        {
            _lineJoin = value;
            _strokePaint.StrokeJoin = value?.ToLowerInvariant() switch
            {
                "round" => SKStrokeJoin.Round,
                "bevel" => SKStrokeJoin.Bevel,
                _ => SKStrokeJoin.Miter
            };
        }
    }

    private float _globalAlpha = 1f;
    public float GlobalAlpha
    {
        get => _globalAlpha;
        set
        {
            _globalAlpha = Math.Clamp(value, 0f, 1f);
            // Re-apply colors with new alpha
            if (TryParseColor(_fillStyle, out var fc))
            {
                _fillPaint.Color = fc.WithAlpha((byte)(_globalAlpha * fc.Alpha));
            }
            if (TryParseColor(_strokeStyle, out var sc))
            {
                _strokePaint.Color = sc.WithAlpha((byte)(_globalAlpha * sc.Alpha));
            }
        }
    }

    private string? _font;
    private SKFont _skFont = new(SKTypeface.Default, 16f);
    public string? Font
    {
        get => _font;
        set
        {
            _font = value;
            float size = ParseFontSize(value);
            _skFont = new SKFont(SKTypeface.Default, size);
        }
    }

    #endregion

    #region Transform Operations

    public void Save() => _canvas.Save();
    public void Restore() => _canvas.Restore();
    public void Translate(float x, float y) => _canvas.Translate(x, y);
    public void Rotate(float angle) => _canvas.RotateDegrees(RadiansToDegrees(angle));
    public void Scale(float x, float y) => _canvas.Scale(x, y);

    #endregion

    #region Text Operations

    public void FillText(string text, float x, float y)
    {
        if (string.IsNullOrEmpty(text)) return;
        _textPaint.Style = SKPaintStyle.Fill;
        _textPaint.Color = _fillPaint.Color;
        ApplyShadowToPaint(_textPaint);
        _canvas.DrawText(text, x, y, _skFont, _textPaint);
    }

    public void StrokeText(string text, float x, float y)
    {
        if (string.IsNullOrEmpty(text)) return;
        _textPaint.Style = SKPaintStyle.Stroke;
        _textPaint.Color = _strokePaint.Color;
        _textPaint.StrokeWidth = _strokePaint.StrokeWidth;
        ApplyShadowToPaint(_textPaint);
        _canvas.DrawText(text, x, y, _skFont, _textPaint);
    }

    #endregion

    #region Game-Specific Extensions

    public void DrawRoundedRect(float x, float y, float width, float height, float radius)
    {
        BeginPath();
        AddRoundedRectToPath(x, y, width, height, radius);
    }

    public void FillRoundedRect(float x, float y, float width, float height, float radius)
    {
        ApplyShadowToPaint(_fillPaint);
        var rect = new SKRoundRect(new SKRect(x, y, x + width, y + height), radius);
        _canvas.DrawRoundRect(rect, _fillPaint);
    }

    public void StrokeRoundedRect(float x, float y, float width, float height, float radius)
    {
        ApplyShadowToPaint(_strokePaint);
        var rect = new SKRoundRect(new SKRect(x, y, x + width, y + height), radius);
        _canvas.DrawRoundRect(rect, _strokePaint);
    }

    public void SetShadow(string color, float blur, float offsetX = 0, float offsetY = 0)
    {
        _shadowFilter?.Dispose();

        if (TryParseColor(color, out var shadowColor))
        {
            _shadowFilter = SKImageFilter.CreateDropShadow(
                offsetX, offsetY,
                blur / 2, blur / 2, // Skia uses sigma, blur is roughly 2*sigma
                shadowColor);
            _shadowEnabled = true;
        }
    }

    public void ClearShadow()
    {
        _shadowFilter?.Dispose();
        _shadowFilter = null;
        _shadowEnabled = false;

        // Clear filters from paints
        _fillPaint.ImageFilter = null;
        _strokePaint.ImageFilter = null;
        _textPaint.ImageFilter = null;
    }

    private void ApplyShadowToPaint(SKPaint paint)
    {
        paint.ImageFilter = _shadowEnabled ? _shadowFilter : null;
    }

    public void ProjectAndDraw(double laneOffset, double normalizedZ, Action<double, double, double> draw)
    {
        var (x, y, scale) = _perspective.Project(laneOffset, normalizedZ);
        draw(x, y, scale);
    }

    public (double x, double width) GetLaneAt(int laneIndex, double normalizedZ)
    {
        double laneOffset = _perspective.GetLaneOffset(laneIndex);
        var (x, _, _) = _perspective.Project(laneOffset, normalizedZ);
        double width = _perspective.GetLaneWidth(normalizedZ);
        return (x, width);
    }

    private void AddRoundedRectToPath(float x, float y, float w, float h, float r)
    {
        r = Math.Min(r, Math.Min(w / 2, h / 2));
        EnsurePath();

        _path!.MoveTo(x + r, y);
        _path.LineTo(x + w - r, y);
        _path.QuadTo(x + w, y, x + w, y + r);
        _path.LineTo(x + w, y + h - r);
        _path.QuadTo(x + w, y + h, x + w - r, y + h);
        _path.LineTo(x + r, y + h);
        _path.QuadTo(x, y + h, x, y + h - r);
        _path.LineTo(x, y + r);
        _path.QuadTo(x, y, x + r, y);
    }

    #endregion

    #region Helpers

    private static readonly Regex RgbaRegex = new(
        @"rgba?\s*\(\s*(?<r>\d{1,3})\s*,\s*(?<g>\d{1,3})\s*,\s*(?<b>\d{1,3})(?:\s*,\s*(?<a>[\d.]+))?\s*\)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static bool TryParseColor(string? input, out SKColor color)
    {
        color = SKColors.Transparent;
        if (string.IsNullOrWhiteSpace(input)) return false;

        string s = input.Trim();

        // Check for rgba/rgb format
        var match = RgbaRegex.Match(s);
        if (match.Success)
        {
            int r = int.Parse(match.Groups["r"].Value, CultureInfo.InvariantCulture);
            int g = int.Parse(match.Groups["g"].Value, CultureInfo.InvariantCulture);
            int b = int.Parse(match.Groups["b"].Value, CultureInfo.InvariantCulture);
            float a = match.Groups["a"].Success
                ? float.Parse(match.Groups["a"].Value, CultureInfo.InvariantCulture)
                : 1f;

            color = new SKColor((byte)r, (byte)g, (byte)b, (byte)(a * 255));
            return true;
        }

        // Try hex format
        try
        {
            color = SKColor.Parse(s);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static float RadiansToDegrees(float radians)
    {
        return (float)(radians * (180.0 / Math.PI));
    }

    private static float ParseFontSize(string? fontSpec)
    {
        if (string.IsNullOrWhiteSpace(fontSpec)) return 16f;

        var match = Regex.Match(fontSpec, @"(\d+(?:\.\d+)?)\s*px", RegexOptions.CultureInvariant);
        if (match.Success && float.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float size))
        {
            return size;
        }

        return 16f;
    }

    #endregion

    public void Dispose()
    {
        _shadowFilter?.Dispose();
        _path?.Dispose();
        _fillPaint.Dispose();
        _strokePaint.Dispose();
        _textPaint.Dispose();
        _skFont.Dispose();
    }
}
