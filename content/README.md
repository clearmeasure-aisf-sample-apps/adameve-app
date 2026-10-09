# Content

`kjv-genesis-1-3.txt` is the canonical text: the only source of Scripture, kept byte for byte and never edited
(design, section 2.1). Everything else in this directory, and every label in `src/AdamEve.Content/GameText.cs`, is
game-written text. Jeffrey Palermo reviews game-written text before it ships (design, decision D8).

## Game-written text for review

This is the whole list as of slice S1. A unit test (`GameTextTests`) fails when a glossary entry or a
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
| Reader, the button that puts a definition away | Close |
| Any page, when the start-up content check fails | The game's text could not be loaded |
