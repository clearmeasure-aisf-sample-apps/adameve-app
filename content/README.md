# Content

`kjv-genesis-1-3.txt` is the canonical text: the only source of Scripture, kept byte for byte and never edited
(design, section 2.1). Everything else in this directory, and every label in `src/AdamEve.Content/GameText.cs`, is
game-written text. Jeffrey Palermo reviews game-written text before it ships (design, decision D8).

## Game-written text for review

This is the whole list as of slice S2. A unit test (`GameTextTests`) fails when a glossary entry or a
label is not on it. Review status: **not yet reviewed**.

### Glossary (`glossary.json`)

Each definition is the wording of the design's starting list (section 1). A definition is shown in narration style,
outside the Scripture card, and must never contradict the verse.

| Word | Definition |
|---|---|
| firmament | the sky |
| subtil | crafty, cunning |
| an help meet | a helper suitable for him |
| compasseth | goes around |
| bdellium | a precious resin or stone |
| enmity | hostility |
| beguiled | tricked |
| hearkened | listened to |
| sanctified | set apart as holy |
| cleave | hold fast |
| Cherubims | heavenly beings |
| meat | food |

Not in the glossary yet, because the design's list gives them no definition: *onyx*, *thereof*, *thee/thou/thy*.

### Labels (`src/AdamEve.Content/GameText.cs`)

| Where | Text |
|---|---|
| The book's name in a Scripture reference | Genesis |
| Title page, the link to the reader | Read Genesis 1 to 3 |
| Reader, the heading | Genesis 1 to 3, King James Version |
| Reader, the link to the title page | Back to the title |
| Reader, the button that puts a definition away; garden, the button that closes the settings or a notice | Close |
| Any page, when the start-up content check fails | The game's text could not be loaded |
| Title page, the link to the garden | Walk in the garden |
| Garden, the menu button | Menu |
| Garden, the heading of the settings | Settings |
| Garden, the setting of the text size | Text size |
| Garden, the text sizes (design, section 1) | S |
| | M |
| | L |
| | XL |
| Garden, the setting that turns sound on and off | Sound |
| Garden, the D-pad buttons, read by a screen reader | Up |
| | Down |
| | Left |
| | Right |
| Garden, the status line a screen reader reads: the region the player is in (the names of the design, section 5.1) | Central Glade |
| | Spring of Eden |
| | Pison Meadows |
| Garden, when the saved game cannot be read (the wording of the design, section 7.3) | Your saved game could not be read. Start again? |

## The garden (slice S2)

Not text, and not generated: written by hand as data, and drawn by the game as flat shapes.

| File | What |
|---|---|
| `maps/garden.tmj` | The map, 64 by 48 tiles, as Tiled JSON: the layer `ground`, the regions and the starting tiles. Three regions are open: the Central Glade, the Spring of Eden and the Pison Meadows; everything else is thicket |
| `rigs/adam.rig.json`, `rigs/woman.rig.json` | The placeholder rigs: bones, parts (one ellipse or rectangle each, in one flat colour), the concealment zones, the companion foliage and hair that cover them, and a concealment record for every zone, view and covering variant (design, sections 5.3 and 5.6) |
| `rigs/person.anim.json` | The keyframes of `idle` and `walk`, for both rigs |

The placeholder figures are abstract by construction: no part has anatomical detail, and a unit test
(`ConcealmentTests`) composes every frame of every animation in all eight facings and proves that every
concealment zone is covered. They are placeholders until the asset pipeline of slice S2b exists; no image, sound
or font file is part of them.
