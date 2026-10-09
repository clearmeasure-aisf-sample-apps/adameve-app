# CLAUDE.md

Guidance for Claude Code sessions in this repository. The design is `docs/design.md`: every decision in it is
Jeffrey Palermo's. Read the section a change touches before changing anything.

## What this is

"Adam and woman in the garden of Eden": a browser game that tells Genesis 1 to 3 in the King James Version. Blazor
WebAssembly, .NET 10, standalone; published as static files to Azure Static Web Apps (Free), one site for each
environment. No server, no database, no account, no request to any other origin. Solution: `AdamEve.slnx`.

## Build and test

```
pwsh ./PrivateBuild.ps1                      # the private build: everything, as the integration build runs it
. ./build.ps1 ; Init ; Compile ; UnitTests   # steps, one after another
```

The integration and acceptance tests ask a running site: `PrivateBuild.ps1` publishes it and serves it with the
Static Web Apps CLI emulator (`ADAMEVE_BASE_URL`). `.github/workflows/build.yml` runs `PrivateBuild.ps1 -CI` and
nothing else. Keep it that way: no second definition of the build in the workflow file.

## Layers (strict)

Client → Content → Core. `AdamEve.Core` has no package reference, no project reference and no I/O.

## Definition of done

- Unit, integration and full-system tests at each level the change touches; the private build is green at the desk.
- The integration build is green, checked through the API (the conclusion of job `Build result`), not by an exit code.
- The first load stays within 3.0 MB (the build fails otherwise).
- Deployed to tdd and verified.
- `master` merged into the branch before a pull request.

## Faithfulness (design, section 2.1)

- `content/kjv-genesis-1-3.txt` is the only source of Scripture. It is never edited, reformatted or re-encoded:
  `.gitattributes` keeps Git from touching it, and a test pins its SHA-256.
- Scripture, narration and speech never look alike. Scripture always carries its reference.
- The LORD God and the serpent speak only Scripture.
- Jeffrey reviews all game-written text (narration, speech, glossary, kind-names) and every generated asset.

## Modesty, hard rule M1 (design, sections 1, 5.3, 5.6)

Before Genesis 3:7 the private parts of Adam and the woman are always turned away from the viewer or covered, in
every sprite frame, pose, animation frame, portrait, cutscene shot and camera angle. Fig-leaf aprons from 3:7,
coats of skins from 3:21. No asset is committed without the checks of the design and a human review.

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
- No package and no SDK version changes without Jeffrey's approval. Versions are in `Directory.Packages.props`.
- No secret in a file, a command line or a log.
- Commit and pull-request text: no model identifier and no co-author trailer.
