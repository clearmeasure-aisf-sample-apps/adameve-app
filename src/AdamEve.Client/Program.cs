using AdamEve.Client;
using AdamEve.Content;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// The start-up content check: the embedded canonical text is parsed and checked once, before any page shows.
builder.Services.AddSingleton(GameContent.LoadEmbedded());

await builder.Build().RunAsync();
