using Microsoft.JSInterop;

namespace BlazorHero.Client.Services;

/// <summary>
/// Service for detecting device type, capabilities, and viewport information.
/// Used to adapt the game for mobile browsers.
/// </summary>
public class DeviceService : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private IJSObjectReference? _module;
    private DotNetObjectReference<DeviceService>? _selfReference;
    private bool _isInitialized;

    // Cached device info
    private bool _isMobile;
    private bool _hasTouch;
    private int _viewportWidth;
    private int _viewportHeight;
    private string _orientation = "landscape";

    // Events
    public event Action? ViewportChanged;
    public event Action<string>? OrientationChanged;

    // Public properties
    public bool IsMobile => _isMobile;
    public bool HasTouch => _hasTouch;
    public int ViewportWidth => _viewportWidth;
    public int ViewportHeight => _viewportHeight;
    public string Orientation => _orientation;
    public bool IsPortrait => _orientation == "portrait";
    public bool IsLandscape => _orientation == "landscape";
    public bool IsInitialized => _isInitialized;

    public DeviceService(IJSRuntime js)
    {
        _js = js;
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized) return;

        _module = await _js.InvokeAsync<IJSObjectReference>(
            "import", "./js/deviceHelpers.js");

        _selfReference = DotNetObjectReference.Create(this);

        // Get initial device info
        _isMobile = await _module.InvokeAsync<bool>("isMobileDevice");
        _hasTouch = await _module.InvokeAsync<bool>("hasTouch");

        var viewport = await _module.InvokeAsync<ViewportSize>("getViewportSize");
        _viewportWidth = viewport.Width;
        _viewportHeight = viewport.Height;

        _orientation = await _module.InvokeAsync<string>("getOrientation");

        // Set up event listeners
        await _module.InvokeVoidAsync("initEventListeners", _selfReference);

        _isInitialized = true;

        Console.WriteLine($"[DeviceService] Initialized: mobile={_isMobile}, touch={_hasTouch}, viewport={_viewportWidth}x{_viewportHeight}, orientation={_orientation}");
    }

    [JSInvokable]
    public void OnViewportChange(int width, int height)
    {
        _viewportWidth = width;
        _viewportHeight = height;
        ViewportChanged?.Invoke();
    }

    [JSInvokable]
    public void OnOrientationChange(string orientation)
    {
        _orientation = orientation;
        OrientationChanged?.Invoke(orientation);
        ViewportChanged?.Invoke(); // Viewport also changes with orientation
    }

    /// <summary>
    /// Get the recommended canvas size based on viewport and device type.
    /// Reserves space for touch controls on mobile.
    /// </summary>
    public (int width, int height) GetRecommendedCanvasSize()
    {
        const double targetAspectRatio = 16.0 / 9.0;
        
        // Reserve space for touch controls on mobile
        int reservedHeight = _isMobile ? 100 : 0;
        
        double availableWidth = _viewportWidth;
        double availableHeight = _viewportHeight - reservedHeight;

        // Calculate dimensions that fit within available space while maintaining aspect ratio
        double widthFromHeight = availableHeight * targetAspectRatio;
        double heightFromWidth = availableWidth / targetAspectRatio;

        int canvasWidth, canvasHeight;

        if (widthFromHeight <= availableWidth)
        {
            // Height is the limiting factor
            canvasWidth = (int)widthFromHeight;
            canvasHeight = (int)availableHeight;
        }
        else
        {
            // Width is the limiting factor
            canvasWidth = (int)availableWidth;
            canvasHeight = (int)heightFromWidth;
        }

        // Ensure minimum size
        canvasWidth = Math.Max(canvasWidth, 320);
        canvasHeight = Math.Max(canvasHeight, 180);

        return (canvasWidth, canvasHeight);
    }

    /// <summary>
    /// Attempt to lock screen orientation (may not work on all browsers).
    /// </summary>
    public async Task<bool> TryLockOrientationAsync(string orientation)
    {
        if (_module == null) return false;

        try
        {
            return await _module.InvokeAsync<bool>("tryLockOrientation", orientation);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Request fullscreen mode for better mobile experience.
    /// </summary>
    public async Task<bool> TryRequestFullscreenAsync()
    {
        if (_module == null) return false;

        try
        {
            return await _module.InvokeAsync<bool>("tryRequestFullscreen");
        }
        catch
        {
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_module != null)
        {
            try
            {
                await _module.InvokeVoidAsync("disposeEventListeners");
                await _module.DisposeAsync();
            }
            catch
            {
                // Ignore disposal errors
            }
        }
        _selfReference?.Dispose();
    }

    private record ViewportSize(int Width, int Height);
}
