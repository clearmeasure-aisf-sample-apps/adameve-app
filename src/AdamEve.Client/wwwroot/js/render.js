// The one thin module between the game (C#) and the canvas (design, section 7.1).
//
// Each frame this module writes the input block, calls the game's Frame once, reads the render list the game
// wrote and draws it. It decides nothing: the camera, the order of drawing and the M1 verdict are the game's.
// Input events are collected here and never sent one by one. Nothing here asks the network.

const HEADER = 16;
const ENTRY = 8;
const CHUNK_TILES = 16;
const KEPT_CHUNKS = 6;
const UP = 1, DOWN = 2, LEFT = 4, RIGHT = 8;
const FACINGS = ["N", "NE", "E", "SE", "S", "SW", "W", "NW"];
const KEYS = {
    ArrowUp: UP, KeyW: UP, ArrowDown: DOWN, KeyS: DOWN,
    ArrowLeft: LEFT, KeyA: LEFT, ArrowRight: RIGHT, KeyD: RIGHT,
};
const DIRECTIONS = { up: UP, down: DOWN, left: LEFT, right: RIGHT };

let game = null;

function css(colour) {
    return "#" + (colour | 0).toString(16).padStart(6, "0");
}

// The atlas: every image is placeholder art painted here once, from the flat shapes the game lists.
function buildAtlas(numbers, pixelsPerUnit) {
    const images = [];
    let at = 1;
    for (let image = 0; image < numbers[0]; image++) {
        const shapes = [];
        const count = numbers[at++];
        let left = Infinity, top = Infinity, right = -Infinity, bottom = -Infinity;
        for (let shape = 0; shape < count; shape++, at += 6) {
            const [kind, x, y, width, height, colour] = numbers.slice(at, at + 6);
            shapes.push({ kind, x, y, width, height, colour });
            left = Math.min(left, x - width / 2);
            top = Math.min(top, y - height / 2);
            right = Math.max(right, x + width / 2);
            bottom = Math.max(bottom, y + height / 2);
        }
        const canvas = document.createElement("canvas");
        canvas.width = Math.max(1, Math.ceil((right - left) * pixelsPerUnit));
        canvas.height = Math.max(1, Math.ceil((bottom - top) * pixelsPerUnit));
        const context = canvas.getContext("2d");
        context.scale(pixelsPerUnit, pixelsPerUnit);
        context.translate(-left, -top);
        for (const shape of shapes) {
            context.fillStyle = css(shape.colour);
            if (shape.kind === 1) {
                context.fillRect(shape.x - shape.width / 2, shape.y - shape.height / 2, shape.width, shape.height);
            } else {
                context.beginPath();
                context.ellipse(shape.x, shape.y, shape.width / 2, shape.height / 2, 0, 0, Math.PI * 2);
                context.fill();
            }
        }
        images.push({ canvas, left, top, width: right - left, height: bottom - top });
    }
    return images;
}

// The ground, painted for each chunk of 16 by 16 tiles when it comes into view, at the size it is drawn at: a chunk
// is then copied to the canvas pixel for pixel. A few chunks are kept; a new size of the play area paints them anew.
function chunkOf(column, row, scale) {
    if (game.chunkScale !== scale) {
        game.chunks.clear();
        game.chunkScale = scale;
    }
    const key = row * 1024 + column;
    let chunk = game.chunks.get(key);
    if (chunk) {
        game.chunks.delete(key);
        game.chunks.set(key, chunk);
        return chunk;
    }
    const size = game.tileSize * scale;
    chunk = document.createElement("canvas");
    chunk.width = chunk.height = Math.ceil(CHUNK_TILES * size);
    const context = chunk.getContext("2d", { alpha: false });
    for (let y = 0; y < CHUNK_TILES; y++) {
        for (let x = 0; x < CHUNK_TILES; x++) {
            const tileX = column * CHUNK_TILES + x;
            const tileY = row * CHUNK_TILES + y;
            const inside = tileX < game.mapWidth && tileY < game.mapHeight;
            const style = inside ? game.tiles[tileY * game.mapWidth + tileX] * 4 : -1;
            const left = Math.round(x * size);
            const top = Math.round(y * size);
            // The last column and row reach the edge of the chunk, which is a whole number of pixels.
            const width = (x === CHUNK_TILES - 1 ? chunk.width : Math.round((x + 1) * size)) - left;
            const height = (y === CHUNK_TILES - 1 ? chunk.height : Math.round((y + 1) * size)) - top;
            context.fillStyle = inside ? css(game.ground[style + ((tileX + tileY) & 1)]) : game.backdrop;
            context.fillRect(left, top, width, height);
            const mark = inside && game.ground[style + 2] >= 0 ? game.ground[style + 3] * scale : 0;
            if (mark > 0) {
                context.fillStyle = css(game.ground[style + 2]);
                context.fillRect(Math.round(left + (width - mark) / 2), Math.round(top + (height - mark) / 2), Math.round(mark), Math.round(mark));
            }
        }
    }
    game.chunks.set(key, chunk);
    if (game.chunks.size > KEPT_CHUNKS) {
        game.chunks.delete(game.chunks.keys().next().value);
    }
    return chunk;
}

