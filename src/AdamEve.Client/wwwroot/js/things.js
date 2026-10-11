// The things of the garden that are not scenery and not a person: the animals brought to Adam, the sapling and the
// fallen branch (AdamEve.Core.Game.GardenThings, ThingArt). The game lists them in a part of the render list of
// their own, after the entries, and gives their pictures once as numbers: a handful of flat shapes each, made by
// code. This module draws them for both renderers and is the only place that knows of them.
//
// With Three.js a thing is, like a character, flat shapes in one plane that stands on its foot and faces the
// camera squarely. All things of a frame are one mesh and one draw call, with one more for the soft patches of
// shade under them. Nothing here asks the network, and nothing here is an image.

// The layout of the part of the render list that holds the things: AdamEve.Core.Game.RenderList (a unit test
// compares these numbers with it).
const THING_COUNT = 24;
const THINGS = 3872;
const ENTRY = 8;
const ART = 0, FOOT_X = 1, FOOT_Y = 2, LOOKS = 3, LIFT = 4;

const ELLIPSE_POINTS = 16;
const CORNER_POINTS = 4;
const LAYER_GAP = 0.04;
const SHADE_POINTS = 12;
const SHADE_HEIGHT = 0.9;

let pictures = [];

// The outline of one shape as points about the foot of its thing (x across, y down), turned by its angle.
function outlineOf(kind, x, y, width, height, turn, round) {
    const points = [];
    if (kind === 0) {
        for (let step = 0; step < ELLIPSE_POINTS; step++) {
            const angle = step / ELLIPSE_POINTS * Math.PI * 2;
            points.push(Math.cos(angle) * width / 2, Math.sin(angle) * height / 2);
        }
    } else if (kind === 2) {
        points.push(0, -height / 2, width / 2, height / 2, -width / 2, height / 2);
    } else if (round > 0) {
        const radius = Math.min(round, width / 2, height / 2);
        for (let corner = 0; corner < 4; corner++) {
            const centreX = (corner === 0 || corner === 3 ? 1 : -1) * (width / 2 - radius);
            const centreY = (corner < 2 ? 1 : -1) * (height / 2 - radius);
            for (let step = 0; step <= CORNER_POINTS; step++) {
                const angle = (corner + step / CORNER_POINTS) * Math.PI / 2;
                points.push(centreX + Math.cos(angle) * radius, centreY + Math.sin(angle) * radius);
            }
        }
    } else {
        points.push(-width / 2, -height / 2, width / 2, -height / 2, width / 2, height / 2, -width / 2, height / 2);
    }
    const angle = turn * Math.PI / 180;
    const cos = Math.cos(angle), sin = Math.sin(angle);
    for (let index = 0; index < points.length; index += 2) {
        const px = points[index], py = points[index + 1];
        points[index] = x + px * cos - py * sin;
        points[index + 1] = y + px * sin + py * cos;
    }
    return points;
}

// The pictures as the game lists them: how many, then for each the number of its shapes and for each shape its
// outline (0 an ellipse, 1 a box, 2 a triangle), x, y, width, height, turn in degrees, colour and corner radius.
export function setArt(numbers) {
    pictures = [];
    let at = 1;
    for (let picture = 0; picture < numbers[0]; picture++) {
        const shapes = [];
        const count = numbers[at++];
        let triangles = 0, left = 0, right = 0;
        for (let shape = 0; shape < count; shape++, at += 8) {
            const outline = outlineOf(numbers[at], numbers[at + 1], numbers[at + 2], numbers[at + 3], numbers[at + 4], numbers[at + 5], numbers[at + 7]);
            const colour = numbers[at + 6];
            shapes.push({ outline, colour, css: "#" + colour.toString(16).padStart(6, "0") });
            triangles += outline.length / 2 - 2;
            for (let index = 0; index < outline.length; index += 2) {
                left = Math.min(left, outline[index]);
                right = Math.max(right, outline[index]);
            }
        }
        pictures.push({ shapes, triangles, left, right });
    }
}

// What the page says of the things of the last frame, for the tests: how many were drawn.
function publish(state, count) {
    if (state.published !== count) {
        state.published = count;
        const root = document.getElementById("game");
        if (root) {
            root.dataset.things = String(count);
        }
    }
}

// For the canvas renderer: the things, flat, the farthest first, over what the list drew.
export function drawThings(context, list, scale, cameraX, cameraY, state) {
    const count = list[THING_COUNT];
    for (let thing = 0; thing < count; thing++) {
        const at = THINGS + thing * ENTRY;
        const picture = pictures[list[at + ART]];
        if (!picture) {
            continue;
        }
        context.setTransform(
            list[at + LOOKS] * scale, 0, 0, scale,
            (list[at + FOOT_X] - cameraX) * scale, (list[at + FOOT_Y] - list[at + LIFT] - cameraY) * scale);
        for (const shape of picture.shapes) {
            const outline = shape.outline;
            context.fillStyle = shape.css;
            context.beginPath();
            context.moveTo(outline[0], outline[1]);
            for (let index = 2; index < outline.length; index += 2) {
                context.lineTo(outline[index], outline[index + 1]);
            }
            context.closePath();
            context.fill();
        }
    }
    publish(state, count);
}

