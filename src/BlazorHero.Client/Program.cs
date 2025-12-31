using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using BlazorHero.Client;
using BlazorHero.Client.Core;
using BlazorHero.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// HTTP Client - Singleton for Blazor WASM (single user context)
builder.Services.AddSingleton(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Game services - Singleton to persist across component navigations
builder.Services.AddSingleton<GameState>();
builder.Services.AddSingleton<AudioService>();
builder.Services.AddSingleton<InputService>();
builder.Services.AddSingleton<ScoringService>();
builder.Services.AddSingleton<ChartService>();  // Must be Singleton since GameEngine depends on it
builder.Services.AddSingleton<GameEngine>();
builder.Services.AddSingleton<SkiaGameEngine>(); // Skia-based rendering engine
builder.Services.AddSingleton<DeviceService>(); // Device detection for mobile support

await builder.Build().RunAsync();
