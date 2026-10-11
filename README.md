# Adam and woman in the garden of Eden

A browser role-playing game for readers aged about 11 to 14. It tells Genesis 1 to 3 in the King James Version: the
player is Adam or the woman, watches the days of creation, lives in the garden of Eden and is tempted. The design,
with every decision, is [docs/design.md](docs/design.md).

**State: slice S4, "Title, character select, creation intro".** The title is the title screen of the design
(beat B0): the player chooses Adam or the woman, the size of the text and the sound, and can always open the reader,
which shows the 80 verses of Genesis 1 to 3, each on a Scripture card with its reference. A new game plays the seven
days of creation (beats B1 to B7): Genesis 1:1 to 2:3, one verse on each card, in order. The player turns the page
and, once in each of the first six days, makes one gesture that reveals what the next verse tells; the player
reveals what God made and makes nothing. The seventh day has no gesture. After a first completion the days can be
skipped. The story machine is `AdamEve.Core.Story.StoryMachine`. After the seventh day the player walks the chosen
character through the Central Glade, the Spring of Eden and the Pison Meadows (slice S2) with the arrow keys, WASD,
an on-screen D-pad or a tap on a tile; the game is saved in the browser and resumed. The story after Genesis 2:3 is
not built yet. The game-written text is listed for review in [content/README.md](content/README.md). The slices
that follow are in section 10 of the design.

**Art made by code (the art pass of 2026-10-10).** Everything the game draws is made by code when it is drawn:
solids from a few primitives with a colour for each corner, gradients, three small patterns computed into
textures, and instances. No image, model, sound or font file was generated, downloaded or added, no shader was
taken from anywhere, and the sound setting has nothing to play yet.

- *The garden.* What stands where is a fact of the world, in `AdamEve.Core.World.GardenScenery`, from the map and
  one fixed seed: four kinds of tree on the tree tiles (broad, tall, fruit-bearing, palm), flowering shrubs and
  mossy rocks at the edge of the thicket, forest behind it, reeds and rocks at the river's edge, and the flowers in
  drifts of one colour. It changes no rule: only what nobody can walk on has height, the ground a character walks
  on carries grass and flowers and nothing else, and the crossings stay clear. The Three.js renderer builds the
  look: ground that shades smoothly with no tile edge, a river with a sloping shore, slow crests, glints and rings
  about a character on the stones, a low warm sun with long soft shadows and cool shade, faint shafts of light
  beside the crowns, mist over the spring, pollen, fireflies in the shade, butterflies over the flowers, birds and
  slow clouds before three ridges of hills that deepen toward blue. The two trees in the midst of the garden
  (Genesis 2:9) stand where the map puts them and are unlike: the tree of life tall, white-gold and green-gold
  with blossoms that glow in a calm warm light; the tree of the knowledge of good and evil low, wide and dark,
  silver-barked, its leaves turning a silver side, in a cool light that is never quite still. Neither is
  interactive, and no fruit is drawn on the second. Everything that moves by itself moves with one clock, which
  stands under `prefers-reduced-motion`. The canvas fallback draws the same garden plainly and flat.
- *The days of creation* are drawn in the same manner, as shapes of the page (`CreationPicture`, `CreationArt`):
  one composition for all seven days, each layer showing what its verses tell and nothing else.
- *The figures* of Adam and the woman are abstract cut-out rigs: the same seven plain blocks each, now with
  rounded corners, never anatomical, and for the woman long hair that covers her chest (design, sections 1 and
  5.3, decision D18). They carry no foliage. Walking has weight (the body sinks and rises, the legs take it up)
  and the hair moves.

God is never drawn as a figure: his presence is a warm light, shown while his voice speaks on a Scripture card.
Pictures of all of it are kept with every build for review: artifact `test-results`, `acceptance/gallery/`.

