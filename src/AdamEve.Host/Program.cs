using AdamEve.Host;
using Microsoft.Net.Http.Headers;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);
builder.Services.AddHealthChecks().AddCheck<SiteHealthCheck>("site");

var app = builder.Build();

// The web root is the published client. The policy names the import map of its index.html by hash.
var webRoot = app.Environment.WebRootFileProvider;
string? indexHtml = null;
if (webRoot.GetFileInfo("index.html") is { Exists: true } indexFile)
{
    using var reader = new StreamReader(indexFile.CreateReadStream());
    indexHtml = await reader.ReadToEndAsync();
}

var contentSecurityPolicy = ContentSecurityPolicy.For(indexHtml);

// The headers of every answer, and the page a navigation gets.
app.Use(async (context, next) =>
{
    var asked = context.Request.Path.Value ?? "/";
    context.Response.OnStarting(() =>
    {
        var headers = context.Response.Headers;
        headers.ContentSecurityPolicy = contentSecurityPolicy;
        headers.XContentTypeOptions = "nosniff";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
        if (SitePaths.IsOpenToEveryOrigin(asked))
        {
            headers.AccessControlAllowOrigin = "*";
        }

        headers.CacheControl = SitePaths.CacheControl(asked, context.Response.StatusCode);
        headers.Remove(HeaderNames.Pragma);
        headers.Remove(HeaderNames.Expires);

        return Task.CompletedTask;
    });

    if (IsRead(context.Request) && SitePaths.IsNavigation(asked))
    {
        context.Request.Path = SitePaths.Index;
    }

    await next(context);
});

// The files of the site. Where the publish step wrote a brotli or gzip copy and the browser accepts it, the copy is
// the answer: nothing is compressed while a request waits.
app.UseWhen(
    context => IsRead(context.Request) && !PrecompressedFiles.IsCopy(context.Request.Path.Value ?? string.Empty),
    files =>
    {
        files.Use(async (context, next) =>
        {
            var path = context.Request.Path.Value ?? string.Empty;
            if (webRoot.GetFileInfo(path) is { Exists: true, IsDirectory: false })
            {
                context.Response.Headers.Vary = HeaderNames.AcceptEncoding;
                var copy = PrecompressedFiles.Choose(path, context.Request.Headers.AcceptEncoding, name => webRoot.GetFileInfo(name).Exists);
                if (copy is not null)
                {
                    context.Request.Path = copy;
                }
            }

            await next(context);
        });
        files.UseStaticFiles(new StaticFileOptions
        {
            ContentTypeProvider = new PrecompressedFiles.ContentTypes(),
            OnPrepareResponse = prepared =>
            {
                if (PrecompressedFiles.CodingOf(prepared.File.Name) is { } coding)
                {
                    prepared.Context.Response.Headers.ContentEncoding = coding;
                }
            },
        });
    });

app.UseRouting();

app.MapHealthChecks(SitePaths.Health);
app.MapGet(SitePaths.Alive, () => Results.Text("alive"));
app.MapGet(SitePaths.Version, () => Results.Json(new Dictionary<string, string> { ["version"] = BuildInfo.Version }));
app.MapGet(SitePaths.Build, (IWebHostEnvironment environment) =>
    environment.ContentRootFileProvider.GetFileInfo(BuildInfo.FactsFile) is { Exists: true, PhysicalPath: { } facts }
        ? Results.File(facts, "application/json")
        : Results.Json(new Dictionary<string, string?> { ["version"] = BuildInfo.Version, ["commit"] = null, ["builtAt"] = null }));
app.MapGet(SitePaths.Files, (IWebHostEnvironment environment) =>
    environment.ContentRootFileProvider.GetFileInfo(BuildInfo.FilesFile) is { Exists: true, PhysicalPath: { } files }
        ? Results.File(files, "application/json")
        : Results.NotFound());

// Whatever names nothing: 404, with the page that says so.
app.MapFallback("{*path}", async context =>
{
    context.Response.StatusCode = StatusCodes.Status404NotFound;
    if (webRoot.GetFileInfo("404.html") is { Exists: true } notFound && !HttpMethods.IsHead(context.Request.Method))
    {
        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.SendFileAsync(notFound);
    }
});

await app.RunAsync();

static bool IsRead(HttpRequest request) => HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method);
