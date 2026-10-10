using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace AdamEve.AcceptanceTests;

/// <summary>
/// A measurement, not a test of the game: the app is started again and again on WebKit and every start is timed. A
/// start that does not show the title within 30 seconds is described (console, requests, what the page did) and
/// then watched for another minute. Temporary: it is removed once the cause of the slow starts is known.
/// </summary>
[TestFixture]
public class StartStressTests : PlaywrightTest
{
    private const string Timeline =
        """
        (() => {
            const t = window.__t = [];
            const note = text => t.push(Math.round(performance.now()) + ' ' + text);
            const short = url => String(url).split('/').pop().slice(0, 48);
            const wrap = (owner, name, label) => {
                const original = owner[name];
                if (typeof original !== 'function') { return; }
                owner[name] = function (...args) {
                    const what = label(args, this);
                    note('> ' + what);
                    let result;
                    try { result = original.apply(this, args); } catch (error) { note('! ' + what + ' ' + error); throw error; }
                    if (result && typeof result.then === 'function') {
                        result.then(() => note('< ' + what), error => note('x ' + what + ' ' + error));
                    }
                    return result;
                };
            };
            wrap(window, 'fetch', args => 'fetch ' + short(args[0] && args[0].url ? args[0].url : args[0]));
            for (const name of ['instantiate', 'instantiateStreaming', 'compile', 'compileStreaming']) {
                wrap(WebAssembly, name, () => 'WebAssembly.' + name);
            }
            for (const name of ['arrayBuffer', 'json', 'text', 'blob']) {
                wrap(Response.prototype, name, (args, response) => name + ' ' + short(response.url));
            }
            for (const type of ['error', 'unhandledrejection', 'DOMContentLoaded', 'load']) {
                window.addEventListener(type, event => note('event ' + type + ' ' + (event.message || event.reason || '')));
            }
            setInterval(() => note('tick'), 2000);
        })();
        """;

    private sealed class Watch
    {
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly object gate = new();
        private readonly List<string> lines = [];
        private readonly Dictionary<IRequest, string> open = [];

        public Watch(IPage page)
        {
            page.Console += (_, message) => Add($"console {message.Type}: {message.Text}");
            page.PageError += (_, error) => Add($"pageerror: {error}");
            page.Crash += (_, _) => Add("CRASH of the page");
            page.Request += (_, request) =>
            {
                lock (gate) { open[request] = $"{clock.ElapsedMilliseconds} {request.Url}"; }
            };
            page.RequestFinished += (_, request) =>
            {
                lock (gate) { open.Remove(request); }
            };
            page.RequestFailed += (_, request) =>
            {
                lock (gate) { open.Remove(request); }
                Add($"request failed: {request.Url} {request.Failure}");
            };
        }

        public int Requests { get; private set; }

        public void Add(string line)
        {
            lock (gate) { lines.Add($"{clock.ElapsedMilliseconds} {line}"); }
        }

        public string Describe()
        {
            lock (gate)
            {
                return $"  seen by the test:{Environment.NewLine}    {string.Join(Environment.NewLine + "    ", lines)}{Environment.NewLine}  requests not finished: {(open.Count == 0 ? "none" : string.Join("; ", open.Values))}";
            }
        }
    }

    private static readonly StringBuilder Report = new();

    private static void Say(string line)
    {
        Report.AppendLine(line);
        TestContext.Progress.WriteLine(line);
    }

    private static async Task<string> AskAsync(IPage page, string script)
    {
        var asked = page.EvaluateAsync<string>(script);
        var first = await Task.WhenAny(asked, Task.Delay(5000));
        if (first != asked)
        {
            return "NO ANSWER in 5 s: the page does not run script";
        }

        try
        {
            return await asked;
        }
        catch (PlaywrightException exception)
        {
            return "evaluate failed: " + exception.Message;
        }
    }

    private static async Task<bool> ShownAsync(ILocator locator, string? attribute, int milliseconds)
    {
        try
        {
            if (attribute is null)
            {
                await Assertions.Expect(locator).ToBeVisibleAsync(new() { Timeout = milliseconds });
            }
            else
            {
                await Assertions.Expect(locator).ToHaveAttributeAsync(attribute, "true", new() { Timeout = milliseconds });
            }

            return true;
        }
        catch (PlaywrightException)
        {
            return false;
        }
    }

