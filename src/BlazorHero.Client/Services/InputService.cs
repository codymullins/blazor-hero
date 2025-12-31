using Microsoft.JSInterop;
using BlazorHero.Client.Models;

namespace BlazorHero.Client.Services;

public class InputService : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private IJSObjectReference? _inputModule;
    private IJSObjectReference? _touchModule;
    private DotNetObjectReference<InputService>? _selfReference;
    private bool _isInitialized;
    private bool _isTouchInitialized;

    // Current key states (5 lanes for Expert mode)
    private readonly bool[] _lanePressed = new bool[5];
    private readonly double[] _laneLastPressTime = new double[5];

    // Touch lane states (separate tracking for debugging)
    private readonly bool[] _touchLanePressed = new bool[5];

    // Events - keyboard and touch fire the same events
    public event Action<Lane, double>? LanePressed;
    public event Action<Lane, double>? LaneReleased;
    public event Action<string>? SpecialKeyPressed;

    public bool IsInitialized => _isInitialized;
    public bool IsTouchInitialized => _isTouchInitialized;

    public InputService(IJSRuntime js)
    {
        _js = js;
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized) return;

        _inputModule = await _js.InvokeAsync<IJSObjectReference>(
            "import", "./js/inputHandler.js");

        _selfReference = DotNetObjectReference.Create(this);
        await _inputModule.InvokeVoidAsync("initInput", _selfReference);
        _isInitialized = true;
    }

    /// <summary>
    /// Initialize touch input handling for mobile devices.
    /// </summary>
    public async Task InitializeTouchAsync()
    {
        if (_isTouchInitialized) return;

        _touchModule = await _js.InvokeAsync<IJSObjectReference>(
            "import", "./js/touchHandler.js");

        // Reuse the same reference - touch events will call OnTouchLaneDown/Up
        if (_selfReference == null)
        {
            _selfReference = DotNetObjectReference.Create(this);
        }

        await _touchModule.InvokeVoidAsync("initTouch", _selfReference);
        _isTouchInitialized = true;

        Console.WriteLine("[InputService] Touch initialized");
    }

    /// <summary>
    /// Register touch controls container after it's rendered.
    /// </summary>
    public async Task RegisterTouchControlsAsync(string containerId)
    {
        if (_touchModule == null)
        {
            await InitializeTouchAsync();
        }

        await _touchModule!.InvokeVoidAsync("registerTouchControls", containerId);
    }

    /// <summary>
    /// Unregister touch controls when component unmounts.
    /// </summary>
    public async Task UnregisterTouchControlsAsync()
    {
        if (_touchModule != null)
        {
            try
            {
                await _touchModule.InvokeVoidAsync("unregisterTouchControls");
            }
            catch
            {
                // Ignore errors during cleanup
            }
        }
    }

    /// <summary>
    /// Set lane boundaries for coordinate-based touch detection.
    /// </summary>
    /// <param name="boundaries">Array of X positions defining lane edges (length = laneCount + 1)</param>
    public async Task SetLaneBoundariesAsync(double[] boundaries)
    {
        if (_touchModule == null)
        {
            await InitializeTouchAsync();
        }

        await _touchModule!.InvokeVoidAsync("setLaneBoundaries", boundaries);
    }

    /// <summary>
    /// Update the touch overlay rect after window resize.
    /// </summary>
    public async Task UpdateTouchOverlayRectAsync()
    {
        if (_touchModule != null)
        {
            try
            {
                await _touchModule.InvokeVoidAsync("updateOverlayRect");
            }
            catch
            {
                // Ignore errors
            }
        }
    }

    /// <summary>
    /// Clear all active touch states (useful when pausing/ending game).
    /// </summary>
    public async Task ClearActiveTouchesAsync()
    {
        if (_touchModule != null)
        {
            try
            {
                await _touchModule.InvokeVoidAsync("clearActiveTouches");
            }
            catch
            {
                // Ignore errors
            }
        }

        // Clear local state
        for (int i = 0; i < 5; i++)
        {
            _touchLanePressed[i] = false;
        }
    }

    // Keyboard event handlers (existing)
    [JSInvokable]
    public void OnLaneKeyDown(int laneIndex, double timestamp)
    {
        if (laneIndex < 0 || laneIndex >= 5) return;

        _lanePressed[laneIndex] = true;
        _laneLastPressTime[laneIndex] = timestamp;

        LanePressed?.Invoke((Lane)laneIndex, timestamp);
    }

    [JSInvokable]
    public void OnLaneKeyUp(int laneIndex, double timestamp)
    {
        if (laneIndex < 0 || laneIndex >= 5) return;

        _lanePressed[laneIndex] = false;

        LaneReleased?.Invoke((Lane)laneIndex, timestamp);
    }

    [JSInvokable]
    public void OnSpecialKey(string keyName)
    {
        Console.WriteLine($"[InputService] OnSpecialKey: {keyName}");
        SpecialKeyPressed?.Invoke(keyName);
    }

    // Touch event handlers (new) - fire the same events as keyboard
    [JSInvokable]
    public void OnTouchLaneDown(int laneIndex, double timestamp)
    {
        if (laneIndex < 0 || laneIndex >= 5) return;

        _touchLanePressed[laneIndex] = true;
        _laneLastPressTime[laneIndex] = timestamp;

        Console.WriteLine($"[InputService] Touch lane down: {laneIndex}");

        // Fire the same event as keyboard - game logic doesn't care about input source
        LanePressed?.Invoke((Lane)laneIndex, timestamp);
    }

    [JSInvokable]
    public void OnTouchLaneUp(int laneIndex, double timestamp)
    {
        if (laneIndex < 0 || laneIndex >= 5) return;

        _touchLanePressed[laneIndex] = false;

        Console.WriteLine($"[InputService] Touch lane up: {laneIndex}");

        // Fire the same event as keyboard
        LaneReleased?.Invoke((Lane)laneIndex, timestamp);
    }

    [JSInvokable]
    public void OnTouchSpecialKey(string keyName)
    {
        Console.WriteLine($"[InputService] Touch special key: {keyName}");
        SpecialKeyPressed?.Invoke(keyName);
    }

    public bool IsLanePressed(Lane lane) => _lanePressed[(int)lane] || _touchLanePressed[(int)lane];

    public bool IsLanePressed(int laneIndex) =>
        laneIndex >= 0 && laneIndex < 5 && (_lanePressed[laneIndex] || _touchLanePressed[laneIndex]);

    public bool[] GetAllLaneStates()
    {
        // Combine keyboard and touch states
        var combined = new bool[5];
        for (int i = 0; i < 5; i++)
        {
            combined[i] = _lanePressed[i] || _touchLanePressed[i];
        }
        return combined;
    }

    public double GetLastPressTime(Lane lane) => _laneLastPressTime[(int)lane];

    public async ValueTask DisposeAsync()
    {
        if (_inputModule != null)
        {
            try
            {
                await _inputModule.InvokeVoidAsync("disposeInput");
                await _inputModule.DisposeAsync();
            }
            catch
            {
                // Ignore disposal errors
            }
        }

        if (_touchModule != null)
        {
            try
            {
                await _touchModule.InvokeVoidAsync("disposeTouch");
                await _touchModule.DisposeAsync();
            }
            catch
            {
                // Ignore disposal errors
            }
        }

        _selfReference?.Dispose();
    }
}
