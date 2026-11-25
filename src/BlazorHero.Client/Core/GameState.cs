namespace BlazorHero.Client.Core;

public enum GameStateType
{
    Loading,
    MainMenu,
    SongSelect,
    DifficultySelect,
    Countdown,
    Playing,
    Paused,
    Results
}

public class GameState
{
    public GameStateType Current { get; private set; } = GameStateType.Loading;
    public GameStateType Previous { get; private set; }

    public event Action<GameStateType, GameStateType>? StateChanged;

    // Selected song and difficulty for current session
    public string? SelectedChartFile { get; set; }
    public Difficulty SelectedDifficulty { get; set; } = Difficulty.Medium;

    public void TransitionTo(GameStateType newState)
    {
        if (Current == newState) return;

        Previous = Current;
        Current = newState;
        StateChanged?.Invoke(Previous, Current);
    }

    public bool CanPause => Current == GameStateType.Playing;
    public bool CanResume => Current == GameStateType.Paused;
    public bool IsInGame => Current == GameStateType.Playing || Current == GameStateType.Paused || Current == GameStateType.Countdown;

    public void Reset()
    {
        SelectedChartFile = null;
        SelectedDifficulty = Difficulty.Medium;
    }
}

public enum Difficulty
{
    Easy,
    Medium,
    Hard,
    Expert
}
