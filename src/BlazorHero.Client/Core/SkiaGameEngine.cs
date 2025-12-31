using Microsoft.JSInterop;
using BlazorHero.Client.Models;
using BlazorHero.Client.Services;
using BlazorHero.Client.Rendering;

namespace BlazorHero.Client.Core;

/// <summary>
/// Game engine optimized for Skia-based rendering.
/// Uses synchronous rendering instead of async JS interop.
/// </summary>
public class SkiaGameEngine : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private readonly GameState _state;
    private readonly AudioService _audio;
    private readonly InputService _input;
    private readonly ScoringService _scoring;
    private readonly ChartService _charts;

    private IJSObjectReference? _gameLoopModule;
    private DotNetObjectReference<SkiaGameEngine>? _selfReference;

    // Initialization state
    private bool _isModuleInitialized;
    private Task? _initializationTask;

    // Rendering
    private PerspectiveProjection _projection = new();
    private HighwayRendererSkia _highwayRenderer = new();
    private NoteRendererSkia _noteRenderer = new();
    private EffectRendererSkia _effectRenderer = new();

    // Canvas dimensions
    private double _canvasWidth = 1280;
    private double _canvasHeight = 720;

    // Game state
    private Chart? _currentChart;
    private List<Note> _activeNotes = new();
    private List<Note> _chartNotes = new();
    private int _nextNoteIndex;
    private double _songPosition;
    private double _countdownTime;
    private bool _isRunning;
    private double _songEndTime;
    private bool _songEnded;

    private const double COUNTDOWN_DURATION = 3000;

    // Note travel time varies by difficulty
    private double NoteTravelTime => _state.SelectedDifficulty switch
    {
        Difficulty.VeryEasy => 3500,
        Difficulty.Easy => 3200,
        Difficulty.Medium => 3200,
        Difficulty.Hard => 2800,
        Difficulty.Expert => 2800,
        _ => 2800
    };

    public int LaneCount => _state.SelectedDifficulty switch
    {
        Difficulty.VeryEasy => 2,
        Difficulty.Easy => 3,
        Difficulty.Medium => 4,
        Difficulty.Hard => 4,
        Difficulty.Expert => 5,
        _ => 4
    };

    // Events
    public event Action? StateChanged;
    public event Action? RenderRequested;

    // Public properties
    public int Score => _scoring.Score;
    public int Combo => _scoring.Combo;
    public int Multiplier => _scoring.Multiplier;
    public double StarPowerMeter => _scoring.StarPowerMeter;
    public bool IsStarPowerActive => _scoring.IsStarPowerActive;
    public GameStateType CurrentState => _state.Current;
    public SongMeta? CurrentSongMeta => _currentChart?.Meta;
    public PlayerStats? LastStats { get; private set; }
    public List<Note> ActiveNotes => _activeNotes;
    public double CurrentNoteTravelTime => NoteTravelTime;
    public double CountdownTime => _countdownTime;
    public PerspectiveProjection Projection => _projection;

    public SkiaGameEngine(
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

        _input.LanePressed += OnLanePressed;
        _input.LaneReleased += OnLaneReleased;
        _input.SpecialKeyPressed += OnSpecialKey;
        _scoring.ComboChanged += OnComboChanged;
        _scoring.ComboBreak += OnComboBreak;
    }

    public void Initialize(double width, double height)
    {
        _canvasWidth = width;
        _canvasHeight = height;
        _projection.Initialize(width, height);
        _projection.LaneCount = LaneCount;
    }

    public Task InitializeModulesAsync()
    {
        if (_isModuleInitialized) return Task.CompletedTask;
        if (_initializationTask != null && !_initializationTask.IsCompleted) return _initializationTask;

        _initializationTask = DoInitializeModulesAsync();
        return _initializationTask;
    }

    private async Task DoInitializeModulesAsync()
    {
        await _audio.InitializeAsync();
        await _input.InitializeAsync();

        _gameLoopModule = await _js.InvokeAsync<IJSObjectReference>("import", "./js/gameLoop.js");
        _isModuleInitialized = true;

        if (_state.Current == GameStateType.Loading)
        {
            _state.TransitionTo(GameStateType.MainMenu);
            StateChanged?.Invoke();
        }
    }

    public async Task StartGameLoopAsync()
    {
        if (_isRunning) return;

        if (!_isModuleInitialized || _gameLoopModule == null)
        {
            await InitializeModulesAsync();
        }

        _selfReference?.Dispose();
        _selfReference = DotNetObjectReference.Create(this);

        await _gameLoopModule!.InvokeVoidAsync("initGameLoop", _selfReference);
        _isRunning = true;
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
        catch { }

        _isRunning = false;
    }

    [JSInvokable]
    public async ValueTask OnFrame(double deltaMs, double timestamp)
    {
        switch (_state.Current)
        {
            case GameStateType.Countdown:
                UpdateCountdown(deltaMs);
                break;
            case GameStateType.Playing:
                await UpdateGameplay(deltaMs);
                break;
        }

        // Request render from the component
        RenderRequested?.Invoke();
    }

    private void UpdateCountdown(double deltaMs)
    {
        _countdownTime -= deltaMs;
        _songPosition = -_countdownTime;

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
        _songPosition = _audio.GetCurrentTime();

        SpawnUpcomingNotes();
        UpdateNotes();
        CheckMissedNotes();

        _scoring.UpdateStarPower(deltaMs);
        _effectRenderer.UpdateComboFlames(_scoring.Combo);

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
            note.CurrentZ = _projection.TimeToNormalizedZ(timeUntilHit, NoteTravelTime);
        }
    }

    private void CheckMissedNotes()
    {
        foreach (var note in _activeNotes.Where(n => n.IsActive && !n.IsHoldActive))
        {
            double timeSinceHit = _songPosition - note.Time;
            double missThreshold = note.IsHoldNote
                ? note.Duration + ScoringService.WINDOW_GOOD
                : ScoringService.WINDOW_MISS;

            if (timeSinceHit > missThreshold)
            {
                note.IsMissed = true;
                _scoring.ProcessMiss(note);
                _effectRenderer.TriggerHitEffect(note.Lane, HitJudgment.Miss);
                _ = _audio.PlaySfxAsync("miss", 0.6);
            }
        }

        foreach (var note in _activeNotes.Where(n => n.IsHoldActive).ToList())
        {
            double holdEndTime = note.Time + note.Duration;
            double actualHoldDuration = _songPosition - note.Time;
            note.HoldProgress = Math.Clamp(actualHoldDuration / note.Duration, 0, 1);

            if (_songPosition >= holdEndTime + ScoringService.WINDOW_GOOD)
            {
                note.IsHoldActive = false;
                note.IsHit = true;
                _ = _audio.StopHoldSustainAsync(note.LaneIndex);
                _effectRenderer.SetHoldActive(note.LaneIndex, false);
                _scoring.ProcessHit(note, _songPosition);
                _ = PlayHitSound(note.Lane, HitJudgment.Perfect);
                _effectRenderer.TriggerHitEffect(note.Lane, HitJudgment.Perfect);
                StateChanged?.Invoke();
            }
        }

        _activeNotes.RemoveAll(n => n.CurrentZ < -0.2 && !n.IsHoldActive && !n.IsHit);
    }

    /// <summary>
    /// Render the current frame using Skia.
    /// Called from the component's OnPaintSurface.
    /// </summary>
    public void RenderFrame(IGameRenderContext ctx, double deltaMs)
    {
        if (_state.Current == GameStateType.Playing ||
            _state.Current == GameStateType.Countdown ||
            _state.Current == GameStateType.Paused)
        {
            bool[] laneStates = _input.GetAllLaneStates();
            _highwayRenderer.Render(ctx, laneStates, LaneCount);

            _noteRenderer.NoteTravelTime = NoteTravelTime;
            _noteRenderer.RenderAllNotes(ctx, _activeNotes);

            _effectRenderer.Render(ctx, deltaMs);
        }
    }

    private void OnLanePressed(Lane lane, double timestamp)
    {
        if (_state.Current != GameStateType.Playing) return;

        var targetNote = _activeNotes
            .Where(n => n.Lane == lane && n.IsActive)
            .OrderBy(n => Math.Abs(n.Time - _songPosition))
            .FirstOrDefault();

        if (targetNote == null)
        {
            _ = _audio.PlaySfxAsync("thump", 0.4);
            return;
        }

        double timingOffset = Math.Abs(_songPosition - targetNote.Time);

        bool isWithinHoldWindow = false;
        if (targetNote.IsHoldNote)
        {
            double holdEndTime = targetNote.Time + targetNote.Duration;
            isWithinHoldWindow = _songPosition >= (targetNote.Time - ScoringService.WINDOW_MISS)
                              && _songPosition <= (holdEndTime + ScoringService.WINDOW_GOOD);
        }

        if (timingOffset <= ScoringService.WINDOW_MISS || isWithinHoldWindow)
        {
            if (targetNote.IsHoldNote)
            {
                targetNote.IsHoldActive = true;
                double missedTime = Math.Max(0, _songPosition - targetNote.Time);
                targetNote.HoldProgress = Math.Clamp(missedTime / targetNote.Duration, 0, 1);
                var judgment = ScoringService.GetJudgment(Math.Min(timingOffset, ScoringService.WINDOW_GOOD));
                _ = PlayHitSound(lane, judgment);
                _effectRenderer.TriggerHitEffect(lane, judgment);
                _effectRenderer.SetHoldActive((int)lane, true);
                _ = _audio.StartHoldSustainAsync((int)lane, 0.35);
            }
            else
            {
                targetNote.IsHit = true;
                var result = _scoring.ProcessHit(targetNote, _songPosition);
                _effectRenderer.TriggerHitEffect(lane, result.Judgment);
                _ = PlayHitSound(lane, result.Judgment);
            }

            StateChanged?.Invoke();
        }
        else
        {
            _ = _audio.PlaySfxAsync("thump", 0.4);
        }
    }

    private void OnLaneReleased(Lane lane, double timestamp)
    {
        if (_state.Current != GameStateType.Playing) return;

        _ = _audio.StopHoldSustainAsync((int)lane);
        _effectRenderer.SetHoldActive((int)lane, false);

        var holdNote = _activeNotes.FirstOrDefault(n => n.Lane == lane && n.IsHoldActive);
        if (holdNote == null) return;

        double currentTime = _audio.GetCurrentTime();
        double actualHoldDuration = currentTime - holdNote.Time;
        holdNote.HoldProgress = Math.Clamp(actualHoldDuration / holdNote.Duration, 0, 1);
        holdNote.IsHoldActive = false;

        if (holdNote.HoldProgress < 0.5)
        {
            holdNote.IsMissed = true;
            holdNote.IsHit = false;
            _scoring.ProcessMiss(holdNote);
            _ = _audio.PlaySfxAsync("miss", 0.6);
        }
        else
        {
            holdNote.IsHit = true;
            var result = _scoring.ProcessHit(holdNote, currentTime);
            _ = PlayHitSound(lane, result.Judgment);
            _effectRenderer.TriggerHitEffect(lane, result.Judgment);
        }

        StateChanged?.Invoke();
    }

    private async Task PlayHitSound(Lane lane, HitJudgment judgment)
    {
        int laneIndex = (int)lane;
        string quality = judgment switch
        {
            HitJudgment.Perfect => "perfect",
            _ => "good"
        };
        await _audio.PlaySfxAsync($"hit_{laneIndex}_{quality}", 0.6);
    }

    private void OnSpecialKey(string key)
    {
        switch (key)
        {
            case "pause":
            case "escape":
                if (_state.CanPause) _ = PauseGame();
                else if (_state.CanResume) _ = ResumeGame();
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

    private void OnComboChanged(int combo) => StateChanged?.Invoke();

    private void OnComboBreak() => _effectRenderer.ClearFlames();

    public async Task StartSongAsync(string chartFile, Difficulty difficulty)
    {
        _state.SelectedChartFile = chartFile;
        _state.SelectedDifficulty = difficulty;
        _projection.LaneCount = LaneCount;

        _currentChart = await _charts.LoadChartAsync(chartFile);
        if (_currentChart == null) return;

        await _audio.LoadSongAsync(_currentChart.Meta.AudioFile);
        await _audio.LoadSongSoundsAsync(_currentChart.NoteSounds);
        await _audio.EnsureAudioResumedAsync();

        _chartNotes = _charts.GetNotesForDifficulty(_currentChart, difficulty);
        _activeNotes.Clear();
        _nextNoteIndex = 0;
        _songPosition = 0;
        _songEnded = false;

        if (_chartNotes.Count > 0)
        {
            var lastNote = _chartNotes[^1];
            _songEndTime = lastNote.Time + lastNote.Duration + 500;
        }
        else
        {
            _songEndTime = 5000;
        }

        _scoring.Reset();
        _effectRenderer.Clear();

        _countdownTime = COUNTDOWN_DURATION;
        _state.TransitionTo(GameStateType.Countdown);
        StateChanged?.Invoke();
    }

    public async Task PauseGame()
    {
        if (_state.Current != GameStateType.Playing) return;

        await _audio.StopAllHoldSustainsAsync();
        _effectRenderer.ClearAllHolds();
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
        _effectRenderer.ClearAllHolds();
        await _audio.StopSongAsync();
        await _audio.ClearSongSoundsAsync();
        _activeNotes.Clear();
        _chartNotes.Clear();
        _effectRenderer.Clear();
        _state.TransitionTo(GameStateType.SongSelect);
        StateChanged?.Invoke();
    }

    private async Task EndSong()
    {
        if (_songEnded) return;
        _songEnded = true;

        await StopGameLoopAsync();
        await _audio.StopAllHoldSustainsAsync();
        _effectRenderer.ClearAllHolds();
        await _audio.StopSongAsync();
        LastStats = _scoring.GetFinalStats(_chartNotes.Count);

        _state.TransitionTo(GameStateType.Results);
        StateChanged?.Invoke();
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

        if (_gameLoopModule != null)
        {
            try { await _gameLoopModule.DisposeAsync(); }
            catch { }
        }

        _gameLoopModule = null;
        _isModuleInitialized = false;
    }
}