    /// <summary>One start of the app: the time until it shows, and for a start that stalls what was seen.</summary>
    private static async Task<(double Milliseconds, bool Stalled)> StartAsync(IPage page, Watch watch, string label, Func<Task> navigate, ILocator shown, string? attribute)
    {
        var clock = Stopwatch.StartNew();
        watch.Add($"-- {label}");
        try
        {
            await navigate();
        }
        catch (PlaywrightException exception)
        {
            watch.Add("navigation threw: " + exception.Message);
        }

        if (await ShownAsync(shown, attribute, 30_000))
        {
            return (clock.Elapsed.TotalMilliseconds, false);
        }

        Say($"STALL {label}: not shown after {clock.Elapsed.TotalSeconds:F1} s");
        Say(watch.Describe());
        Say("  the page: " + await AskAsync(page, "() => JSON.stringify({ now: Math.round(performance.now()), ready: document.readyState, app: (document.getElementById('app') || {}).innerHTML?.slice(0, 200), error: getComputedStyle(document.getElementById('blazor-error-ui')).display, resources: performance.getEntriesByType('resource').length, last: performance.getEntriesByType('resource').slice(-6).map(entry => entry.name.split('/').pop() + ':' + Math.round(entry.responseEnd)).join(' '), runtime: typeof globalThis.getDotnetRuntime, blazor: typeof globalThis.Blazor })"));
        Say("  its timeline: " + await AskAsync(page, "() => (window.__t || ['no timeline in this start']).join(' | ')"));
        var later = await ShownAsync(shown, attribute, 60_000);
        Say($"  after {clock.Elapsed.TotalSeconds:F1} s: {(later ? "it is shown, the start was slow" : "still not shown, the start hangs")}");
        if (!later)
        {
            Say("  its timeline then: " + await AskAsync(page, "() => (window.__t || ['no timeline in this start']).slice(-12).join(' | ')"));
            var again = Stopwatch.StartNew();
            try
            {
                await page.ReloadAsync(new() { Timeout = 20_000 });
                Say($"  a reload of the same page: {(await ShownAsync(shown, attribute, 30_000) ? "shown" : "NOT shown")} after {again.Elapsed.TotalSeconds:F1} s");
            }
            catch (PlaywrightException exception)
            {
                Say("  a reload of the same page threw: " + exception.Message);
            }
        }

        return (clock.Elapsed.TotalMilliseconds, true);
    }

    private static string Spread(List<double> times)
    {
        if (times.Count == 0)
        {
            return "none";
        }

        var sorted = times.Order().ToList();
        double At(double share) => sorted[Math.Min(sorted.Count - 1, (int)(share * sorted.Count))];
        return string.Create(CultureInfo.InvariantCulture, $"n={sorted.Count} min={sorted[0]:F0} median={At(0.5):F0} p90={At(0.9):F0} p99={At(0.99):F0} max={sorted[^1]:F0} ms");
    }

    [TestCase("iPhone 13", "webkit", 31)]
    public async Task Start_TheAppAgainAndAgain_ShouldSayHowLongEachStartTakes(string device, string engine, int minutes)
    {
        string[] kinds = ["guarded", "plain", "guarded with timeline", "plain with timeline"];
        var times = kinds.ToDictionary(kind => kind, _ => new List<double>());
        var stalled = kinds.ToDictionary(kind => kind, _ => 0);
        var whole = Stopwatch.StartNew();
        Say($"START STRESS {device} {engine}: {Environment.ProcessorCount} processors");

        for (var round = 0; whole.Elapsed < TimeSpan.FromMinutes(minutes); round++)
        {
            // Four kinds of round: a page as the tests open it (GuardedPage: every request is routed through the
            // test) or a plain one, each with and without the timeline script.
            var kind = kinds[round % 4];
            var guarded = kind.StartsWith("guarded", StringComparison.Ordinal) ? await GuardedPage.OpenAsync(Playwright, device, engine) : null;
            var browser = guarded is null ? await Playwright[engine].LaunchAsync() : null;
            var context = guarded?.Page.Context ?? await browser!.NewContextAsync(Playwright.Devices[device]);
            if (kind.EndsWith("timeline", StringComparison.Ordinal))
            {
                await context.AddInitScriptAsync(Timeline);
            }

            var page = guarded?.Page ?? await context.NewPageAsync();
            var watch = new Watch(page);
            var label = $"round {round} ({kind})";
            var title = page.GetByTestId("choose-Adam");
            var game = page.GetByTestId("game");

            (double Milliseconds, bool Stalled)[] starts =
            [
                await StartAsync(page, watch, label + " cold /", () => page.GotoAsync(Site.BaseAddress), title, null),
                await StartAsync(page, watch, label + " reload /", () => page.ReloadAsync(), title, null),
                await StartAsync(page, watch, label + " goto /garden", () => page.GotoAsync(Site.BaseAddress + "garden"), game, "data-ready"),
                await StartAsync(page, watch, label + " reload /garden", () => page.ReloadAsync(), game, "data-ready"),
            ];
            times[kind].AddRange(starts.Select(start => start.Milliseconds));
            stalled[kind] += starts.Count(start => start.Stalled);
            if (starts.Any(start => start.Stalled) && guarded is not null)
            {
                Say("  errors the guarded page kept: " + string.Join(" || ", guarded.Errors));
            }

            if (guarded is not null)
            {
                await guarded.DisposeAsync();
            }
            else
            {
                await context.DisposeAsync();
                await browser!.DisposeAsync();
            }
        }

        Say($"RESULT {device} {engine} in {whole.Elapsed.TotalSeconds:F0} s");
        foreach (var kind in kinds)
        {
            Say($"  {kind}: {stalled[kind]} stalled of {times[kind].Count} starts; {Spread(times[kind])}");
        }

        var path = GardenView.KeptPath("gallery", $"start-stress-{engine}.txt");
        await File.WriteAllTextAsync(path, Report.ToString());
        TestContext.AddTestAttachment(path);
    }
}
