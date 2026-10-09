# Adam and woman in the garden of Eden

A browser role-playing game for readers aged about 11 to 14. It tells Genesis 1 to 3 in the King James Version: the
player is Adam or the woman, watches the days of creation, lives in the garden of Eden and is tempted. The design,
with every decision, is [docs/design.md](docs/design.md).

**State: slice S0, the empty shell.** The site has one page with the title and "Coming soon". There is no game code
yet. The slices that follow are in section 10 of the design.

## What is here

| Path | What |
|---|---|
| `src/AdamEve.Core` | The story machine and the world rules. No package reference, no I/O |
| `src/AdamEve.Content` | The canonical text and the loaders of the content. References Core only |
| `src/AdamEve.Client` | Blazor WebAssembly, standalone: the game, which runs in the browser |
| `src/AdamEve.Host` | ASP.NET Core, the outermost project: serves the published client and answers the health paths. No game code. Published, it is the content of the container image |
| `tests/AdamEve.UnitTests` | NUnit and Shouldly |
| `tests/AdamEve.IntegrationTests` | The published site, served by the published host as a process (`ADAMEVE_BASE_URL`) |
| `tests/AdamEve.AcceptanceTests` | Playwright: desktop Chromium, Pixel 7 (Chromium), iPhone 13 (WebKit), headless |
| `content/kjv-genesis-1-3.txt` | Genesis 1 to 3, King James Version: the only source of Scripture, byte for byte (see [NOTICE](NOTICE)) |
| `deploy/` | `deploy.ps1`, `verify.ps1`, `settings.json`, `infra/main.bicep`: what the game runs on, and how an environment gets a version |
| `PrivateBuild.ps1`, `build.ps1`, `BuildFunctions.ps1` | The build |

Dependencies point inward: Host, then Client, then Content, then Core, which references nothing. The host runs none
of the client's code: publishing the host publishes the client and takes its `wwwroot`. There is no database, and
no game code runs on a server.

## The private build

```
pwsh ./PrivateBuild.ps1
```

One command, at a desk and in the integration build (`.github/workflows/build.yml` runs it with `-CI` and nothing
else). It needs the .NET 10 SDK and PowerShell 7.4 or later with the module PSScriptAnalyzer. No Node.js and no
Docker.

| Step | What it does |
|---|---|
| Init | Checks the tools, empties `build/` and `TestResults/`, restores the packages |
| Analyze | PSScriptAnalyzer over every script, a warning is an error; `deploy/infra/main.bicep` compiles without a diagnostic (where the Azure CLI is installed) |
| Compile | `dotnet build` in Release; a warning is an error |
| UnitTests | `tests/AdamEve.UnitTests` |
| Publish | `dotnet publish` of the host to `build/publish`, with the published client as its `wwwroot` |
| PayloadBudget | The first load, as brotli, is 3.0 MB or less |
| StaticFiles | Every file served as immutable has a fingerprint in its name; `files.json` lists every file of the site |
| IntegrationTests | The published host as a process: the health paths, the headers, brotli, the fallback, 404, every file |
| AcceptanceTests | Playwright against the same process, on the three device profiles |
| BuildFacts | `build-facts.json`, beside the host |
| ContainerImage | The image `container-image:<version>`, from `build/publish` as tested, in `build/container-image/container-image.tar.gz`. Where a Docker daemon answers, it is loaded and run once |
| DeployPackage | `build/deploy-package`: the `deploy/` folder |

The image is made after the last test, from the very folder the tests asked. A build that stops early leaves no
image to release.

To run the site at a desk after a build: `cd build/publish && dotnet AdamEve.Host.dll --urls http://127.0.0.1:8080`.

## Hosting and delivery

Azure Container Apps express (design, section 7.6 and decision D17 of 2026-10-09). One container app for each
environment, `ca-adameve-tdd-web` and `ca-adameve-prod-web`, in the express environment the system owns:
`cae-adameve` in the resource group `rg-adameve-apps`, Central US. The game is the deployable `web` of the system
`adameve` of the Bible fleet (hosting `own` of the demo-environment kit):

- the Build keeps the image as the artifact `container-image` and the `deploy/` folder as `deploy-package`;
- the system's release workflow pushes that image to the system's registry as `<registry>/adameve/web:<version>`,
  locks the tag, and makes an Octopus package from `deploy-package`;