function resize() {
    const box = game.canvas.getBoundingClientRect();
    game.ratio = Math.min(2, window.devicePixelRatio || 1);
    game.viewWidth = Math.max(1, box.width);
    game.viewHeight = Math.max(1, box.height);
    const width = Math.round(game.viewWidth * game.ratio);
    const height = Math.round(game.viewHeight * game.ratio);
    if (game.canvas.width !== width || game.canvas.height !== height) {
        game.canvas.width = width;
        game.canvas.height = height;
    }
    game.resized = false;
}

function readList() {
    // No copy where the runtime offers its view of the game's memory; otherwise one copy into a kept buffer.
    if (typeof game.listView._unsafe_create_view === "function") {
        return game.listView._unsafe_create_view();
    }
    game.listView.copyTo(game.listCopy);
    return game.listCopy;
}

function draw(list) {
    const context = game.context;
    const scale = list[3] * game.ratio;
    const cameraX = list[1];
    const cameraY = list[2];
    context.setTransform(1, 0, 0, 1, 0, 0);
    const chunkSize = CHUNK_TILES * game.tileSize;
    const viewRight = cameraX + game.canvas.width / scale;
    const viewBottom = cameraY + game.canvas.height / scale;
    if (cameraX < 0 || cameraY < 0 || viewRight > game.mapWidth * game.tileSize || viewBottom > game.mapHeight * game.tileSize) {
        // The play area shows more than the map: the backdrop lies behind it.
        context.fillStyle = game.backdrop;
        context.fillRect(0, 0, game.canvas.width, game.canvas.height);
    }
    const firstColumn = Math.max(0, Math.floor(cameraX / chunkSize));
    const firstRow = Math.max(0, Math.floor(cameraY / chunkSize));
    const lastColumn = Math.min(Math.ceil(game.mapWidth / CHUNK_TILES) - 1, Math.floor(viewRight / chunkSize));
    const lastRow = Math.min(Math.ceil(game.mapHeight / CHUNK_TILES) - 1, Math.floor(viewBottom / chunkSize));
    for (let row = firstRow; row <= lastRow; row++) {
        for (let column = firstColumn; column <= lastColumn; column++) {
            context.drawImage(chunkOf(column, row, scale), Math.round((column * chunkSize - cameraX) * scale), Math.round((row * chunkSize - cameraY) * scale));
        }
    }

    const count = list[0];
    for (let entry = 0; entry < count; entry++) {
        const at = HEADER + entry * ENTRY;
        const image = game.atlas[list[at]];
        if (!image) {
            continue;
        }
        context.setTransform(
            list[at + 1] * scale, list[at + 2] * scale, list[at + 3] * scale, list[at + 4] * scale,
            (list[at + 5] - cameraX) * scale, (list[at + 6] - cameraY) * scale);
        context.drawImage(image.canvas, image.left, image.top, image.width, image.height);
    }
}

// What the tests and a screen reader's status line read: on the game root, changed only when a value changes.
function publish(list) {
    const values = {
        playerTile: list[4] + "," + list[5],
        concealment: game.verdicts[list[6]] || "fail:unknown:unknown",
        facing: FACINGS[list[7]],
        concealmentFailures: String(list[8]),
        moving: list[9] ? "true" : "false",
    };
    for (const name in values) {
        if (game.published[name] !== values[name]) {
            game.published[name] = values[name];
            game.root.dataset[name] = values[name];
        }
    }
}

function tick(now) {
    if (!game) {
        return;
    }
    game.frameRequest = requestAnimationFrame(tick);
    if (game.resized) {
        resize();
    }
    const input = game.input;
    input[0] = game.held;
    input[1] = game.pressed;
    input[2] = game.action;
    input[3] = game.menu;
    input[4] = game.tapped;
    input[5] = game.tapX;
    input[6] = game.tapY;
    input[7] = game.viewWidth;
    input[8] = game.viewHeight;
    input[9] = game.ratio;
    game.inputView.set(input);
    game.pressed = game.action = game.menu = game.tapped = 0;

    game.frame(now);

    const list = readList();
    draw(list);
    publish(list);
}