**The garden in perspective (decision D18, 2026-10-10).** Three.js (WebGL 2) draws the garden through a perspective
camera that follows the player, fixed in direction and tilt: nearer things are larger and cross the screen faster,
a sun casts soft shadows, haze grows with distance, and behind the haze a far layer of hills and sky slides less
than the ground. The camera is one model in `AdamEve.Core` (`PerspectiveCamera`): the game culls with it, reads a
tap through it and judges each figure at the scale it gives, and the renderer builds its own camera from the same
numbers. A character is a flat cut-out that faces the camera, never a body in space. Where WebGL 2 is not to be had,
where its context is lost and does not come back, or with `…/garden?renderer=canvas`, the canvas 2D renderer draws
instead, flat; the game root says so (`data-renderer`, `data-renderer-fallback`). The title, the days of creation,
the reader and all text and menus are flat pages. `docs/spike-threejs.md` has the history.

**Modesty, rule M1, as decision D18 amended it** (confirmed by Jeffrey on 2026-10-10 ("All good", then "Do it" to promoting it); design, sections 1, 5.3 and 5.6). The figures are never anatomical: the pelvic region of both is
a smooth, featureless continuation of the body shape, and the woman's chest is a plain torso shape covered by her
long hair whenever it faces the viewer. Three checks hold it. `RigStructure` lists what a rig may hold (plain
shapes, the seven blocks of the body, hair, the eyes, the apron and the coat, each in a colour of its kind): a rig
with anything else does not load. `ConcealmentChecker` judges every frame: the hips cover the pelvic zone and
nothing else reaches into it, and hair covers every pixel of the woman's chest zone that is not turned away. Unit
tests run both over every frame of every animation of both rigs, in all eight facings and all three covering
variants; the running game runs them for each character in every frame it draws and writes the verdict on the game
root (`data-concealment`); a figure whose frame fails is not drawn and the frame counts as a failure, which the
full-system tests refuse. A full-system test reads the pixels the renderer drew, for both figures in all eight
facings on the three device profiles, and keeps a sheet of each for review (artifact `test-results`,
`acceptance/**/m1-sheets/`). The character select and the two far figures of light of the sixth day are drawn from
the same rigs and judged by the same checks (`StillFigure`), with the verdict on the root of their page.

## What is here

| Path | What |
|---|---|
| `src/AdamEve.Core` | The story machine (the title and the days of creation) and the world rules: the map, walking, pathfinding, the camera, the rigs and the M1 check, the render list, the save model. No package reference, no I/O |
| `src/AdamEve.Content` | The canonical text, the game-written labels and the loaders of the content (glossary, map, rigs). References Core only |
| `src/AdamEve.Client` | Blazor WebAssembly, standalone: the game, which runs in the browser. `wwwroot/js/render-three.js` draws the garden with Three.js (vendored in `wwwroot/lib/three/`), `wwwroot/js/render.js` is the canvas fallback, `wwwroot/js/shell.js` is what the two share |
| `src/AdamEve.Host` | ASP.NET Core, the outermost project: serves the published client and answers the health paths. No game code. Published, it is the content of the container image |
| `tests/AdamEve.UnitTests` | NUnit and Shouldly |
| `tests/AdamEve.IntegrationTests` | The published site, served by the published host as a process (`ADAMEVE_BASE_URL`) |
| `tests/AdamEve.AcceptanceTests` | Playwright: desktop Chromium, Pixel 7 (Chromium), iPhone 13 (WebKit), headless |
| `content/kjv-genesis-1-3.txt` | Genesis 1 to 3, King James Version: the only source of Scripture, byte for byte (see [NOTICE](NOTICE)) |
| `content/glossary.json`, `content/README.md` | The glossary (game text), and the list of all game-written text for review |
| `content/maps`, `content/rigs` | The map of the garden (Tiled JSON) and the rigs with their concealment zones |
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
| PayloadBudget | The first load (what the title needs), as brotli, is 3.0 MB or less; what the garden loads on demand is added up apart |
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

**The payload budget.** The empty shell used about 2.3 MB of the 3.0 MB: the .NET runtime, its libraries and one
ICU data file, as brotli. With slice S2 (the garden, its two script modules and the JSON serializers of the save)
the build measured 2.37 MB; with slice S4 (the story machine, the title and the days of creation) it measures
2.39 MB, which leaves about 0.6 MB. Since decision D18 the first load is what the title needs: the modules that
draw the garden and Three.js (0.16 MB) are loaded on demand, when a player enters the garden, and are added up
apart; a full-system test proves that the title and the reader ask for none of them.

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
