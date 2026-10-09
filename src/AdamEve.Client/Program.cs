using AdamEve.Client;
using AdamEve.Client.Game;
using AdamEve.Content;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

// The client runs in a browser and nowhere else: the canvas, the sound and localStorage are the browser's.
[assembly: System.Runtime.Versioning.SupportedOSPlatform("browser")]

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// The start-up content check: the embedded canonical text is parsed and checked once, before any page shows.
builder.Services.AddSingleton(GameContent.LoadEmbedded());

// The garden: the game that is playing in this tab.
builder.Services.AddSingleton<GameSession>();

await builder.Build().RunAsync();
