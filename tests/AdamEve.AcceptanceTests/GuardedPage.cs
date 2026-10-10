using Microsoft.Playwright;

namespace AdamEve.AcceptanceTests;

/// <summary>
/// A page on one device profile that keeps what a test must never see: an error of the browser, and a request that
/// leaves the origin of the site (it is refused and recorded).
/// </summary>
internal sealed class GuardedPage : IAsyncDisposable
{
    private readonly IBrowser browser;
    private readonly IBrowserContext context;

    private GuardedPage(IBrowser browser, IBrowserContext context, IPage page)
    {
        this.browser = browser;
        this.context = context;
        Page = page;
    }

    public IPage Page { get; }

    public List<string> Errors { get; } = [];

    public List<string> RequestsOutsideTheOrigin { get; } = [];

    // A headless Chromium has WebGL only with one of these, by the machine it runs on. The first that gives a
    // WebGL 2 context is used for every test, and the tests of the renderer name it and the renderer behind it.
    private static readonly string[][] ChromiumArguments =
    [
        [],
        ["--enable-unsafe-swiftshader"],
        ["--use-angle=swiftshader", "--enable-unsafe-swiftshader"],
        ["--use-angle=gl-egl", "--enable-gpu", "--ignore-gpu-blocklist"],
    ];

    private static readonly SemaphoreSlim Probing = new(1, 1);
    private static string[]? chromiumArguments;

    /// <summary>What draws WebGL in the Chromium of the tests, as the browser names it.</summary>
    public static string WebGlOfChromium { get; private set; } = "unknown";

    /// <summary>
    /// Opens a page on a device profile. The garden is drawn with WebGL (design, section 7.1), so the browser is
    /// started in a way that has it.
    /// </summary>
    public static async Task<GuardedPage> OpenAsync(IPlaywright playwright, string device, string engine, bool reducedMotion = false)
    {
        var browser = await playwright[engine].LaunchAsync(new() { Args = await ArgumentsAsync(playwright, engine) });
        // The player's setting "reduce motion", as the browser reports it to the page (prefers-reduced-motion).
        var options = new BrowserNewContextOptions(playwright.Devices[device]) { ReducedMotion = reducedMotion ? ReducedMotion.Reduce : ReducedMotion.NoPreference };
        var context = await browser.NewContextAsync(options);
        var page = await context.NewPageAsync();
        var guarded = new GuardedPage(browser, context, page);
        var origin = new Uri(Site.BaseAddress).GetLeftPart(UriPartial.Authority);

        await context.RouteAsync("**/*", async route =>
        {
            var url = route.Request.Url;
            if (url.StartsWith(origin + "/", StringComparison.Ordinal) || url == origin)
            {
                await route.ContinueAsync();
                return;
            }

            guarded.RequestsOutsideTheOrigin.Add(url);
            await route.AbortAsync();
        });
        page.Console += (_, message) =>
        {
            if (message.Type == "error")
            {
                guarded.Errors.Add(message.Text);
            }
        };
        page.PageError += (_, error) => guarded.Errors.Add(error);
        return guarded;
    }

    /// <summary>The arguments a browser of an engine needs on this machine to have WebGL 2.</summary>
    private static async Task<string[]> ArgumentsAsync(IPlaywright playwright, string engine)
    {
        if (engine != "chromium")
        {
            return [];
        }

        await Probing.WaitAsync();
        try
        {
            if (chromiumArguments is null)
            {
                foreach (var candidate in ChromiumArguments)
                {
                    await using var browser = await playwright.Chromium.LaunchAsync(new() { Args = candidate });
                    var page = await browser.NewPageAsync();
                    var renderer = await page.EvaluateAsync<string?>(
                        """
                        () => {
                            const context = document.createElement('canvas').getContext('webgl2');
                            if (!context) { return null; }
                            const names = context.getExtension('WEBGL_debug_renderer_info');
                            return names ? context.getParameter(names.UNMASKED_RENDERER_WEBGL) : 'not named';
                        }
                        """);
                    if (renderer is not null)
                    {
                        chromiumArguments = candidate;
                        WebGlOfChromium = renderer;
                        break;
                    }
                }
            }
        }
        finally
        {
            Probing.Release();
        }

        return chromiumArguments
            ?? throw new InvalidOperationException("No headless Chromium here has WebGL 2 with any of the arguments tried: the renderer of the garden cannot be tested on this machine.");
    }

    public async ValueTask DisposeAsync()
    {
        await context.DisposeAsync();
        await browser.DisposeAsync();
    }
}
