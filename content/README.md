# Content

`kjv-genesis-1-3.txt` is the canonical text: the only source of Scripture, kept byte for byte and never edited
(design, section 2.1). Everything else in this directory, and every label in `src/AdamEve.Content/GameText.cs`, is
game-written text. Jeffrey Palermo reviews game-written text before it ships (design, decision D8).

## Game-written text for review

This is the whole list as of slice S4. A unit test (`GameTextTests`) fails when a glossary entry or a
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
| Title, the character select: the man (the wording of the design, decision D3) | Adam |
| Title, the character select: the woman (the wording of the design, decision D3) | The woman |
| Title, the character select: the small line beneath "The woman" (the wording of the design, decision D3) | named Eve in Genesis 3:20 |
| Title, the link that goes on with the saved game | Continue |
| Days of creation, the speaker label of a Scripture card on which God speaks (the wording of the design, section 1) | God |
| Days of creation, the control of the one gesture of a day | Reveal |
| Days of creation, the control that shows the next Scripture card | Turn the page |
| Days of creation, the control that leaves them, offered after a first completion | Skip |
| Garden, the menu button | Menu |
| Title and garden, the heading of the settings | Settings |
| Title and garden, the setting of the text size | Text size |
| Title and garden, the text sizes (design, section 1) | S |
| | M |
| | L |
| | XL |
| Title and garden, the setting that turns sound on and off | Sound |
| Garden, the D-pad buttons, read by a screen reader | Up |
| | Down |
| | Left |
| | Right |
| Garden, the status line a screen reader reads: the region the player is in (the names of the design, section 5.1) | Central Glade |
| | Spring of Eden |
| | Pison Meadows |
| Garden, when the saved game cannot be read (the wording of the design, section 7.3) | Your saved game could not be read. Start again? |

## The days of creation (slice S4)

The days of creation add no narration: every word a player reads there is a Scripture card (Genesis 1:1 to 2:3, one
verse each, with its reference), the label "God" on the cards on which he speaks, and the three controls above.
Which verses a day shows, where the player's one gesture of a day falls and on which cards God speaks are
references in `src/AdamEve.Core/Story/CreationStory.cs`, never text. The picture of each day is placeholder art
made by code (`CreationPicture`): flat ellipses and rectangles, no image file. God is never drawn as a figure: his
presence is a light, shown while his voice speaks. The two figures of light of Genesis 1:26 to 1:28 are the rigs
below, in one pale colour, far away, judged by the same modesty checks.

## The garden (slice S2)

Not text, and not generated: written by hand as data, and drawn by the game as flat shapes.

| File | What |
|---|---|
| `maps/garden.tmj` | The map, 64 by 48 tiles, as Tiled JSON: the layer `ground`, the regions and the starting tiles. Three regions are open: the Central Glade, the Spring of Eden and the Pison Meadows; everything else is thicket |
| `rigs/adam.rig.json`, `rigs/woman.rig.json` | The placeholder rigs: bones, parts (one ellipse or rectangle each, in one flat colour), the concealment zones, the woman's long hair that covers her chest, and a concealment record for every zone, view and covering variant (design, sections 5.3 and 5.6, as decision D18 amended them) |
| `rigs/person.anim.json` | The keyframes of `idle` and `walk`, for both rigs |

The placeholder figures are abstract by construction and never anatomical: the body of each is the same seven
plain blocks, the pelvic region is the body's own smooth shape with nothing drawn in it, and the woman's long hair
covers her chest. They carry no foliage (decision D18). Unit tests (`RigStructureTests`, `ConcealmentTests`) hold a
rig to the lists of what it may contain and compose every frame of every animation in all eight facings. They are placeholders until the asset pipeline of slice S2b exists; no image, sound
or font file is part of them.