// For the Three.js renderer: one mesh for all things of a frame and one for the shade under them.
export function createThings(THREE, scene) {
    const state = { published: -1, key: "", capacity: 0, shadeCapacity: 0, colour: new THREE.Color() };
    const material = new THREE.MeshBasicMaterial({ vertexColors: true, side: THREE.DoubleSide, fog: true });
    const shadeMaterial = new THREE.MeshBasicMaterial({ color: 0x1f2a1c, transparent: true, opacity: 0.2, depthWrite: false, fog: true });
    const mesh = new THREE.Mesh(new THREE.BufferGeometry(), material);
    const shade = new THREE.Mesh(new THREE.BufferGeometry(), shadeMaterial);
    for (const each of [mesh, shade]) {
        each.frustumCulled = false;
        each.matrixAutoUpdate = false;
        each.visible = false;
        scene.add(each);
    }
    shade.renderOrder = 1;

    const room = (target, vertices, coloured) => {
        const geometry = new THREE.BufferGeometry();
        geometry.setAttribute("position", new THREE.BufferAttribute(new Float32Array(vertices * 3), 3));
        if (coloured) {
            geometry.setAttribute("color", new THREE.BufferAttribute(new Float32Array(vertices * 3), 3));
        }
        target.geometry.dispose();
        target.geometry = geometry;
    };

    state.update = (list, tilt) => {
        const count = list[THING_COUNT];
        publish(state, count);
        if (count === 0) {
            mesh.visible = shade.visible = false;
            state.key = "";
            return;
        }

        // The same things at the same places are the same triangles: nothing is filled again.
        let key = String(tilt);
        let vertices = 0;
        for (let thing = 0; thing < count; thing++) {
            const at = THINGS + thing * ENTRY;
            key += "|" + list[at + ART] + "," + list[at + FOOT_X].toFixed(2) + "," + list[at + FOOT_Y].toFixed(2) + "," + list[at + LOOKS] + "," + list[at + LIFT].toFixed(2);
            vertices += (pictures[list[at + ART]]?.triangles ?? 0) * 3;
        }
        if (key === state.key) {
            return;
        }
        state.key = key;
        if (vertices > state.capacity) {
            state.capacity = vertices + 3000;
            room(mesh, state.capacity, true);
        }
        if (count * SHADE_POINTS * 3 > state.shadeCapacity) {
            state.shadeCapacity = (count + 16) * SHADE_POINTS * 3;
            room(shade, state.shadeCapacity, false);
        }

        const sin = Math.sin(tilt), cos = Math.cos(tilt);
        const positions = mesh.geometry.attributes.position.array;
        const colours = mesh.geometry.attributes.color.array;
        const shades = shade.geometry.attributes.position.array;
        let at3 = 0, shadeAt = 0;
        for (let thing = 0; thing < count; thing++) {
            const at = THINGS + thing * ENTRY;
            const picture = pictures[list[at + ART]];
            if (!picture) {
                continue;
            }
            const footX = list[at + FOOT_X], footZ = list[at + FOOT_Y], looks = list[at + LOOKS], lift = list[at + LIFT];

            // A point of the picture in the plane of its thing: across is east, up the plane is up the picture; the
            // plane stands on the foot and leans back by the camera's tilt. Each shape lies a hair before the last.
            const put = (x, y, layer) => {
                const up = lift - y;
                positions[at3] = footX + looks * x;
                positions[at3 + 1] = up * cos + layer * LAYER_GAP * sin;
                positions[at3 + 2] = footZ - up * sin + layer * LAYER_GAP * cos;
            };
            for (let layer = 0; layer < picture.shapes.length; layer++) {
                const shape = picture.shapes[layer];
                const outline = shape.outline;
                state.colour.setHex(shape.colour);
                for (let index = 2; index + 2 < outline.length; index += 2) {
                    for (const corner of [0, index, index + 2]) {
                        put(outline[corner], outline[corner + 1], layer);
                        colours[at3] = state.colour.r;
                        colours[at3 + 1] = state.colour.g;
                        colours[at3 + 2] = state.colour.b;
                        at3 += 3;
                    }
                }
            }

            // The shade under it: a flat round patch on the ground, as wide as the thing.
            const middle = footX + looks * (picture.left + picture.right) / 2;
            const halfWidth = Math.max(3, (picture.right - picture.left) * 0.42);
            const halfDepth = Math.max(2, halfWidth * 0.36);
            for (let step = 0; step < SHADE_POINTS; step++) {
                const from = step / SHADE_POINTS * Math.PI * 2, to = (step + 1) / SHADE_POINTS * Math.PI * 2;
                shades.set([
                    middle, SHADE_HEIGHT, footZ,
                    middle + Math.cos(to) * halfWidth, SHADE_HEIGHT, footZ + Math.sin(to) * halfDepth,
                    middle + Math.cos(from) * halfWidth, SHADE_HEIGHT, footZ + Math.sin(from) * halfDepth,
                ], shadeAt);
                shadeAt += 9;
            }
        }
        mesh.geometry.setDrawRange(0, at3 / 3);
        mesh.geometry.attributes.position.needsUpdate = true;
        mesh.geometry.attributes.color.needsUpdate = true;
        shade.geometry.setDrawRange(0, shadeAt / 3);
        shade.geometry.attributes.position.needsUpdate = true;
        mesh.visible = shade.visible = true;
    };
    return state;
}