function inForm(target) {
    return target instanceof Element && target.closest("input, select, textarea, button, a, [data-game-panel]") !== null;
}

function listen(target, type, handler, options) {
    target.addEventListener(type, handler, options);
    game.listeners.push(() => target.removeEventListener(type, handler, options));
}

function wireInput() {
    // The keyboard adapter: arrow keys and WASD, eight directions; Escape for the menu; Space and Enter to act.
    listen(window, "keydown", event => {
        const bit = KEYS[event.code];
        if (bit && !inForm(event.target)) {
            game.held |= bit;
            game.pressed |= bit;
            event.preventDefault();
        } else if (event.code === "Escape") {
            game.menu = 1;
        } else if ((event.code === "Space" || event.code === "Enter") && !inForm(event.target)) {
            game.action = 1;
            event.preventDefault();
        }
    });
    listen(window, "keyup", event => {
        const bit = KEYS[event.code];
        if (bit) {
            game.held &= ~bit;
        }
    });
    listen(window, "blur", () => { game.held = 0; });
    listen(document, "visibilitychange", () => { game.held = 0; });

    // The D-pad adapter: a button held walks on, a button tapped walks one tile. A browser sends pointer events,
    // touch events or both for one touch; taking both is harmless, the bits are the same.
    for (const button of game.root.querySelectorAll("[data-dir]")) {
        const bit = DIRECTIONS[button.dataset.dir];
        const press = event => {
            game.held |= bit;
            game.pressed |= bit;
            event.preventDefault();
        };
        const release = () => { game.held &= ~bit; };
        listen(button, "pointerdown", press);
        listen(button, "touchstart", press, { passive: false });
        for (const type of ["pointerup", "pointercancel", "pointerleave", "touchend", "touchcancel"]) {
            listen(button, type, release);
        }
        listen(button, "contextmenu", event => event.preventDefault());
    }

    // The tap-to-move adapter (a click with a mouse is the same): where the play area was touched.
    const tap = (event, point) => {
        const box = game.canvas.getBoundingClientRect();
        game.tapped = 1;
        game.tapX = point.clientX - box.left;
        game.tapY = point.clientY - box.top;
        event.preventDefault();
    };
    listen(game.canvas, "pointerdown", event => tap(event, event));
    listen(game.canvas, "touchstart", event => {
        if (event.changedTouches.length > 0) {
            tap(event, event.changedTouches[0]);
        }
    }, { passive: false });
}

export async function attach(listView, inputView, tiles, ground, atlas, verdicts, mapWidth, mapHeight, tileSize, backdrop) {
    detach();
    const root = document.getElementById("game");
    const canvas = document.getElementById("game-canvas");
    if (!root || !canvas) {
        throw new Error("The page has no game root and canvas.");
    }
    const runtime = await globalThis.getDotnetRuntime(0);
    const exports = await runtime.getAssemblyExports("AdamEve.Client.dll");
    game = {
        root, canvas,
        context: canvas.getContext("2d", { alpha: false }),
        frame: exports.AdamEve.Client.Game.GameInterop.Frame,
        listView, inputView,
        listCopy: new Float64Array(listView.length),
        input: new Float64Array(inputView.length),
        tiles, ground, verdicts, mapWidth, mapHeight, tileSize,
        backdrop: css(backdrop),
        atlas: buildAtlas(atlas, 3),
        chunks: new Map(),
        chunkScale: 0,
        published: {},
        listeners: [],
        held: 0, pressed: 0, action: 0, menu: 0, tapped: 0, tapX: 0, tapY: 0,
        viewWidth: 1, viewHeight: 1, ratio: 1, resized: true, frameRequest: 0,
    };
    const touch = navigator.maxTouchPoints > 0 || "ontouchstart" in window || window.matchMedia("(pointer: coarse)").matches;
    root.dataset.touch = touch ? "true" : "false";
    wireInput();
    listen(window, "resize", () => { game.resized = true; });
    listen(window, "orientationchange", () => { game.resized = true; });
    if (typeof ResizeObserver === "function") {
        const observer = new ResizeObserver(() => { if (game) { game.resized = true; } });
        observer.observe(canvas);
        game.listeners.push(() => observer.disconnect());
    }
    game.frameRequest = requestAnimationFrame(tick);
    root.dataset.ready = "true";
}

export function detach() {
    if (!game) {
        return;
    }
    cancelAnimationFrame(game.frameRequest);
    for (const remove of game.listeners) {
        remove();
    }
    if (typeof game.listView.dispose === "function") {
        game.listView.dispose();
    }
    if (typeof game.inputView.dispose === "function") {
        game.inputView.dispose();
    }
    game = null;
}
