using Microsoft.JSInterop;

namespace BlazorHero.Client.Services;

public class AudioService : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private IJSObjectReference? _audioModule;
    private IJSInProcessObjectReference? _audioModuleSync;  // For sync interop in game loop
    private bool _isInitialized;

    public bool IsInitialized => _isInitialized;
    public double SongDuration { get; private set; }

    public AudioService(IJSRuntime js)
    {
        _js = js;
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized) return;

        _audioModule = await _js.InvokeAsync<IJSObjectReference>(
            "import", "./js/audioEngine.js");

        // Cast to sync interface for high-frequency calls (Blazor WASM only)
        _audioModuleSync = _audioModule as IJSInProcessObjectReference;

        await _audioModule.InvokeVoidAsync("initAudio");
        _isInitialized = true;
    }

    /// <summary>
    /// Ensure audio is initialized. Handles recovery if module was disposed.
    /// </summary>
    public async Task EnsureInitializedAsync()
    {
        if (_isInitialized && _audioModule != null) return;

        // Reset state if module was disposed
        _isInitialized = false;
        await InitializeAsync();
    }

    public async Task EnsureAudioResumedAsync()
    {
        if (_audioModule != null)
        {
            await _audioModule.InvokeVoidAsync("ensureAudioResumed");
        }
    }

    public async Task<double> LoadSongAsync(string audioFile)
    {
        if (!_isInitialized)
        {
            await InitializeAsync();
        }

        SongDuration = await _audioModule!.InvokeAsync<double>("loadSong", audioFile);
        return SongDuration;
    }

    public async Task<bool> LoadSfxAsync(string name, string url)
    {
        if (_audioModule == null) return false;

        return await _audioModule.InvokeAsync<bool>("loadSfx", name, url);
    }

    public async Task PlaySongAsync(double offsetMs = 0)
    {
        if (_audioModule == null) return;
        await _audioModule.InvokeVoidAsync("playSong", offsetMs);
    }

    public async Task PauseSongAsync()
    {
        if (_audioModule == null) return;
        await _audioModule.InvokeVoidAsync("pauseSong");
    }

    public async Task ResumeSongAsync()
    {
        if (_audioModule == null) return;
        await _audioModule.InvokeVoidAsync("resumeSong");
    }

    public async Task StopSongAsync()
    {
        if (_audioModule == null) return;
        await _audioModule.InvokeVoidAsync("stopSong");
    }

    /// <summary>
    /// Gets the current song position in milliseconds (async version).
    /// </summary>
    public async ValueTask<double> GetCurrentTimeAsync()
    {
        if (_audioModule == null) return 0;
        return await _audioModule.InvokeAsync<double>("getCurrentTime");
    }

    /// <summary>
    /// Gets the current song position in milliseconds (sync version).
    /// This is the authoritative time source for game synchronization.
    /// Uses synchronous interop for better performance in the game loop.
    /// </summary>
    public double GetCurrentTime()
    {
        if (_audioModuleSync == null) return 0;
        return _audioModuleSync.Invoke<double>("getCurrentTime");
    }

    public async ValueTask<bool> IsPlayingAsync()
    {
        if (_audioModule == null) return false;
        return await _audioModule.InvokeAsync<bool>("isPlaying");
    }

    public async Task PlaySfxAsync(string name, double volume = 1.0)
    {
        if (_audioModule == null) return;
        await _audioModule.InvokeVoidAsync("playSfx", name, volume);
    }

    public async Task SetVolumeAsync(double volume)
    {
        if (_audioModule == null) return;
        await _audioModule.InvokeVoidAsync("setVolume", volume);
    }

    public async Task SetSfxVolumeAsync(double volume)
    {
        if (_audioModule == null) return;
        await _audioModule.InvokeVoidAsync("setSfxVolume", volume);
    }

    public async Task StartHoldSustainAsync(int lane, double volume = 0.4)
    {
        if (_audioModule == null) return;
        await _audioModule.InvokeVoidAsync("startHoldSustain", lane, volume);
    }

    public async Task StopHoldSustainAsync(int lane)
    {
        if (_audioModule == null) return;
        await _audioModule.InvokeVoidAsync("stopHoldSustain", lane);
    }

    public async Task StopAllHoldSustainsAsync()
    {
        if (_audioModule == null) return;
        await _audioModule.InvokeVoidAsync("stopAllHoldSustains");
    }

    public async Task LoadSongSoundsAsync(object? noteSoundsConfig)
    {
        if (_audioModule == null) return;
        await _audioModule.InvokeVoidAsync("loadSongSounds", noteSoundsConfig);
    }

    public async Task ClearSongSoundsAsync()
    {
        if (_audioModule == null) return;
        await _audioModule.InvokeVoidAsync("clearSongSounds");
    }

    public async ValueTask DisposeAsync()
    {
        if (_audioModule != null)
        {
            try
            {
                await _audioModule.InvokeVoidAsync("stopSong");
                await _audioModule.DisposeAsync();
            }
            catch
            {
                // Ignore disposal errors
            }
        }
    }
}
