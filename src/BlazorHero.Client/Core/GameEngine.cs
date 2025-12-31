using Microsoft.JSInterop;
using Blazor.Extensions.Canvas.Canvas2D;
using BlazorHero.Client.Models;
using BlazorHero.Client.Services;
using BlazorHero.Client.Rendering;

namespace BlazorHero.Client.Core;

public class GameEngine : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private readonly GameState _state;
    private readonly AudioService _audio;
    private readonly InputService _input;
    private readonly ScoringService _scoring;
    private readonly ChartService _charts;

    private IJSObjectReference? _gameLoopModule;
    private DotNetObjectReference<GameEngine>? _selfReference;
    private Canvas2DContext? _ctx;

    // Initialization state tracking
    private bool _isModuleInitialized;
    private bool _isCanvasInitialized;
    private Task? _initializationTask;  // Shared task for non-blocking init

    // Rendering
    private PerspectiveCamera _camera = new();
    private HighwayRenderer? _highwayRenderer;
    private NoteRenderer? _noteRenderer;
    private EffectRenderer? _effectRenderer;

    // Canvas dimensions
    private double _canvasWidth = 800;
    private double _canvasHeight = 600;

    // Game state
    private Chart? _currentChart;
    private List<Note> _activeNotes = new();
    private List<Note> _chartNotes = new();
    private int _nextNoteIndex;
    private double _songPosition;
    private double _countdownTime;
    private bool _isRunning;
    private double _songEndTime;  // Calculated from last note + buffer

    // Timing constants
    private const double COUNTDOWN_DURATION = 3000;  // 3 second countdown

    // Note travel time varies by difficulty (ms from spawn to hit line)
    // Difficulty comes primarily from note density, not speed
    private double NoteTravelTime => _state.SelectedDifficulty switch
    {
        Difficulty.VeryEasy => 3500,  // Slowest pace, 2 lanes
        Difficulty.Easy => 3200,      // Slow pace, 3 lanes
        Difficulty.Medium => 3200,    // Same speed as Easy, 4 lanes
        Difficulty.Hard => 2800,      // Faster pace, 4 lanes
        Difficulty.Expert => 2800,    // Same speed as Hard, 5 lanes
        _ => 2800
    };

    // Lane count varies by difficulty (must match chart data)
    public int LaneCount => _state.SelectedDifficulty switch
    {
        Difficulty.VeryEasy => 2,   // Green, Red
        Difficulty.Easy => 3,       // Green, Red, Yellow
        Difficulty.Medium => 4,     // Green, Red, Yellow, Blue
        Difficulty.Hard => 4,       // Green, Red, Yellow, Blue
        Difficulty.Expert => 5,     // Green, Red, Yellow, Blue, Orange
        _ => 4
    };

    // Events
    public event Action? StateChanged;

    // Public properties
    public int Score => _scoring.Score;
    public int Combo => _scoring.Combo;
    public int Multiplier => _scoring.Multiplier;
    public double StarPowerMeter => _scoring.StarPowerMeter;
    public bool IsStarPowerActive => _scoring.IsStarPowerActive;
    public GameStateType CurrentState => _state.Current;
    public SongMeta? CurrentSongMeta => _currentChart?.Meta;
    public PlayerStats? LastStats { get; private set; }
    public PerspectiveCamera Camera => _camera;

    public GameEngine(
        IJSRuntime js,
        GameState state,
        AudioService audio,
        InputService input,
        ScoringService scoring,
        ChartService charts)
    {
        _js = js;
        _state = state;
        _audio = audio;
        _input = input;
        _scoring = scoring;
        _charts = charts;

        // Wire up events
        _input.LanePressed += OnLanePressed;
        _input.LaneReleased += OnLaneReleased;
        _input.SpecialKeyPressed += OnSpecialKey;
        _scoring.ComboChanged += OnComboChanged;
        _scoring.ComboBreak += OnComboBreak;
    }

    /// <summary>
    /// Initialize JS modules once at app startup. Can be called multiple times safely.
    /// Uses shared Task pattern to avoid deadlocks in single-threaded Blazor WASM.
    /// </summary>
    public Task InitializeModulesAsync()
    {
        Console.WriteLine($"[InitializeModulesAsync] Called: _isModuleInitialized={_isModuleInitialized}, _initializationTask={_initializationTask != null}, taskStatus={_initializationTask?.Status}");

        // If already initialized, return completed task
        if (_isModuleInitialized)
        {
            Console.WriteLine("[InitializeModulesAsync] Already initialized, returning CompletedTask");
            return Task.CompletedTask;
        }

        // If initialization is in progress AND task is still running, return the existing task
        if (_initializationTask != null && !_initializationTask.IsCompleted)
        {
            Console.WriteLine("[InitializeModulesAsync] Task in progress, returning existing task");
            return _initializationTask;
        }

        // If task exists but completed without setting _isModuleInitialized (faulted or cancelled),
        // or if task is null, start a new one
        if (_initializationTask != null)
        {
            Console.WriteLine($"[InitializeModulesAsync] Previous task completed but init failed (status={_initializationTask.Status}), restarting");
        }

        // Start initialization and store the task
        Console.WriteLine("[InitializeModulesAsync] Starting new initialization task");
        _initializationTask = DoInitializeModulesAsync();
        return _initializationTask;
    }

    private async Task DoInitializeModulesAsync()
    {
        try
        {
            Console.WriteLine("[DoInitializeModulesAsync] Starting module initialization");

            // Initialize services
            Console.WriteLine("[DoInitializeModulesAsync] Initializing audio...");
            await _audio.InitializeAsync();
            Console.WriteLine("[DoInitializeModulesAsync] Initializing input...");
            await _input.InitializeAsync();

            // Load game loop module ONCE
            Console.WriteLine("[DoInitializeModulesAsync] Loading game loop module...");
            _gameLoopModule = await _js.InvokeAsync<IJSObjectReference>(
                "import", "./js/gameLoop.js");

            _isModuleInitialized = true;
            Console.WriteLine("[DoInitializeModulesAsync] Modules loaded successfully!");

            // Only transition to MainMenu if we're still in Loading state
            // Don't interrupt if user already started a song
            if (_state.Current == GameStateType.Loading)
            {
                _state.TransitionTo(GameStateType.MainMenu);
                StateChanged?.Invoke();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DoInitializeModulesAsync] ERROR: {ex.GetType().Name}: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Bind a new canvas context. Called each time GameScreen mounts.
    /// Always called from UI thread, no lock needed.
    /// </summary>
    public void BindCanvas(Canvas2DContext ctx, double width, double height)
    {
        Console.WriteLine($"[BindCanvas] Starting, ctx={ctx != null}, state={_state.Current}");

        _ctx = ctx;
        _canvasWidth = width;
        _canvasHeight = height;

        _camera.Initialize(width, height);
        _highwayRenderer = new HighwayRenderer(_camera);
        _noteRenderer = new NoteRenderer(_camera);
        _effectRenderer = new EffectRenderer(_camera);

        _isCanvasInitialized = true;
        Console.WriteLine($"[BindCanvas] Complete, _ctx={_ctx != null}, _isCanvasInitialized={_isCanvasInitialized}");
    }

    /// <summary>
    /// Unbind canvas when GameScreen unmounts. Does not dispose the engine.
    /// </summary>
    public void UnbindCanvas()
    {
        _ctx = null;
        _isCanvasInitialized = false;
        _highwayRenderer = null;
        _noteRenderer = null;
        _effectRenderer = null;
    }

    public async Task StartGameLoopAsync()
    {
        Console.WriteLine($"[StartGameLoopAsync] Starting, _isRunning={_isRunning}, _isModuleInitialized={_isModuleInitialized}, _ctx={_ctx != null}");
        if (_isRunning) return;

        // Ensure module is loaded
        if (!_isModuleInitialized || _gameLoopModule == null)
        {
            Console.WriteLine("[StartGameLoopAsync] Modules not initialized, calling InitializeModulesAsync");
            await InitializeModulesAsync();
        }

        // Guard against disposed module
        if (_gameLoopModule == null)
        {
            throw new InvalidOperationException("Game loop module not available");
        }

        _selfReference?.Dispose();  // Clean up any old reference
        _selfReference = DotNetObjectReference.Create(this);

        try
        {
            await _gameLoopModule.InvokeVoidAsync("initGameLoop", _selfReference);
            _isRunning = true;
        }
        catch (ObjectDisposedException)
        {
            // Module was disposed, need to reinitialize
            _isModuleInitialized = false;
            _gameLoopModule = null;
            await InitializeModulesAsync();

            // Retry once
            _selfReference?.Dispose();
            _selfReference = DotNetObjectReference.Create(this);
            await _gameLoopModule!.InvokeVoidAsync("initGameLoop", _selfReference);
            _isRunning = true;
        }
        catch (JSDisconnectedException)
        {
            // Browser was closed, ignore
        }
    }

    public async Task StopGameLoopAsync()
    {
        if (!_isRunning) return;

        try
        {
            if (_gameLoopModule != null)
            {
                await _gameLoopModule.InvokeVoidAsync("stopGameLoop");
            }
        }
        catch (ObjectDisposedException)
        {
            // Module already disposed, ignore
        }
        catch (JSDisconnectedException)
        {
            // Browser closed, ignore
        }

        _isRunning = false;
    }

    [JSInvokable]
    public async ValueTask OnFrame(double deltaMs, double timestamp)
    {
        if (_ctx == null)
        {
            Console.WriteLine($"[OnFrame] _ctx is null, _isCanvasInitialized={_isCanvasInitialized}");
            return;
        }

        // Update game logic based on state
        switch (_state.Current)
        {
            case GameStateType.Countdown:
                UpdateCountdown(deltaMs);
                break;
            case GameStateType.Playing:
                await UpdateGameplay(deltaMs);
                break;
        }

        // Render
        await RenderFrame(deltaMs);
    }

    private void UpdateCountdown(double deltaMs)
    {
        _countdownTime -= deltaMs;

        // During countdown, calculate virtual song position (negative, approaching 0)
        // This allows notes to spawn and travel toward the hit line before the song starts
        _songPosition = -_countdownTime;

        // Spawn and update notes during countdown so they're visible
        SpawnUpcomingNotes();
        UpdateNotes();

        if (_countdownTime <= 0)
        {
            _state.TransitionTo(GameStateType.Playing);
            _ = _audio.PlaySongAsync();
            StateChanged?.Invoke();
        }
    }

    private async Task UpdateGameplay(double deltaMs)
    {
        // Get authoritative song position from audio (sync for performance)
        _songPosition = _audio.GetCurrentTime();

        // Spawn upcoming notes
        SpawnUpcomingNotes();

        // Update note positions
        UpdateNotes();

        // Check for missed notes
        CheckMissedNotes();

        // Update star power drain
        _scoring.UpdateStarPower(deltaMs);

        // Update combo flames
        _effectRenderer?.UpdateComboFlames(_scoring.Combo);

        // Check for song end - all notes spawned and we're past the end time
        bool allNotesSpawned = _nextNoteIndex >= _chartNotes.Count;
        bool pastEndTime = _songPosition >= _songEndTime;
        
        if (allNotesSpawned && pastEndTime)
        {
            await EndSong();
        }
    }

    private void SpawnUpcomingNotes()
    {
        while (_nextNoteIndex < _chartNotes.Count)
        {
            var note = _chartNotes[_nextNoteIndex];
            double spawnTime = note.Time - NoteTravelTime;

            if (_songPosition >= spawnTime)
            {
                _activeNotes.Add(note);
                _nextNoteIndex++;
            }
            else
            {
                break;
            }
        }
    }

    private void UpdateNotes()
    {
        foreach (var note in _activeNotes)
        {
            double timeUntilHit = note.Time - _songPosition;
            note.CurrentZ = _camera.TimeToNormalizedZ(timeUntilHit, NoteTravelTime);
        }
    }

    private void CheckMissedNotes()
    {
        foreach (var note in _activeNotes.Where(n => n.IsActive && !n.IsHoldActive))
        {
            double timeSinceHit = _songPosition - note.Time;
            
            // For hold notes, don't mark as missed until the ENTIRE hold duration has passed
            // This gives the player time to press anywhere during the hold
            double missThreshold = note.IsHoldNote 
                ? note.Duration + ScoringService.WINDOW_GOOD  // After tail passes
                : ScoringService.WINDOW_MISS;                  // Normal timing window

            if (timeSinceHit > missThreshold)
            {
                note.IsMissed = true;
                _scoring.ProcessMiss(note);
                _effectRenderer?.TriggerHitEffect(note.Lane, HitJudgment.Miss);
                _ = _audio.PlaySfxAsync("miss", 0.6);
            }
        }

        // Check for hold notes that have completed their duration
        foreach (var note in _activeNotes.Where(n => n.IsHoldActive).ToList())
        {
            double holdEndTime = note.Time + note.Duration;
            
            // Update hold progress
            double actualHoldDuration = _songPosition - note.Time;
            note.HoldProgress = Math.Clamp(actualHoldDuration / note.Duration, 0, 1);
            
            // Check if the hold duration has passed
            if (_songPosition >= holdEndTime + ScoringService.WINDOW_GOOD)
            {
                // Player held all the way through - perfect!
                note.IsHoldActive = false;
                note.IsHit = true;
                
                // Stop the sustained sound and visual effect
                _ = _audio.StopHoldSustainAsync(note.LaneIndex);
                _effectRenderer?.SetHoldActive(note.LaneIndex, false);
                
                var result = _scoring.ProcessHit(note, _songPosition);
                _ = PlayHitSound(note.Lane, HitJudgment.Perfect);
                _effectRenderer?.TriggerHitEffect(note.Lane, HitJudgment.Perfect);
                StateChanged?.Invoke();
            }
        }

        // Remove notes that have passed (but keep hold notes that are still being held)
        _activeNotes.RemoveAll(n => n.CurrentZ < -0.2 && !n.IsHoldActive && !n.IsHit);
    }

    private void OnLanePressed(Lane lane, double timestamp)
    {
        if (_state.Current != GameStateType.Playing) return;

        // Find the closest unhit note in this lane
        var targetNote = _activeNotes
            .Where(n => n.Lane == lane && n.IsActive)
            .OrderBy(n => Math.Abs(n.Time - _songPosition))
            .FirstOrDefault();

        if (targetNote == null)
        {
            // No note in this lane - play a muted thump sound
            _ = _audio.PlaySfxAsync("thump", 0.4);
            return;
        }

        double timingOffset = Math.Abs(_songPosition - targetNote.Time);

        // For hold notes, also check if we're within the hold duration
        // This allows pressing anytime from just before the head until the tail passes
        bool isWithinHoldWindow = false;
        if (targetNote.IsHoldNote)
        {
            double holdEndTime = targetNote.Time + targetNote.Duration;
            // Accept press from slightly before head to slightly after tail
            isWithinHoldWindow = _songPosition >= (targetNote.Time - ScoringService.WINDOW_MISS) 
                              && _songPosition <= (holdEndTime + ScoringService.WINDOW_GOOD);
        }

        if (timingOffset <= ScoringService.WINDOW_MISS || isWithinHoldWindow)
        {
            if (targetNote.IsHoldNote)
            {
                // Start holding - don't mark as fully hit yet
                targetNote.IsHoldActive = true;
                
                // Calculate how much of the hold we've already missed
                double missedTime = Math.Max(0, _songPosition - targetNote.Time);
                targetNote.HoldProgress = Math.Clamp(missedTime / targetNote.Duration, 0, 1);
                
                // Play hit sound for starting the hold (judge based on head timing)
                var judgment = ScoringService.GetJudgment(Math.Min(timingOffset, ScoringService.WINDOW_GOOD));
                _ = PlayHitSound(lane, judgment);
                _effectRenderer?.TriggerHitEffect(lane, judgment);
                
                // Show visual indicator that hold is active
                _effectRenderer?.SetHoldActive((int)lane, true);
                
                // Start sustained guitar tone while holding
                _ = _audio.StartHoldSustainAsync((int)lane, 0.35);
            }
            else
            {
                // Regular tap note
                targetNote.IsHit = true;
                var result = _scoring.ProcessHit(targetNote, _songPosition);
                _effectRenderer?.TriggerHitEffect(lane, result.Judgment);
                _ = PlayHitSound(lane, result.Judgment);
            }

            StateChanged?.Invoke();
        }
        else
        {
            // Note exists but too far away - play thump
            _ = _audio.PlaySfxAsync("thump", 0.4);
        }
    }

    private void OnLaneReleased(Lane lane, double timestamp)
    {
        if (_state.Current != GameStateType.Playing) return;

        // Stop the sustained hold sound and visual effect for this lane
        _ = _audio.StopHoldSustainAsync((int)lane);
        _effectRenderer?.SetHoldActive((int)lane, false);

        // Find any hold note being held in this lane
        var holdNote = _activeNotes
            .FirstOrDefault(n => n.Lane == lane && n.IsHoldActive);

        if (holdNote == null) return;

        // Get current song position (may be more up-to-date than _songPosition)
        double currentTime = _audio.GetCurrentTime();
        
        // Calculate how much of the hold was completed
        double holdEndTime = holdNote.Time + holdNote.Duration;
        double actualHoldDuration = currentTime - holdNote.Time;
        holdNote.HoldProgress = Math.Clamp(actualHoldDuration / holdNote.Duration, 0, 1);

        // Mark the hold as no longer active
        holdNote.IsHoldActive = false;

        // Score based on hold completion and release timing
        if (holdNote.HoldProgress < 0.5)
        {
            // Released way too early - miss
            holdNote.IsMissed = true;
            holdNote.IsHit = false;
            _scoring.ProcessMiss(holdNote);
            _ = _audio.PlaySfxAsync("miss", 0.6);
        }
        else
        {
            // Held enough - score it as a hit
            holdNote.IsHit = true;
            var result = _scoring.ProcessHit(holdNote, currentTime);
            _ = PlayHitSound(lane, result.Judgment);
            _effectRenderer?.TriggerHitEffect(lane, result.Judgment);
        }

        StateChanged?.Invoke();
    }

    private async Task PlayHitSound(Lane lane, HitJudgment judgment)
    {
        // Each lane has its own sound (like different guitar strings)
        // Format: hit_<lane>_<quality> e.g., hit_0_perfect, hit_1_good
        int laneIndex = (int)lane;
        string quality = judgment switch
        {
            HitJudgment.Perfect => "perfect",
            HitJudgment.Great => "good",
            HitJudgment.Good => "good",
            _ => "good"
        };
        string sfx = $"hit_{laneIndex}_{quality}";
        await _audio.PlaySfxAsync(sfx, 0.6);
    }

    private void OnSpecialKey(string key)
    {
        switch (key)
        {
            case "pause":
            case "escape": // Both ESC key and pause button trigger this
                if (_state.CanPause)
                {
                    _ = PauseGame();
                }
                else if (_state.CanResume)
                {
                    _ = ResumeGame();
                }
                break;
            case "starpower":
                if (_state.Current == GameStateType.Playing && _scoring.CanActivateStarPower)
                {
                    _scoring.ActivateStarPower();
                    _ = _audio.PlaySfxAsync("starpower", 0.7);
                    StateChanged?.Invoke();
                }
                break;
        }
    }

    private void OnComboChanged(int combo)
    {
        StateChanged?.Invoke();
    }

    private void OnComboBreak()
    {
        _effectRenderer?.ClearFlames();
        // Note: miss sound is now played in CheckMissedNotes for each missed note
    }

    private static int _frameCount = 0;
    private async Task RenderFrame(double deltaMs)
    {
        // Guard against uninitialized or disposed canvas
        if (_ctx == null || !_isCanvasInitialized)
        {
            Console.WriteLine($"[RenderFrame] Early return: _ctx={_ctx != null}, _isCanvasInitialized={_isCanvasInitialized}");
            return;
        }

        _frameCount++;
        if (_frameCount % 60 == 1) // Log every ~1 second
        {
            Console.WriteLine($"[RenderFrame] Frame {_frameCount}, state={_state.Current}, countdown={_countdownTime:F0}ms");
        }

        try
        {
            // Clear canvas
            await _ctx.ClearRectAsync(0, 0, _canvasWidth, _canvasHeight);

            // Draw background
            await _ctx.SetFillStyleAsync("#0a0a0f");
            await _ctx.FillRectAsync(0, 0, _canvasWidth, _canvasHeight);

            if (_state.Current == GameStateType.Playing ||
                _state.Current == GameStateType.Countdown ||
                _state.Current == GameStateType.Paused)
            {
                // Render highway
                bool[] laneStates = _input.GetAllLaneStates();
                await _highwayRenderer!.RenderAsync(_ctx, laneStates);

                // Render notes (set travel time for hold note calculations)
                _noteRenderer!.NoteTravelTime = NoteTravelTime;
                await _noteRenderer.RenderAllNotesAsync(_ctx, _activeNotes);

                // Render effects
                await _effectRenderer!.RenderAsync(_ctx, deltaMs);

                // Render countdown
                if (_state.Current == GameStateType.Countdown)
                {
                    await RenderCountdown();
                }

                // Render pause overlay
                if (_state.Current == GameStateType.Paused)
                {
                    await RenderPauseOverlay();
                }
            }
        }
        catch (ObjectDisposedException)
        {
            // Canvas was disposed during render (navigation), stop rendering
            _isRunning = false;
        }
        catch (JSDisconnectedException)
        {
            // Browser disconnected, stop rendering
            _isRunning = false;
        }
    }

    private async Task RenderCountdown()
    {
        int countdownNumber = (int)Math.Ceiling(_countdownTime / 1000);
        if (countdownNumber <= 0) return;

        double x = _canvasWidth / 2;
        double y = _canvasHeight / 2;

        // Background overlay
        await _ctx!.SetFillStyleAsync("rgba(0, 0, 0, 0.5)");
        await _ctx.FillRectAsync(0, 0, _canvasWidth, _canvasHeight);

        // Number
        await _ctx.SetFillStyleAsync("#FFD700");
        await _ctx.SetFontAsync("bold 120px 'Segoe UI', sans-serif");
        await _ctx.SetTextAlignAsync(TextAlign.Center);
        await _ctx.SetTextBaselineAsync(TextBaseline.Middle);
        await _ctx.FillTextAsync(countdownNumber.ToString(), x, y);
    }

    private async Task RenderPauseOverlay()
    {
        await _ctx!.SetFillStyleAsync("rgba(0, 0, 0, 0.7)");
        await _ctx.FillRectAsync(0, 0, _canvasWidth, _canvasHeight);

        await _ctx.SetFillStyleAsync("#FFFFFF");
        await _ctx.SetFontAsync("bold 48px 'Segoe UI', sans-serif");
        await _ctx.SetTextAlignAsync(TextAlign.Center);
        await _ctx.SetTextBaselineAsync(TextBaseline.Middle);
        await _ctx.FillTextAsync("PAUSED", _canvasWidth / 2, _canvasHeight / 2 - 40);

        await _ctx.SetFontAsync("24px 'Segoe UI', sans-serif");
        await _ctx.FillTextAsync("Press ESC to resume", _canvasWidth / 2, _canvasHeight / 2 + 20);
    }

    // Public methods for UI interaction
    public async Task StartSongAsync(string chartFile, Difficulty difficulty)
    {
        _state.SelectedChartFile = chartFile;
        _state.SelectedDifficulty = difficulty;

        // Update camera lane count for this difficulty
        _camera.LaneCount = LaneCount;

        _currentChart = await _charts.LoadChartAsync(chartFile);
        if (_currentChart == null) return;

        // Load audio
        await _audio.LoadSongAsync(_currentChart.Meta.AudioFile);

        // Load song-specific note sounds (falls back to defaults if not defined)
        await _audio.LoadSongSoundsAsync(_currentChart.NoteSounds);

        // Ensure audio context is resumed (browser autoplay policy)
        await _audio.EnsureAudioResumedAsync();

        // Get notes for selected difficulty
        _chartNotes = _charts.GetNotesForDifficulty(_currentChart, difficulty);
        _activeNotes.Clear();
        _nextNoteIndex = 0;
        _songPosition = 0;
        _songEnded = false;

        // Calculate song end time from last note + small buffer
        if (_chartNotes.Count > 0)
        {
            var lastNote = _chartNotes[^1];
            _songEndTime = lastNote.Time + lastNote.Duration + 500; // 0.5 seconds after last note
        }
        else
        {
            _songEndTime = 5000; // 5 seconds if no notes
        }

        // Reset scoring
        _scoring.Reset();
        _effectRenderer?.Clear();

        // Start countdown
        _countdownTime = COUNTDOWN_DURATION;
        _state.TransitionTo(GameStateType.Countdown);
        StateChanged?.Invoke();

        // Note: GameScreen will call StartGameLoopAsync after canvas initialization
    }

    public async Task PauseGame()
    {
        if (_state.Current != GameStateType.Playing) return;

        await _audio.StopAllHoldSustainsAsync();
        _effectRenderer?.ClearAllHolds();
        await _audio.PauseSongAsync();
        await _gameLoopModule!.InvokeVoidAsync("pauseGameLoop");
        _state.TransitionTo(GameStateType.Paused);
        StateChanged?.Invoke();
    }

    public async Task ResumeGame()
    {
        if (_state.Current != GameStateType.Paused) return;

        _state.TransitionTo(GameStateType.Playing);
        await _audio.ResumeSongAsync();
        await _gameLoopModule!.InvokeVoidAsync("resumeGameLoop");
        StateChanged?.Invoke();
    }

    public async Task RestartSong()
    {
        if (_state.SelectedChartFile == null) return;

        await _audio.StopSongAsync();
        await StartSongAsync(_state.SelectedChartFile, _state.SelectedDifficulty);
    }

    public async Task QuitToMenu()
    {
        await _audio.StopAllHoldSustainsAsync();
        _effectRenderer?.ClearAllHolds();
        await _audio.StopSongAsync();
        await _audio.ClearSongSoundsAsync();
        _activeNotes.Clear();
        _chartNotes.Clear();
        _effectRenderer?.Clear();
        _state.TransitionTo(GameStateType.SongSelect);
        StateChanged?.Invoke();
    }

    private bool _songEnded;
    
    private async Task EndSong()
    {
        // Prevent multiple calls
        if (_songEnded) return;
        _songEnded = true;
        
        Console.WriteLine("[GameEngine] EndSong called - transitioning to Results");
        
        await StopGameLoopAsync();
        await _audio.StopAllHoldSustainsAsync();
        _effectRenderer?.ClearAllHolds();
        await _audio.StopSongAsync();
        LastStats = _scoring.GetFinalStats(_chartNotes.Count);
        
        Console.WriteLine($"[GameEngine] Final stats: Score={LastStats.Score}, Accuracy={LastStats.Accuracy}");
        
        _state.TransitionTo(GameStateType.Results);
        StateChanged?.Invoke();
        
        Console.WriteLine($"[GameEngine] State is now: {_state.Current}");
    }

    public void GoToMainMenu()
    {
        _state.TransitionTo(GameStateType.MainMenu);
        StateChanged?.Invoke();
    }

    public void GoToSongSelect()
    {
        _state.TransitionTo(GameStateType.SongSelect);
        StateChanged?.Invoke();
    }

    public async ValueTask DisposeAsync()
    {
        await StopGameLoopAsync();

        _input.LanePressed -= OnLanePressed;
        _input.LaneReleased -= OnLaneReleased;
        _input.SpecialKeyPressed -= OnSpecialKey;
        _scoring.ComboChanged -= OnComboChanged;
        _scoring.ComboBreak -= OnComboBreak;

        _selfReference?.Dispose();
        _selfReference = null;

        if (_gameLoopModule != null)
        {
            try
            {
                await _gameLoopModule.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // Browser closed, ignore
            }
        }

        _gameLoopModule = null;
        _isModuleInitialized = false;
        _initializationTask = null;
    }
}
