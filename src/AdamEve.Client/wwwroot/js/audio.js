// The sound adapter (design, section 5.4). Slice S2 ships the setting and this adapter and no audio file: the
// table of cues is empty, so nothing plays and nothing is fetched. A later slice adds the cues.

const cues = new Map();
let enabled = true;
let unlocked = false;

// A browser lets sound start only after the first tap or key press.
for (const type of ["pointerdown", "keydown"]) {
    window.addEventListener(type, () => { unlocked = true; }, { once: true, passive: true });
}

export function setEnabled(value) {
    enabled = !!value;
    if (!enabled) {
        for (const cue of cues.values()) {
            cue.pause();
        }
    }
}

export function play(id) {
    const cue = cues.get(id);
    if (!enabled || !unlocked || !cue) {
        return false;
    }
    cue.currentTime = 0;
    cue.play();
    return true;
}
