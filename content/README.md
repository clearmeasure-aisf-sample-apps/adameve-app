# Content

`kjv-genesis-1-3.txt` is the canonical text: the only source of Scripture, kept byte for byte and never edited
(design, section 2.1). Everything else in this directory, and every label in `src/AdamEve.Content/GameText.cs`, is
game-written text. Jeffrey Palermo reviews game-written text before it ships (design, decision D8).

## Game-written text for review

This is the whole list as of slice S5. A unit test (`GameTextTests`) fails when a glossary entry, a label, a line
of narration or a kind-name with its meaning is not on it. Review status: **not yet reviewed**.

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
| The man's path, the speaker label of a Scripture card on which the LORD God speaks: Genesis 2:16, 2:17, 2:18 (the wording of the design, section 1) | The LORD God |
| The man's path, what the game calls the man until Genesis 2:19 is shown; from then on "Adam" (the wording of the design, decision D3) | The man |
| Narration, beat B9: what the player is to do after Genesis 2:8 and 2:9 (the session's wording) | Walk to the river. |
| Narration, beat B10: the first task (the session's wording, after the design's "carry water from a river to saplings") | Carry water from the river to the sapling. |
| Narration, beat B10: the second task (the session's wording, after the design's "clear fallen branches") | Clear the fallen branch. |
| Narration, beat B13: over the three kind-names of the animal that was brought (the session's wording) | Choose a name for this kind of animal. |
| Beat B13, the button beside a kind-name that shows its meaning (the design's "?" button) | ? |
| Beat B13, what a screen reader reads for the "?" button (the session's wording) | Meaning |
| Garden, the button and the heading of the journal (the design's word, section 3.4) | Journal |

## The man's path before the woman (slice S5)

Beats B8 to B13 show Genesis 2:4 to 2:20, one verse on each Scripture card, in order, each with its reference. Which
verses a beat shows, on which the LORD God speaks (2:16, 2:17, 2:18) and where the label of the man changes (2:19)
are references in `src/AdamEve.Core/Story/GardenStory.cs`, never text. The command is the two cards of Genesis 2:16
and 2:17 and nothing else: no narration, no summary, no choice. The game-written text of these beats is the nine
labels above (four of them lines of narration, held to a reading grade of 7.0 or lower by a unit test) and the
kind-names below. Nobody speaks a game-written line: the LORD God speaks only Scripture, and the man says nothing
the text does not give him.

The picture of the formation (Genesis 2:4 to 2:7) is art made by code (`FormationPicture`): bare earth, mist with
2:6, and with 2:7 dust in the air, a warm light (the breath of life; never a figure) and the man, who is the rig of
the garden, whole, judged by the modesty checks. No body is shown being shaped.

### The animals and their kind-names (`animals.json`)

Design, section 3.5 and decision D9: the player names the **kind** of each animal, choosing one of three kind-names,
each a real English word for that kind, each with a one-line meaning. 24 animals, 72 kind-names; no fish and no
serpent. The game shows the words and the meanings; it never shows the everyday name in the first column, which is
the id of the animal and of its picture. The group is the id of one of the three groups Genesis 2:20 names. The
animals are brought in the order of this table. Review status: **not yet reviewed**. Whose wording: `design` is the
starting list of the design (section 3.5), word for word; `session` was proposed by the session that built slice S5
and waits for Jeffrey's review (D8).

| Animal | Group | Kind-name | Meaning | Whose wording |
|---|---|---|---|---|
| elephant | beast | pachyderm | thick-skinned beast | design |
| elephant | beast | proboscid | trunked beast | design |
| elephant | beast | tusker | tusked beast | design |
| sheep | cattle | ovine | the sheep kind | session |
| sheep | cattle | ruminant | a beast that chews the cud | session |
| sheep | cattle | wool bearer | a beast that grows wool | session |
| dove | fowl | columbid | the dove and pigeon family | session |
| dove | fowl | columbine | of doves | session |
| dove | fowl | culver | old word for a dove | session |
| lion | beast | feline | the cat kind | design |
| lion | beast | leonine | of lions | design |
| lion | beast | great cat | a large wild cat | design |
| pig | cattle | swine | the pig kind | design |
| pig | cattle | porcine | of pigs | design |
| pig | cattle | hog | a pig | design |
| rabbit | beast | leporid | the hare family | design |
| rabbit | beast | coney | old word for a rabbit, used in the KJV | design |
| rabbit | beast | lagomorph | hares and rabbits | design |
| eagle | fowl | raptor | a bird of prey | design |
| eagle | fowl | aquiline | of eagles | design |
| eagle | fowl | bird of prey | a bird that hunts for its food | word: design; meaning: session |
| horse | cattle | equine | the horse kind | design |
| horse | cattle | steed | a riding horse | design |
| horse | cattle | courser | a swift horse | design |
| deer | beast | cervine | of deer | session |
| deer | beast | cervid | the deer family | session |
| deer | beast | hart | old word for a male deer, used in the KJV | session |
| rooster | fowl | chanticleer | old name for a rooster | session |
| rooster | fowl | galliform | the group of chickens, pheasants and quail | session |
| rooster | fowl | poultry | farm birds such as chickens | session |
| bear | beast | ursine | of bears | design |
| bear | beast | bruin | old name for a bear | design |
| bear | beast | ursid | the bear family | design |
| goat | cattle | caprine | the goat kind | session |
| goat | cattle | hircine | of goats | session |
| goat | cattle | caprid | the group of goats and sheep | session |
| sparrow | fowl | passerine | a perching bird | session |
| sparrow | fowl | songbird | a bird that sings | session |
| sparrow | fowl | oscine | the songbird group | session |
| camel | cattle | camelid | the camel family | design |
| camel | cattle | dromedary | one-humped camel | design |
| camel | cattle | humpback | old nickname | design |
| squirrel | beast | sciurid | the squirrel family | session |
| squirrel | beast | sciurine | of squirrels | session |
| squirrel | beast | rodent | a gnawing animal | session |
| duck | fowl | anatine | of ducks | session |
| duck | fowl | anatid | the duck, goose and swan family | session |
| duck | fowl | waterfowl | a bird that lives on the water | session |
| cow | cattle | bovine | the ox kind | design |
| cow | cattle | kine | cows, a KJV word | design |
| cow | cattle | neat | old word for cattle | design |
| wolf | beast | canine | the dog kind | design |
| wolf | beast | lupine | of wolves | design |
| wolf | beast | wolfkind | wolves | design |
| peacock | fowl | peafowl | the peacock kind | session |
| peacock | fowl | pavonine | of peacocks | session |
| peacock | fowl | phasianid | the pheasant family | session |
| donkey | cattle | equid | the horse family | session |
| donkey | cattle | burro | a small donkey | session |
| donkey | cattle | beast of burden | an animal that carries loads | session |
| hedgehog | beast | urchin | old word for a hedgehog | session |
| hedgehog | beast | hedgepig | old name for a hedgehog | session |
| hedgehog | beast | erinaceid | the hedgehog family | session |
| raven | fowl | corvid | the crow family | session |
| raven | fowl | corvine | of crows and ravens | session |
| raven | fowl | corbie | a Scots word for a raven or crow | session |
| badger | beast | brock | old name for a badger | session |
| badger | beast | mustelid | the weasel family | session |
| badger | beast | meline | of badgers | session |
| heron | fowl | wader | a bird that wades in shallow water | session |
| heron | fowl | ardeid | the heron family | session |
| heron | fowl | hern | old word for a heron | session |

What a unit test holds the table to (`AnimalsTests`, and at start-up `Animals.Violations`): exactly three kind-names
for each animal; no kind-name twice anywhere; a word is lowercase letters and spaces; every kind-name has a meaning;
no kind-name is "apple" or a pet name of the blocklist; every animal is cattle, fowl or beast, and none is a fish or
a serpent. The pictures of the animals are drawn by code from a handful of flat shapes each
(`src/AdamEve.Core/Game/ThingArt.cs`); no image file.

## The days of creation (slice S4)

The days of creation add no narration: every word a player reads there is a Scripture card (Genesis 1:1 to 2:3, one
verse each, with its reference), the label "God" on the cards on which he speaks, and the three controls above.
Which verses a day shows, where the player's one gesture of a day falls and on which cards God speaks are
references in `src/AdamEve.Core/Story/CreationStory.cs`, never text. The picture of each day is art
made by code (`CreationPicture`, `CreationArt`): outlines and gradients from numbers, no image file, and no word. God is never drawn as a figure: his
presence is a light, shown while his voice speaks. The two figures of light of Genesis 1:26 to 1:28 are the rigs
below, in one pale colour, far away, judged by the same modesty checks.

## The garden (slice S2)

Not text, and not generated: written by hand as data, and drawn by the game. What is planted where (the kind of
each tree, the shrubs, rocks and reeds, the drifts of flowers) is not a file: `AdamEve.Core.World.GardenScenery`
makes it from the map and a fixed seed.

| File | What |
|---|---|
| `maps/garden.tmj` | The map, 64 by 48 tiles, as Tiled JSON: the layer `ground`, the regions and the starting tiles. Three regions are open: the Central Glade, the Spring of Eden and the Pison Meadows; everything else is thicket |
| `rigs/adam.rig.json`, `rigs/woman.rig.json` | The rigs: bones, parts (one ellipse, rectangle or rectangle with rounded corners each, in one flat colour), the concealment zones, the woman's long hair that covers her chest, and a concealment record for every zone, view and covering variant (design, sections 5.3 and 5.6, as decision D18 amended them) |
| `rigs/person.anim.json` | The keyframes of `idle` and `walk`, for both rigs |

The figures are abstract by construction and never anatomical: the body of each is the same seven
plain blocks, the pelvic region is the body's own smooth shape with nothing drawn in it, and the woman's long hair
covers her chest. They carry no foliage (decision D18). Unit tests (`RigStructureTests`, `ConcealmentTests`) hold a
rig to the lists of what it may contain and compose every frame of every animation in all eight facings. They are code-drawn until the asset pipeline of slice S2b exists; no image, sound
or font file is part of them.
