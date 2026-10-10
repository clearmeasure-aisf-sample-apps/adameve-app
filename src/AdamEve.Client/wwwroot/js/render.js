// The canvas 2D renderer: the fallback where WebGL is not to be had, where its context was lost for good, or where
// the address of the garden asks for it (?renderer=canvas). The garden is drawn by render-three.js otherwise
// (design, section 7.1 and decision D18).
//
// It is flat and plain: no perspective, no parallax, no light and nothing that moves but the figures. The ground
// shades smoothly from place to place (no checker), the flowers lie in their drifts, and what stands on a tile is
// the few flat shapes the game lists for its kind.
//
// It is flat: no perspective and no parallax. It asks the game for the flat projection, so the camera, the culling
// and the tile under a tap are those of a flat picture. Each frame it draws the render list the game wrote, in the
// order of the list. It decides nothing: the camera, the order of drawing and the M1 verdict are the game's. What
// it shares with the other renderer (input, the frame, the game root) is shell.js. Nothing here asks the network.

import { HEADER, ENTRY, FLAT, createShell, drift, openPage, readAtlas, scatter } from "./shell.js";

const CHUNK_TILES = 16;
const KEPT_CHUNKS = 6;

let game = null;

function css(colour) {
    return "#" + (colour | 0).toString(16).padStart(6, "0");
}

// A colour between two, as CSS.
function between(from, to, share) {
    const channel = shift => Math.round(((from >> shift) & 255) + (((to >> shift) & 255) - ((from >> shift) & 255)) * share);
    return css((channel(16) << 16) | (channel(8) << 8) | channel(0));
}

// The atlas: every image is placeholder art painted here once, from the flat shapes the game lists.
function buildAtlas(numbers, pixelsPerUnit) {
    return readAtlas(numbers).map(({ shapes }) => {
        let left = Infinity, top = Infinity, right = -Infinity, bottom = -Infinity;
        for (const shape of shapes) {
            left = Math.min(left, shape.x - shape.width / 2);
            top = Math.min(top, shape.y - shape.height / 2);
            right = Math.max(right, shape.x + shape.width / 2);
            bottom = Math.max(bottom, shape.y + shape.height / 2);
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
            } else if (shape.kind === 2) {
                // A rectangle with rounded corners: four sides and four quarter circles.
                const x0 = shape.x - shape.width / 2, y0 = shape.y - shape.height / 2, x1 = x0 + shape.width, y1 = y0 + shape.height, r = shape.round;
                context.beginPath();
                context.moveTo(x0 + r, y0);
                context.arcTo(x1, y0, x1, y1, r);
                context.arcTo(x1, y1, x0, y1, r);
                context.arcTo(x0, y1, x0, y0, r);
                context.arcTo(x0, y0, x1, y0, r);
                context.closePath();
                context.fill();
            } else {
                context.beginPath();
                context.ellipse(shape.x, shape.y, shape.width / 2, shape.height / 2, 0, 0, Math.PI * 2);
                context.fill();
            }
        }
        return { canvas, left, top, width: right - left, height: bottom - top };
    });
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
            // The colour of the ground changes smoothly from tile to tile, with a little of each tile's own.
            const shade = 0.94 * drift(tileX / 7, tileY / 7) + 0.06 * scatter(tileX, tileY);
            context.fillStyle = inside ? between(game.ground[style], game.ground[style + 1], shade) : game.backdrop;
            context.fillRect(left, top, width, height);
            const mark = inside && game.ground[style + 2] >= 0 ? game.ground[style + 3] * scale : 0;
            if (mark > 0) {
                // The stones of a crossing and the resting place: a round mark.
                context.fillStyle = css(game.ground[style + 2]);
                context.beginPath();
                context.ellipse(left + width / 2, top + height / 2, mark / 2, mark * 0.42, 0, 0, Math.PI * 2);
                context.fill();
            }
            const flowers = inside ? game.cover[tileY * game.mapWidth + tileX] : 0;
            if (flowers > 0) {
                // The flowers of the tile, in the colour of their drift: thick on a flower tile, a few beside it.
                context.fillStyle = css(game.ground[game.driftColours + (flowers & 7) - 1]);
                const count = flowers & 8 ? 7 : 3;
                for (let flower = 0; flower < count; flower++) {
                    const x = left + (0.12 + 0.76 * scatter(tileX * 8 + flower, tileY)) * width;
                    const y = top + (0.12 + 0.76 * scatter(tileX, tileY * 8 + flower)) * height;
                    context.beginPath();
                    context.arc(x, y, Math.max(1, 2.2 * scale), 0, Math.PI * 2);
                    context.fill();
                }
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
    game.shell.measure();
    const width = Math.round(game.shell.viewWidth * game.shell.ratio);
    const height = Math.round(game.shell.viewHeight * game.shell.ratio);
    if (game.canvas.width !== width || game.canvas.height !== height) {
        game.canvas.width = width;
        game.canvas.height = height;
    }
}

function draw(list) {
    const context = game.context;
    const scale = list[3] * game.shell.ratio;
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

function tick(now) {
    if (!game) {
        return;
    }
    game.frameRequest = requestAnimationFrame(tick);
    if (game.shell.resized) {
        resize();
    }
    const list = game.shell.step(now);
    draw(list);
    game.shell.publish(list);
}

export async function attach(listView, inputView, tiles, ground, atlas, kinds, cover, verdicts, mapWidth, mapHeight, tileSize, backdrop) {
    detach();
    const page = await openPage();
    game = {
        canvas: page.canvas,
        context: page.canvas.getContext("2d", { alpha: false }),
        shell: createShell({ ...page, listView, inputView, verdicts, projection: FLAT }),
        tiles, ground, cover, mapWidth, mapHeight, tileSize,
        // After the four numbers of each of the ten tile kinds, the ground lists the colours of the drifts of flowers.
        driftColours: 40,
        backdrop: css(backdrop),
        atlas: buildAtlas(atlas, 3),
        chunks: new Map(),
        chunkScale: 0,
        frameRequest: 0,
    };
    game.frameRequest = requestAnimationFrame(tick);
    page.root.dataset.ready = "true";
}

export function detach() {
    if (!game) {
        return;
    }
    cancelAnimationFrame(game.frameRequest);
    game.shell.dispose();
    game = null;
}
