using Microsoft.JSInterop;
using BlazorHero.Client.Models;

namespace BlazorHero.Client.Services;

public class InputService : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private IJSObjectReference? _inputModule;
    private DotNetObjectReference<InputService>? _selfReference;
    private bool _isInitialized;

    // Current key states (5 lanes for Expert mode)
    private readonly bool[] _lanePressed = new bool[5];
    private readonly double[] _laneLastPressTime = new double[5];

    // Events
    public event Action<Lane, double>? LanePressed;
    public event Action<Lane, double>? LaneReleased;
    public event Action<string>? SpecialKeyPressed;

    public bool IsInitialized => _isInitialized;

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

    public bool IsLanePressed(Lane lane) => _lanePressed[(int)lane];

    public bool IsLanePressed(int laneIndex) =>
        laneIndex >= 0 && laneIndex < 5 && _lanePressed[laneIndex];

    public bool[] GetAllLaneStates() => _lanePressed.ToArray();

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
        _selfReference?.Dispose();
    }
}
