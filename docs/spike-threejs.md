# Spike: a Three.js renderer for the garden

Jeffrey asked on 2026-10-09 whether the graphics could be better with another technology, and said "Do the spike".
This is the spike: the garden of slice S2 drawn by a second renderer, Three.js on WebGL, beside the canvas 2D
renderer of the design (section 7.1), so the two can be compared on the test site. It evaluates one option: keep
the rules in C# and swap only the drawing. It decides nothing; the canvas renderer is the default and is unchanged.

## What was decided (2026-10-10)

Jeffrey compared the two renderers on the test site and decided (design, section 12, D18): "Three.js is better. Use
perspective or parallax when possible. Make characters not have leaves covering. Woman has long hair covering
breasts". What follows below is the spike as it was written on 2026-10-09, kept as the record of what was compared.
What changed with the decision:

- **Three.js draws the garden.** It is no longer chosen: it is the renderer. The "Renderer (trial)" setting, its
  three labels and the `sessionStorage` key are gone. The canvas renderer is the fallback: where the browser has no
  WebGL 2 (`data-renderer-fallback="webgl-unavailable"`), where the module or Three.js cannot be loaded
  (`"load-failed"`), where the WebGL context is lost while playing and does not come back within two seconds
  (`"webgl-context-lost"`; a context that comes back is taken up again, and Three.js rebuilds what it had), and
  where the address asks for it (`…/garden?renderer=canvas`, `"asked"`), for tests and for comparing the two.
- **A perspective camera**, which the spike had avoided (below, "What got harder"). It is a model in Core,
  `PerspectiveCamera`: fixed direction, 42 degrees down, 40 degrees of view, following the player. What the spike
  listed as the cost of it was done there: the game culls for the trapezoid of ground the camera sees, as far as
  the haze; a tap is a ray from the eye to the ground; each figure is judged at the scale of the place it stands
  on. The renderer has no projection number of its own: the render list carries the eye, the tilt, the field of
  view and the haze, and a full-system test compares where the renderer's camera and the game's put the same points.
- **Parallax.** Where the haze closes, a far layer (sky and two ridges of hills, made by code) hangs before the
  camera and slides less than the farthest ground. It is layered parallax, not a place of the map: at this tilt a
  true horizon is never in the picture. It stands still under `prefers-reduced-motion`.
- **Rule M1 without the foliage** (the session's reading of D18, to be confirmed by Jeffrey before final art is
  made; design, sections 1, 5.3 and 5.6). The figures are never anatomical; the woman's long hair covers her chest;
  the checks are structural, by coverage and by the rendered image. Under perspective a figure is still flat: one
  plane that stands on the feet and faces the camera squarely (it leans back by the camera's tilt, so it is
  parallel to the picture). The camera then maps the plane to the screen by one scale and a shift, which a unit
  test proves for every part of both rigs: the figure on the screen is the figure the check judged, and it is never
  seen edge-on. The parts are painted in the order of the list and every pixel of every part of a character is
  given one depth, so scenery hides all parts of a figure at a pixel or none.
- **One shared module.** `js/shell.js` holds the input adapters, the frame and the game root for both renderers; the
  copy the spike had made is gone.
