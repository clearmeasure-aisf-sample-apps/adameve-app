# CLAUDE.md

Guidance for Claude Code sessions in this repository. The design is `docs/design.md`: every decision in it is
Jeffrey Palermo's. Read the section a change touches before changing anything.

## What this is

"Adam and woman in the garden of Eden": a browser game that tells Genesis 1 to 3 in the King James Version. Blazor
WebAssembly, .NET 10, standalone. A small ASP.NET Core host (`src/AdamEve.Host`) serves the published client from a
container image on Azure Container Apps express: one container app for each environment, in the environment the
system owns (design, section 7.6, decision D17). No game code on a server, no database, no account, no request to
any other origin. Solution: `AdamEve.slnx`.

## Build and test

```
pwsh ./PrivateBuild.ps1                      # the private build: everything, as the integration build runs it
. ./build.ps1 ; Init ; Compile ; UnitTests   # steps, one after another
```

The integration and acceptance tests ask a running site: `PrivateBuild.ps1` publishes the host with the client as
its web root and starts it as a process (`ADAMEVE_BASE_URL`). No test needs Docker or Node.js. The image is built by
the .NET SDK in the step `ContainerImage`, from the folder the tests asked, after the last test.
`.github/workflows/build.yml` runs `PrivateBuild.ps1 -CI` and nothing else. Keep it that way: no second definition
of the build in the workflow file.

What the system's release relies on (do not change without the system's owner): the workflow's name `Build`,
`MAJOR_VERSION` and `MINOR_VERSION`, the job `Build result`, the artifact `deploy-package` (`deploy.ps1` and
`verify.ps1` at its root) and the artifact `container-image` (`container-image.tar.gz`, which holds the image
`container-image:<version>`).

## Layers (strict)

Host → Client → Content → Core. `AdamEve.Core` has no package reference, no project reference and no I/O.
`AdamEve.Host` is outermost and holds no game code: it serves files, sends the headers and answers `/_healthcheck`,
`/alive`, `/_version` and `/_build`. It has no project reference to the client; its publish publishes the client
and takes its `wwwroot` (a standalone client gets its import map only from its own publish).

## Definition of done

- Unit, integration and full-system tests at each level the change touches; the private build is green at the desk.
- The integration build is green, checked through the API (the conclusion of job `Build result`), not by an exit code.
- The first load (what the title needs) stays within 3.0 MB (the build fails otherwise). The modules of the garden
  and Three.js are loaded on demand; the title and the reader must not ask for them.
- Deployed to tdd and verified.
- `master` merged into the branch before a pull request.

## Faithfulness (design, section 2.1)

- `content/kjv-genesis-1-3.txt` is the only source of Scripture. It is never edited, reformatted or re-encoded:
  `.gitattributes` keeps Git from touching it, and a test pins its SHA-256.
- Scripture, narration and speech never look alike. Scripture always carries its reference.
- The LORD God and the serpent speak only Scripture.
- Jeffrey reviews all game-written text (narration, speech, glossary, kind-names) and every generated asset.

## Modesty, hard rule M1 (design, sections 1, 5.3, 5.6)

Decision D4, amended by D18 on 2026-10-10 (the amendment is the session's reading of D18, to be confirmed by
Jeffrey before final art is made). The rule, in every sprite frame, pose, animation frame, portrait, cutscene shot
and camera angle:

- **The figures are never anatomical.** They are for young readers. No rig part, in any frame, facing or variant,
  draws or suggests genitals, buttocks detail, nipples or breasts. The pelvic region of both figures is a smooth,
  featureless continuation of the body shape, as on a simple doll or a wooden figure. The woman's chest is a plain
  torso shape with no modelling. Never add anatomical detail to make a figure "more realistic": if a task seems to
  require it, stop and ask Jeffrey.
- **The woman's long hair covers her chest** whenever it faces the viewer. There is no companion foliage: D18
  removed it.
- Fig-leaf aprons from 3:7, coats of skins from 3:21: they are Scripture and stay in the rigs.
- No asset is committed without the checks of the design and a human review.

The checks are in `AdamEve.Core.Rigs`. `RigStructure` lists what a rig may hold (shapes, kinds, names, colours):
a rig with anything else does not load, and a new kind or shape fails a test until a person adds it to the lists.
`ConcealmentChecker` judges a frame: the pelvic zone is plain (the hips cover it and nothing else reaches into it),
and hair covers every pixel of the woman's chest zone that is not turned away, with alpha 0.95 or more. The unit
tests run them over every frame of every rig (`ConcealmentTests`, `RigStructureTests`); the running game runs them
for every character in every frame and writes the verdict on the game root (`data-concealment`); a figure that
fails is not drawn. A full-system test reads the pixels the renderer drew (`GardenRendererTests`). A concealment
zone needs a concealment record for every view and covering variant, or the frame fails closed. Never weaken a
check to make a frame pass: change the rig.

## The garden's camera and renderer (design, sections 6 and 7.1, decision D18)

Three.js draws the garden (`wwwroot/js/render-three.js`), the canvas is the fallback (`render.js`), and what they
share is `shell.js`. The camera is a rule of the game: `AdamEve.Core.World.PerspectiveCamera`. Culling, the tile
under a tap and the scale a figure is judged at come from it, and the renderer sets its camera from the numbers the
render list carries. Do not put a projection constant into JavaScript, and do not let anything turn or zoom the
camera. A character is flat shapes in one plane that faces the camera, at one depth: never a 3D body.

## Assets (design, section 5.5)

All generated, each from a spec in the repository, with its provenance and licence. Which generators may be used,
and under whose account, is not decided yet (design, section 11): generate nothing until it is.

## Conventions

- NUnit 4 with Shouldly. Test doubles are named `Stub...`. Arrange, act, assert, without section comments.
  Test names: `[Method]_[Scenario]_[ExpectedResult]`.
- File-scoped namespaces, nullable reference types, XML documentation on public APIs. A warning is an error, in C#
  and in PSScriptAnalyzer.
- Scripts: PowerShell 7.4, `#Requires -Version 7.4`, the help block, `Set-StrictMode -Version Latest`,
  `$ErrorActionPreference = 'Stop'`, `$PSNativeCommandUseErrorActionPreference = $true`. Log lines are `==> step`,
  `PASS`, `FAIL`, `SKIP`. `deploy/deploy.ps1` and `deploy/verify.ps1` write nothing to standard error.
- `deploy.ps1` deploys a version by the tag of its image, so it can deploy any released version. Do not put what it
  deploys into the package.
- The headers of the site (content security policy, caching, CORS of the health paths) are the host's:
  `src/AdamEve.Host`. An inline script is refused by the policy; the one import map .NET writes into `index.html`
  is allowed by its hash.
- No package and no SDK version changes without Jeffrey's approval. Versions are in `Directory.Packages.props`.
- No secret in a file, a command line or a log.
- Commit and pull-request text: no model identifier and no co-author trailer.
