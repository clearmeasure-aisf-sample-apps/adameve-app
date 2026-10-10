// What the two renderers of the garden share (design, section 7.1): the input adapters, the input block and the one
// call into the game for each frame, the reading of the render list, and what the game root says to the tests and to
// a screen reader. A renderer draws; everything else of the border between the browser and the game is here, once.
//
// Input events are collected here and never sent one by one. Nothing here asks the network.

// The layout of the render list and of the input block: AdamEve.Core.Game.RenderList and AdamEve.Core.Input.InputBlock
// (a unit test compares these numbers with them).
export const HEADER = 32;
export const ENTRY = 8;
export const CHARACTER = 7;
export const ANCHORS = 10;
export const ANCHOR_COUNT = 2;
export const PERSPECTIVE = 0;
export const FLAT = 1;

const UP = 1, DOWN = 2, LEFT = 4, RIGHT = 8;
const FACINGS = ["N", "NE", "E", "SE", "S", "SW", "W", "NW"];
const KEYS = {
    ArrowUp: UP, KeyW: UP, ArrowDown: DOWN, KeyS: DOWN,
    ArrowLeft: LEFT, KeyA: LEFT, ArrowRight: RIGHT, KeyD: RIGHT,
};
const DIRECTIONS = { up: UP, down: DOWN, left: LEFT, right: RIGHT };

function inForm(target) {
    return target instanceof Element && target.closest("input, select, textarea, button, a, [data-game-panel]") !== null;
}

// The game root and the canvas of the page, and what the game exports to a renderer.
export async function openPage() {
    const root = document.getElementById("game");
    const canvas = document.getElementById("game-canvas");
    if (!root || !canvas) {
        throw new Error("The page has no game root and canvas.");
    }
    const runtime = await globalThis.getDotnetRuntime(0);
    const exports = await runtime.getAssemblyExports("AdamEve.Client.dll");
    return { root, canvas, interop: exports.AdamEve.Client.Game.GameInterop };
}

// The atlas as the game lists it: for each image its flat shapes.
export function readAtlas(numbers) {
    const images = [];
    let at = 1;
    for (let image = 0; image < numbers[0]; image++) {
        const shapes = [];
        const count = numbers[at++];
        for (let shape = 0; shape < count; shape++, at += 6) {
            const [kind, x, y, width, height, colour] = numbers.slice(at, at + 6);
            shapes.push({ kind, x, y, width, height, colour });
        }
        images.push({ shapes });
    }
    return images;
}