- **Loading.** The title and the reader ask for no script of the garden. The garden asks for `js/audio.js`,
  `js/shell.js`, `js/render-three.js` and the two files of Three.js when a player enters it (0.16 MB as brotli
  together with the fallback's `js/render.js`, which is asked for only when it has to draw). The first load is what
  the title needs: 2.39 MB of 3.0 MB.
- **Still open** from the list at the end: a run on a real graphics chip or a real phone, memory, battery, and the
  offline cache of slice S11, which will have to hold the files the garden loads on demand.

## What was built

- `src/AdamEve.Client/wwwroot/js/render-three.js`: a second renderer module. It takes the same arguments as
  `render.js` (the render list, the input block, the tiles, the ground styles, the atlas of flat shapes) and
  publishes the same `data-` attributes on the game root.
- What it draws that the canvas does not:
  - the ground as a plane; the river three units lower, with a bank on its north shore;
  - the thicket as a raised, uneven mass; the stones of the crossing, the resting place and the flowers as solids;
  - each tree as a trunk and a crown built from the flat shapes of its image (a rectangle becomes a cylinder, an
    ellipse an ellipsoid), in the colours of `PlaceholderArt`, shaded flat;
  - a depth buffer: things hide each other by where they stand;
  - one directional light with soft shadows (a 1024 shadow map), ambient light and a sky-and-ground hemisphere
    light, and a light haze that grows toward the far edge of the picture;
  - motion: the crowns sway, the flowers stir, two layers of a light pattern made by code drift over the water. It
    stands still under `prefers-reduced-motion` (`data-motion="off"`).
- The camera is fixed: orthographic, looking north and down at 45 degrees, with the picture stretched so that the
  ground lies on the screen exactly as the canvas draws it. It follows the game's camera and cannot be turned or
  zoomed. A tap therefore names the same tile as on the canvas, and the game culls for the same rectangle.
- No art was added: no image, model, texture or font file. The only download is Three.js itself.

### What C# sends: two additive fields

The render list is the same block. Two things were added to it, and the canvas renderer reads neither:

- header numbers 10 to 13 (`RenderList.Anchors`): for each of the two characters, the logical pixel of the map
  under its feet;
- in the flags of an entry, the bits from `RenderList.CharacterShift` on: 0 for scenery, 1 or 2 for a part of that
  character (body, companion foliage and fail-closed cluster alike).

They are needed because a flat list has no depth: a renderer with depth must know which entries are one figure and
where on the ground that figure stands. Inferring it from the order of the list was possible and fragile.

## How to switch

- `…/garden?renderer=three` (and `?renderer=canvas` to go back);
- or the garden's menu, Settings, "Renderer (trial)": "Canvas", "Three.js". The garden goes on where it stood.

The choice is kept in `sessionStorage` (`adameve.trial.renderer`) for the tab only. It is not in the saved game and
not in the kept settings. The page says what draws: `data-renderer="canvas"` or `"three"` on the game root. Where
the browser has no WebGL 2, the canvas draws and the root says `data-renderer-fallback="webgl-unavailable"`
(`"load-failed"` when the module could not be loaded).

## Rule M1

Characters are flat. A character is not a body in space: every entry the game lists for it is a flat shape (a
rectangle, or a polygon drawn around the ellipse, never inside it) in one upright plane that stands at the
character's anchor. Each shape is placed by the same transform the canvas would draw it with. Because of the
camera above, a point of that plane lands on the screen where the canvas would put it, at the same scale: the
figure on the screen is the figure the check judged. Inside the plane, an entry listed later lies 0.02 units nearer
the viewer along the line of sight, which does not move it on the screen: the depth order of a figure is the order
of the list, and no scenery can hide a cover and leave what it covers (scenery in front hides both). The figures
use a material that takes no light, no shadow and no haze; under each is a plain dark patch, not a shadow of its
shape. The camera cannot turn, so a plane is never seen edge-on.

What the tests show:

- unit: every entry of a character carries its number and the anchors are the feet (`GardenGameTests`); the module
  keeps the lines this construction rests on (`ThreeVendorTests`);
- full-system, with `?renderer=three`, on Desktop Chrome, Pixel 7 and iPhone 13, for Adam and for the woman: in all
  eight facings `data-concealment` is `ok` with 0 failed frames, and for the five facings that are not turned away
  the pixels the renderer drew where the pelvic zone lies on the screen hold no skin colour. Screenshots of every
  facing are kept (`three-m1-*.png`).

The verdict itself is still computed by `ConcealmentChecker` in C#, from the pose, not from pixels.

## Three.js

Version 0.185.1, MIT, vendored in `src/AdamEve.Client/wwwroot/lib/three/`: `three.module.min.js`,
`three.core.min.js` (the module build imports it) and `LICENSE.txt`, with their SHA-256 in the folder's README and a
unit test that compares them. No addon. Downloaded from the npm registry tarball, whose SHA-512 matched the
registry's `dist.integrity`. 0.186.0 and 0.186.1 no longer ship minified builds, so 0.185.1 is the newest release
that has `three.module.min.js`.

The content security policy did not change. WebGL and Three.js needed nothing from it: no `unsafe-eval`, no inline
script, no inline style (the renderer is given the page's canvas and told not to style it), no worker, no request.
The full-system tests run under the policy and fail on any error the browser reports.

## Loading and the first load

Three.js is fetched only when this renderer is chosen: C# imports `js/render-three.js` then, and that module imports
Three.js when it attaches, after it has found WebGL 2.

| | brotli |
|---|---|
| First load (build, step PayloadBudget) | 2.39 MB, as before |
| Loaded on demand: `js/render-three.js`, `lib/three/*` | 0.16 MB (of it Three.js: 72 KB + 82 KB) |

`build.ps1` and `deploy/verify.ps1` hold the same pattern for these files, add them up apart and print both
numbers; `files.json` lists them under `onDemand`. A full-system test records the requests of a visit that goes
from the title to the garden and walks: it asks for none of them, and asks for exactly three of them after
"Three.js" is chosen. The host serves them with `Cache-Control: no-cache` (they have no fingerprint in their names),
never `immutable`.

## Frame times

Measured by the full-system test `Walk_OnAPixel7WithTheProcessorSlowedFourTimes_ShouldReportTheFramesOfBothRenderers`:
the Pixel 7 profile of Playwright (412 by 839 CSS pixels at a pixel ratio of 2, the cap), the processor slowed four
times, five seconds of walking east, the time between `requestAnimationFrame` callbacks. It is the measurement of
the canvas test, run for both renderers in turn.

| Where | WebGL drawn by | Canvas 2D | Three.js |
|---|---|---|---|
| The desk of this spike (Arch Linux, headless Chromium) | Mesa llvmpipe: software | 301 frames, median 16.7 ms, 95th percentile 16.8 ms | 145 frames, median 33.3 ms, 95th percentile 50.0 ms |
| The integration build (GitHub runner, headless Chromium) | SwiftShader: software | 301 frames, median 16.7 ms, 95th percentile 16.8 ms | 29 frames, median 166.7 ms, 95th percentile 183.4 ms |

Each build writes its own numbers to `frame-times.txt` in the artifact `test-results`, beside the screenshots
(`acceptance/**/three-*.png`).

Read these with care. A headless browser on a build machine has no graphics chip: it draws WebGL on the processor,
and the test then slows that processor four times. The Three.js numbers above therefore measure a software
rasterizer, not a phone. No run on a real graphics chip was possible from the machines of this spike. The test
reports the Three.js number and requires nothing of it; the canvas test and its budget (a median of 20 ms) are
unchanged. The number that matters has to be read on a real mid-range phone on the test site.

## What got better

- Depth: a tree is in front of or behind a character by where it stands, with no sorting rule to maintain, and the
  thicket and the stones have a height the eye reads.
- Light: one sun gives every solid a lit and a shaded side and a soft shadow on the ground, which places things.
- Motion that costs no art: sway and shimmer are a few lines.
- All of it from the same placeholder shapes.

## What got harder

- **M1 with depth and a camera.** On the canvas, "drawn later" is the whole story. Here it had to be rebuilt: one
  plane for a figure, the list's order as its depth order, a camera chosen so the plane lands on the screen as the
  check assumes, no light on the figures. Each freedom a 3D scene offers (a perspective camera, a camera that
  turns, figures that take light or cast their own shadow, bodies as models) breaks one of these and would need
  the check itself to change: it would have to judge what the camera sees, not a 2D pose.
- **The camera is the canvas's camera.** The game computes one rectangle of the map and culls for it. A perspective
  camera sees a trapezoid of the ground, wider at the far edge: the list would lack trees there, a tap would need
  a ray cast back to the tile, and a character away from the middle would be drawn at another scale than the check
  judged it at. This spike avoided all three by keeping the canvas's projection, and so has depth, light and
  shadow but no perspective and no parallax.
- **Assets.** Flat shapes became solids by a rule (ellipse to ellipsoid). Real art is not like that: see below.
- **Testing.** The canvas tests read no pixels. These must (is anything drawn; does anything move), and a headless
  Chromium has WebGL only with a launch argument that differs by machine; the tests try a short list and name what
  they got. Frame times in the build say nothing about a phone.
- **Two modules.** The input adapters are copied into the second module so that `render.js` stayed byte for byte
  as it was.

## What a real move would cost

- **Files.** `render.js` would go, or stay as the fallback for browsers without WebGL 2 (then both are maintained
  and both tested). `render-three.js` (about 740 lines now, 110 of them the copied input adapters) would grow: the fruit's glow, particles, the light of
  God's presence, the cherubims and the sword. `GardenGame.Compose`, `RenderList`, `AtlasCatalog` and
  `PlaceholderArt` change if the camera changes. `ConcealmentChecker` changes if the figures stop being flat.
  `GameInterop`, `GameSession`, `Garden.razor`: small. The title and the days of creation draw with SVG and are not
  touched by any of this.
- **The asset pipeline** (design, section 5.5, not built yet) is the real cost, and it forks:
  - *flat cut-outs in a 3D scene* (what this spike is): characters and animals stay 2D rig parts as designed, M1
    stays as designed, and only scenery needs 3D: either modelled (a second kind of generated asset, with its own
    spec, review and size budget) or flat cards standing in the scene;
  - *everything in 3D*: models, skeletons, materials and animation for people and animals, a new M1 check that
    works on rendered images from the real camera, and a review of every model from every angle the camera can
    take. That is a different project from the design's.
- **What stays.** The rules, the story machine, the save format, the text, the pages, the host, the pipeline, the
  budget's mechanism, and every test that reads the game root.
- **Size.** 0.16 MB on demand today; made the only renderer it would be in the first load: 2.39 + 0.16 = 2.55 MB
  of 3.0 MB before any 3D asset. The design rejected PixiJS for 0.4 MB the game did not need.
- **Risk.** Phones: WebGL performance and battery on a mid-range phone are unmeasured. WebGL can be absent or
  lost (an old device, a blocked graphics driver, a lost context: the spike falls back when WebGL is absent and
  does not handle a context lost while playing). A library with a release every month that has just dropped its
  minified builds.

## What was not tried

- A perspective camera, parallax, any camera movement other than following.
- A run on a real graphics chip or a real phone; memory use; battery.
- Textures, models, post-processing, anything from the addons folder, WebGPU.
- The fruit, the glow, particles, the reeds and the river as M1 occluders, cutscenes: none exist yet in S2.
- Recovery from a WebGL context lost while playing.
- Keeping the trial setting anywhere but the tab.
- The offline cache of slice S11 (it would have to know that the on-demand files are optional).
