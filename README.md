# Adam and woman in the garden of Eden

A browser role-playing game for readers aged about 11 to 14. It tells Genesis 1 to 3 in the King James Version: the
player is Adam or the woman, watches the days of creation, lives in the garden of Eden and is tempted. The design,
with every decision, is [docs/design.md](docs/design.md).

**State: slice S1, the Scripture pipeline and the reader.** The title page says "Coming soon" and links to a reader
that shows the 80 verses of Genesis 1 to 3, each on a Scripture card with its reference. The verses are parsed at
start from the canonical file, which the build embeds byte for byte; hard words are underlined and open a short
definition outside the card. The game-written text is listed for review in [content/README.md](content/README.md).
The slices that follow are in section 10 of the design.

## What is here

| Path | What |
|---|---|
| `src/AdamEve.Core` | The story machine and the world rules. No package reference, no I/O |
| `src/AdamEve.Content` | The canonical text and the loaders of the content. References Core only |
| `src/AdamEve.Client` | Blazor WebAssembly, standalone. Its published `wwwroot` is the whole deployable |
| `tests/AdamEve.UnitTests` | NUnit and Shouldly |
| `tests/AdamEve.IntegrationTests` | The published site, served by the Static Web Apps CLI emulator |
| `tests/AdamEve.AcceptanceTests` | Playwright: desktop Chromium, Pixel 7 (Chromium), iPhone 13 (WebKit), headless |
| `content/kjv-genesis-1-3.txt` | Genesis 1 to 3, King James Version: the only source of Scripture, byte for byte (see [NOTICE](NOTICE)) |
| `content/glossary.json`, `content/README.md` | The glossary (game text), and the list of all game-written text for review |
| `deploy/` | `deploy.ps1`, `verify.ps1`, `main.bicep`: what the game runs on, and how an environment gets a version |
| `PrivateBuild.ps1`, `build.ps1`, `BuildFunctions.ps1` | The build |

Dependencies point inward: Client, then Content, then Core. There is no server and no database.

## The private build

```
pwsh ./PrivateBuild.ps1
```

One command, at a desk and in the integration build (`.github/workflows/build.yml` runs it with `-CI` and nothing
else). It needs the .NET 10 SDK, PowerShell 7.4 or later with the module PSScriptAnalyzer, and Node.js 20 or later.

| Step | What it does |
|---|---|
| Init | Checks the tools, empties `build/` and `TestResults/`, restores the packages, `npm ci` |
| Analyze | PSScriptAnalyzer over every script; a warning is an error |
| Compile | `dotnet build` in Release; a warning is an error |
| UnitTests | `tests/AdamEve.UnitTests` |
| Publish | `dotnet publish` of the client to `build/publish` |
| PayloadBudget | The first load, as brotli, is 3.0 MB or less |
| StaticFiles | The health files in `_health/`; every file served as immutable has a fingerprint in its name |
| IntegrationTests | The published site in the emulator: the health paths, their headers |
| AcceptanceTests | Playwright against the same emulator, on the three device profiles |
| BuildFacts | `build-facts.json`; `healthcheck.txt` becomes `Healthy`; `files.json` lists every file |
| DeployPackage | `build/deploy-package`: the two scripts, `main.bicep`, `site.zip`, `version.txt` |

`healthcheck.txt` says `Pending` until every test has passed against the published site. A build that stops early
cannot ship a site that says it is healthy.

## Hosting and delivery

Azure Static Web Apps, Free plan, one site for each environment: `swa-adameve-tdd-web` and `swa-adameve-prod-web`.
The game is the deployable `web` of the system `adameve` of the Bible fleet (hosting `own` of the demo-environment
kit): the system's release workflow makes an Octopus package from the artifact `deploy-package`, and its deployment
process runs `deploy.ps1`, then `verify.ps1`, in each environment.

| Path | Answer |
|---|---|
| `/_healthcheck` | `Healthy`: a file the build wrote after every test passed. It proves that the site serves this deployment, not that the game starts in a browser |
| `/alive` | `alive` |
| `/_version` | `{"version":"1.0.42"}` |
| `/_build` | The build facts: commit, build run, lines of code, tests, coverage, who ran the build |

All four allow every origin and are never cached. `verify.ps1` also fetches every file `/_health/files.json` lists
and compares size and SHA-256, and measures the first load as Azure sends it.

**The payload budget.** The empty shell already uses about 2.3 MB of the 3.0 MB: the .NET runtime, its libraries
and one ICU data file, as brotli. That leaves about 0.7 MB for the game's first load.

**The content security policy.** `staticwebapp.config.json` allows scripts from the site itself only. .NET 10
writes an import map into `index.html` when it publishes (the fingerprinted names of the runtime's scripts), which is
an inline script. The build adds the SHA-256 of that one import map to `script-src` of the published
configuration; no other inline script runs.

**WebKit at a desk.** Playwright's WebKit runs on Ubuntu and Debian (and macOS and Windows). On another Linux the
iPhone 13 test cannot start its browser and the private build fails there; the integration build runs all three.

**Compression by Azure.** Not measured yet: the game has not been deployed (section 10 of the design, slice S0).
`verify.ps1` prints how many answers the service compressed.

**Reverting.** A package deploys its own version only. Putting an earlier version back needs the archive of
released sites, slice S3.

## Licence

MIT for the code and for what the project itself made: [LICENSE](LICENSE). The Scripture text is not the
project's: [NOTICE](NOTICE).
