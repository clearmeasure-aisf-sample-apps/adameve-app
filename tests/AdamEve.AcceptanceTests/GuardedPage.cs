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

    public static async Task<GuardedPage> OpenAsync(IPlaywright playwright, string device, string engine)
    {
        var browser = await playwright[engine].LaunchAsync();
        var context = await browser.NewContextAsync(playwright.Devices[device]);
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

    public async ValueTask DisposeAsync()
    {
        await context.DisposeAsync();
        await browser.DisposeAsync();
    }
}