// Starts the shared part on a page. `projection` is what the renderer draws with (PERSPECTIVE or FLAT): the game
// composes the frame, culls and reads a tap for it.
export function createShell({ root, canvas, interop, listView, inputView, verdicts, projection }) {
    const shell = {
        root, canvas, projection,
        viewWidth: 1, viewHeight: 1, ratio: 1, resized: true,
        held: 0, pressed: 0, action: 0, menu: 0, tapped: 0, tapX: 0, tapY: 0,
        input: new Float64Array(inputView.length),
        listCopy: new Float64Array(listView.length),
        published: {},
        listeners: [],
    };

    shell.listen = (target, type, handler, options) => {
        target.addEventListener(type, handler, options);
        shell.listeners.push(() => target.removeEventListener(type, handler, options));
    };

    // The size of the play area in CSS pixels, and device pixels for one of them, capped at 2.
    shell.measure = () => {
        const box = canvas.getBoundingClientRect();
        shell.ratio = Math.min(2, window.devicePixelRatio || 1);
        shell.viewWidth = Math.max(1, box.width);
        shell.viewHeight = Math.max(1, box.height);
        shell.resized = false;
    };

    // One frame of the game: the input block is written, the game runs, the render list it wrote is returned. No
    // copy where the runtime offers its view of the game's memory; otherwise one copy into a kept buffer.
    shell.step = now => {
        const input = shell.input;
        input[0] = shell.held;
        input[1] = shell.pressed;
        input[2] = shell.action;
        input[3] = shell.menu;
        input[4] = shell.tapped;
        input[5] = shell.tapX;
        input[6] = shell.tapY;
        input[7] = shell.viewWidth;
        input[8] = shell.viewHeight;
        input[9] = shell.ratio;
        input[10] = shell.projection;
        inputView.set(input);
        shell.pressed = shell.action = shell.menu = shell.tapped = 0;

        interop.Frame(now);

        if (typeof listView._unsafe_create_view === "function") {
            return listView._unsafe_create_view();
        }
        listView.copyTo(shell.listCopy);
        return shell.listCopy;
    };

    // What the tests and a screen reader's status line read: on the game root, changed only when a value changes.
    shell.publish = (list, more) => {
        const values = {
            playerTile: list[4] + "," + list[5],
            concealment: verdicts[list[6]] || "fail:unknown:unknown",
            facing: FACINGS[list[7]],
            concealmentFailures: String(list[8]),
            moving: list[9] ? "true" : "false",
            ...more,
        };
        for (const name in values) {
            if (shell.published[name] !== values[name]) {
                shell.published[name] = values[name];
                root.dataset[name] = values[name];
            }
        }
    };

    shell.dispose = () => {
        for (const remove of shell.listeners) {
            remove();
        }
        shell.listeners.length = 0;
        if (typeof listView.dispose === "function") {
            listView.dispose();
        }
        if (typeof inputView.dispose === "function") {
            inputView.dispose();
        }
    };

    // The keyboard adapter: arrow keys and WASD, eight directions; Escape for the menu; Space and Enter to act.
    shell.listen(window, "keydown", event => {
        const bit = KEYS[event.code];
        if (bit && !inForm(event.target)) {
            shell.held |= bit;
            shell.pressed |= bit;
            event.preventDefault();
        } else if (event.code === "Escape") {
            shell.menu = 1;
        } else if ((event.code === "Space" || event.code === "Enter") && !inForm(event.target)) {
            shell.action = 1;
            event.preventDefault();
        }
    });
    shell.listen(window, "keyup", event => {
        const bit = KEYS[event.code];
        if (bit) {
            shell.held &= ~bit;
        }
    });
    shell.listen(window, "blur", () => { shell.held = 0; });
    shell.listen(document, "visibilitychange", () => { shell.held = 0; });

    // The D-pad adapter: a button held walks on, a button tapped walks one tile. A browser sends pointer events,
    // touch events or both for one touch; taking both is harmless, the bits are the same.
    for (const button of root.querySelectorAll("[data-dir]")) {
        const bit = DIRECTIONS[button.dataset.dir];
        const press = event => {
            shell.held |= bit;
            shell.pressed |= bit;
            event.preventDefault();
        };
        const release = () => { shell.held &= ~bit; };
        shell.listen(button, "pointerdown", press);
        shell.listen(button, "touchstart", press, { passive: false });
        for (const type of ["pointerup", "pointercancel", "pointerleave", "touchend", "touchcancel"]) {
            shell.listen(button, type, release);
        }
        shell.listen(button, "contextmenu", event => event.preventDefault());
    }

    // The tap-to-move adapter (a click with a mouse is the same): where the play area was touched, in its own CSS
    // pixels. Which point of the ground lies under it is the game's to say: it has the camera.
    const tap = (event, point) => {
        const box = canvas.getBoundingClientRect();
        shell.tapped = 1;
        shell.tapX = point.clientX - box.left;
        shell.tapY = point.clientY - box.top;
        event.preventDefault();
    };
    shell.listen(canvas, "pointerdown", event => tap(event, event));
    shell.listen(canvas, "touchstart", event => {
        if (event.changedTouches.length > 0) {
            tap(event, event.changedTouches[0]);
        }
    }, { passive: false });

    shell.listen(window, "resize", () => { shell.resized = true; });
    shell.listen(window, "orientationchange", () => { shell.resized = true; });
    if (typeof ResizeObserver === "function") {
        const observer = new ResizeObserver(() => { shell.resized = true; });
        observer.observe(canvas);
        shell.listeners.push(() => observer.disconnect());
    }

    const touch = navigator.maxTouchPoints > 0 || "ontouchstart" in window || window.matchMedia("(pointer: coarse)").matches;
    root.dataset.touch = touch ? "true" : "false";
    return shell;
}
