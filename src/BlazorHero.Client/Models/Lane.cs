namespace BlazorHero.Client.Models;

public enum Lane
{
    Green = 0,  // D key
    Red = 1,    // F key
    Yellow = 2, // J key
    Blue = 3,   // K key
    Orange = 4  // L key (Expert only)
}

public static class LaneExtensions
{
    public static string GetColor(this Lane lane) => lane switch
    {
        Lane.Green => "#22C55E",
        Lane.Red => "#EF4444",
        Lane.Yellow => "#EAB308",
        Lane.Blue => "#3B82F6",
        Lane.Orange => "#F97316",
        _ => "#FFFFFF"
    };

    public static string GetGlowColor(this Lane lane) => lane switch
    {
        Lane.Green => "rgba(34, 197, 94, 0.5)",
        Lane.Red => "rgba(239, 68, 68, 0.5)",
        Lane.Yellow => "rgba(234, 179, 8, 0.5)",
        Lane.Blue => "rgba(59, 130, 246, 0.5)",
        Lane.Orange => "rgba(249, 115, 22, 0.5)",
        _ => "rgba(255, 255, 255, 0.5)"
    };

    public static string GetKeyName(this Lane lane) => lane switch
    {
        Lane.Green => "D",
        Lane.Red => "F",
        Lane.Yellow => "J",
        Lane.Blue => "K",
        Lane.Orange => "L",
        _ => "?"
    };
}