- the system's deployment process runs `deploy.ps1`, then `verify.ps1`, in each environment, as the tier's deploy
  identity.

**What `deploy.ps1` needs.** From the context file: `system`, `resourceGroup` (the tier's), `registryServer`,
`deployPrincipalId`. In Azure, all of it the system's to provide:

| What | Name | Why |
|---|---|---|
| The express environment | `cae-adameve` in `rg-adameve-apps` (`deploy/settings.json`, `appEnvironment`) | The app runs in it. The script reads it and creates nothing there |
| The deploy identity's rights on it | Read, and `Microsoft.App/managedEnvironments/join/action` | To read its state and region, and to place the app in it |
| The deploy identity's rights on the tier's resource group | Owner | The deployment stack with its deny settings, the container app, and assigning the pull identity to it |
| The pull identity | `id-adameve-<environment>-app` in the tier's resource group, with AcrPull on the registry | The app pulls the image as this identity |
| The image | `<registryServer>/adameve/web:<version>` | The release pushes it before the first deployment of a version |

`deploy.ps1` applies `deploy/infra/main.bicep` as the deployment stack `stack-adameve-<environment>-web`. The stack
denies writing and deleting to everyone but the deploy identity.

| Path | Answer |
|---|---|
| `/_healthcheck` | `Healthy` when the process has the `index.html` and the runtime it is to serve; 503 and `Unhealthy` otherwise. It proves that a process of this version runs and has its files, not that the game starts in a browser |
| `/alive` | `alive`, whenever the process answers |
| `/_version` | `{"version":"1.0.42"}`: the version compiled into the process |
| `/_build` | The build facts: commit, build run, lines of code, tests, coverage, who ran the build |

All four allow every origin and are never cached. `verify.ps1` also fetches every file `/_health/files.json` lists
and compares size and SHA-256, and measures the first load as the app sends it.

**Scale and the cold start.** Both environments run 0 to 1 replica (`deploy/settings.json`). An app nobody asks
stops, and costs nothing while stopped. The next request starts it: that first answer takes some seconds longer.
`verify.ps1` allows for it (it asks `/_version` for up to 5 minutes).

**The payload budget.** The empty shell already uses about 2.3 MB of the 3.0 MB: the .NET runtime, its libraries
and one ICU data file, as brotli. That leaves about 0.7 MB for the game's first load.

**Compression.** The publish step writes a brotli and a gzip file beside every file that compresses. The host
answers with the one the browser accepts and compresses nothing itself, so the first load on the wire is the size
the build measured: 2.26 MB as brotli for the empty shell, measured against the published host with
`Accept-Encoding: br` (42 files and one ICU data file; 44 of 45 answers were brotli, the icon is a PNG). Not yet
measured against a deployed app: the game has not been deployed to Container Apps. `verify.ps1` measures it at
every deployment and fails above 3.0 MB.

**The headers.** The host sends them with every answer: the content security policy, `X-Content-Type-Options:
nosniff`, `Referrer-Policy: no-referrer`, `Permissions-Policy: camera=(), microphone=(), geolocation=()`. Files
under `/_framework/` and `/assets/` have a fingerprint in their names and are kept for a year (`immutable`);
`index.html` and everything else is `no-cache`. A path a person navigates to gets `index.html`; a file that is not
there gets 404.

**The content security policy.** Scripts come from the site itself only. .NET 10 writes an import map into
`index.html` when it publishes (the fingerprinted names of the runtime's scripts), which is an inline script. The
host reads the `index.html` it serves and adds the SHA-256 of that one import map to `script-src`; no other inline
script runs (`src/AdamEve.Host/ContentSecurityPolicy.cs`).

**WebKit at a desk.** Playwright's WebKit runs on Ubuntu and Debian (and macOS and Windows). On another Linux the
iPhone 13 test cannot start its browser and the private build fails there; the integration build runs all three.

**Reverting.** `deploy.ps1` deploys any released version: the image of every release stays in the registry, its tag
locked, and the script names the image by the version it is asked for. The storage archive the design once planned
(slice S3, decision D12) is not needed. A version that was never released has no image; the script then exits 1.

**What express leaves out.** No custom domain (the app keeps its `azurecontainerapps.io` address), no HTTP/2, and
tdd and prod share one runtime.

## Licence

MIT for the code and for what the project itself made: [LICENSE](LICENSE). The Scripture text is not the
project's: [NOTICE](NOTICE).
