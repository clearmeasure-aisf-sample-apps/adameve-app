// The renderer of the garden (design, section 7.1 and decision D18): Three.js on WebGL 2, with a perspective camera.
//
// It decides as little as the canvas renderer does. The camera is the game's: each frame the render list carries the
// eye, the tilt and the field of view of AdamEve.Core.World.PerspectiveCamera, and the camera here is set from those
// numbers and from nothing else. It looks north and down, it follows the player, and nothing here can turn it or
// zoom it. The game culls and reads a tap with the same camera, so what is listed is what can be seen and a tap
// names the ground under it.
//
// The art. Everything drawn here is made by this code when the garden opens: solids from a few primitives, a colour
// for each corner, three small patterns computed into textures, and instances. There is no image, no model and no
// shader from anywhere else. What stands where is the game's to say (AdamEve.Core.World.GardenScenery: the kind of
// each tree, the shrubs and rocks at the edge of the thicket, the reeds, the drifts of flowers); how it looks is
// decided here. The rules stay readable in the picture: only what nobody can walk on has height (trees, thicket,
// rocks, reeds, the river between its banks); the ground a character walks on carries grass and flowers and
// nothing else; a crossing is a row of flat stones with clear water around it.
//
// What the eye reads as depth and as light: nearer things are larger and cross the screen faster; a low, warm sun
// casts long soft shadows and the shade is cool; shafts of light fall beside the crowns; haze grows with distance,
// and where it closes stands the far layer (sky, clouds, three ridges that deepen in colour), which slides less than
// anything on the ground (layered parallax: it is not a place of the map). Everything that moves by itself (wind in
// the leaves and the grass, the water, the mist, pollen, butterflies, fireflies, birds, clouds, the flicker of one
// tree) moves with one clock, and under prefers-reduced-motion that clock stands.
//
// Rule M1. A character is never a body in space. Every part the game lists for it is a flat shape in ONE plane that
// stands on the character's feet and faces the camera squarely: the plane is parallel to the picture, so the figure
// on the screen is the figure the game judged, larger or smaller and nothing else, and it is never seen edge-on.
// The parts are painted in the order of the list. Every pixel of every part of one character has one and the same
// depth (written by its material, not left to the geometry), so scenery hides all of a figure's parts at a pixel
// or none of them: it cannot remove a cover and leave what it covers. The figures take no light and no shadow.
//
// Three.js itself is asked for only here, when this renderer attaches and the browser has WebGL 2. Nothing here asks
// any other origin.

import { HEADER, ENTRY, CHARACTER, ANCHORS, ANCHOR_COUNT, PERSPECTIVE, createShell, drift, openPage, readAtlas, scatter } from "./shell.js";

// Where the render list carries the camera (AdamEve.Core.Game.RenderList; a unit test compares these numbers).
const EYE_X = 16;
const EYE_HEIGHT = 17;
const EYE_Y = 18;
const TILT = 19;
const FIELD_OF_VIEW = 20;
const HAZE_START = 21;
const HAZE_END = 22;
const FIGURE_DEPTH_HEIGHT = 23;

// The tile kinds that get height or depth here, by their numbers in AdamEve.Core.World.TileKind (a unit test
// compares them).
const KIND = { water: 1, thicket: 2, crossing: 3, restingPlace: 8, flowers: 9 };

// What stands on a tile, by its number in AdamEve.Core.World.SceneryKind (a unit test compares them).
const SCENERY = { broadTree: 1, tallTree: 2, fruitTree: 3, palmTree: 4, figTree: 5, treeOfLife: 6, treeOfKnowledge: 7, forestTree: 8, shrub: 9, rock: 10, reeds: 11 };
const LAST_TREE = 8;

const WATER_LEVEL = -1.6;
const RIVER_BED = -8;
// The thicket goes on beyond the edge of the map: this many tiles as the uneven mass it is, then flat.
const THICKET_BEYOND = 6;
const TREE_CAPACITY = 160;
const ELLIPSE_SEGMENTS = 64;
const CORNER_SEGMENTS = 12;
// What keeps a frame light enough for a phone: one shadow map of this size, device pixels capped at 2 (shell.js),
// antialiasing only below that, everything that grows drawn as instances, the grass and the flowers in chunks that
// are left out when the camera does not see them, shadows cast only by the planted trees and the stones, grass and
// flowers painted thinly over the lit ground instead of being lit themselves, and no second pass over the picture.
const SHADOW_MAP = 1024;
const CHUNK_TILES = 16;
const TUFTS_ON_A_TILE = 4;
const SHAFT_CAPACITY = 20;
const GLOW_CAPACITY = 240;
const POLLEN = 70;
const FIREFLIES_SEEN = 36;
const BUTTERFLIES = 10;
const BLOSSOMS = 26;
// The sun stands low in the west, a little before the viewer: long shadows fall eastward across the picture.
const SUN = [-0.8, 0.52, 0.3];
// How long a lost WebGL context may take to come back before the game goes on with the canvas renderer.
const RESTORE_WAIT_MS = 2000;
// The far layer slides as if it stood this many times farther away than the line where the haze closes.
const FAR_RIDGE_DISTANCE = 6;
const NEAR_RIDGE_DISTANCE = 3;
// Light and air, which are not places of the map: the sun, the sky, the haze, the far hills, and the palette the
// solids are painted from. One family of colours: warm light, cool shade, greens from gold to teal.
const LIGHT = {
    haze: 0xE2E8C6, sun: 0xFFE3B4, sky: 0xC4DCFF, earth: 0x6C8C52, skyTop: 0x8CC2EE, skyLow: 0xF6E7BE,
    farRidge: 0x8FB0C8, midRidge: 0x74A6A4, nearRidge: 0x5B9777, cloud: 0xFFFDF4, cloudShade: 0xE9D9C4,
    sand: 0xDCCB9B, bed: 0x2D6F78, shallow: 0x8FD6CE, deep: 0x2F8FB2, stone: 0xC9C0AC, stoneShade: 0x8F887A, moss: 0x7FA85A,
    bark: 0x7A5A3A, barkShade: 0x54402E, shaft: 0xFFE9B0, pollen: 0xFFF1C4, firefly: 0xE8FF9A, mist: 0xFFFFFF,
};

const FIGURE_VERTEX = "void main() { gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0); }";
// One flat colour, and one depth for every pixel of every part of a character.
const FIGURE_FRAGMENT = `
uniform vec3 uColour;
uniform vec3 uHazeColour;
uniform float uHaze;
uniform float uDepth;
void main() {
    gl_FragColor = vec4(mix(uColour, uHazeColour, uHaze), 1.0);
    gl_FragDepth = uDepth;
    #include <colorspace_fragment>
}`;

// Wind, written here for the materials of Three.js: a corner of a solid leans by how much it may sway (`aSway`: 0 at
// a trunk or a root, more toward the tip), in a slow gust that travels across the garden. `aFlicker` is for the
// leaves of the tree of the knowledge of good and evil, silver on one side: a face that has it turns its silver side
// to the viewer now and then.
const WIND_PARS = `
attribute float aSway;
attribute float aFlicker;
uniform float uTime;`;
const WIND_COLOUR = `
#ifdef USE_COLOR
    if (aFlicker > 0.0) {
        float turned = smoothstep(0.35, 1.0, sin(uTime * (1.1 + 2.3 * aFlicker) + aFlicker * 41.0));
        vColor.rgb = mix(vColor.rgb, vec3(0.62, 0.68, 0.78), turned);
    }
#endif`;
const WIND_MOVE = `
#ifdef USE_INSTANCING
    vec2 windAt = instanceMatrix[3].xz;
#else
    vec2 windAt = position.xz;
#endif
    float windPhase = windAt.x * 0.021 + windAt.y * 0.033;
    float gust = sin(uTime * 0.9 + windPhase) + 0.45 * sin(uTime * 2.3 + windPhase * 2.1 + position.y * 0.05);
    transformed.x += aSway * gust;
    transformed.z += aSway * 0.5 * cos(uTime * 0.7 + windPhase);`;

// The water, written here for the material of Three.js: slow crests that carry the light of the sky and troughs that
// carry the dark of the trees, glints of the sun, and rings that spread from where a character stands on the stones
// of a crossing (`uRings`: x, z and strength for each character).
const WATER_PARS = `
uniform float uTime;
uniform vec3 uRings[${ANCHOR_COUNT}];
uniform vec3 uSkyTint;
uniform vec3 uShadeTint;
varying vec3 vWater;`;
const WATER_COLOUR = `
    vec2 flow = vWater.xz * 0.05;
    float crestA = sin(flow.x * 2.7 + sin(flow.y * 2.1 + uTime * 0.8) * 1.4 + uTime * 0.5);
    float crestB = sin(flow.y * 3.9 - uTime * 1.0 + sin(flow.x * 1.6 - uTime * 0.45) * 1.8);
    float crestC = sin((flow.x + flow.y) * 6.3 + uTime * 1.6);
    float crest = (crestA + crestB + 0.5 * crestC) / 2.5;
    float rings = 0.0;
    for (int ring = 0; ring < ${ANCHOR_COUNT}; ring++) {
        float away = distance(vWater.xz, uRings[ring].xy);
        rings += uRings[ring].z * sin(away * 0.6 - uTime * 4.2) * exp(-away * 0.045);
    }
    crest += rings;
    float lit = smoothstep(-0.25, 0.65, crest);
    diffuseColor.rgb = mix(mix(diffuseColor.rgb, uShadeTint, 0.34), diffuseColor.rgb + uSkyTint * 0.2, lit);
    float glint = smoothstep(0.78, 0.98, crest);
    diffuseColor.rgb += vec3(1.0, 0.96, 0.84) * glint * 0.55;
    diffuseColor.a = mix(diffuseColor.a, 1.0, glint * 0.7);`;

let game = null;

function hasWebGL() {
    try {
        const probe = document.createElement("canvas");
        return typeof WebGL2RenderingContext === "function" && probe.getContext("webgl2") !== null;
    } catch {
        return false;
    }
}

// Gathers triangles into one geometry: for each corner a place, a colour, how much it sways in the wind and whether
// its face flickers. Every solid is shaded flat, from its faces, so a colour is given to a face: lighter and warmer
// where the face looks up, deeper and cooler where it looks down, with a little of its own, like cut paper.
function gather(THREE) {
    const positions = [], colours = [], sways = [], flickers = [];
    const colour = new THREE.Color(), low = new THREE.Color();
    const a = new THREE.Vector3(), b = new THREE.Vector3(), c = new THREE.Vector3(), normal = new THREE.Vector3(), edge = new THREE.Vector3();
    let faces = 0;
    return {
        corner(at, hex, alpha = 1, sway = 0, flicker = 0) {
            colour.setHex(hex);
            positions.push(at[0], at[1], at[2]);
            colours.push(colour.r, colour.g, colour.b, alpha);
            sways.push(sway);
            flickers.push(flicker);
        },
        triangle(p, q, r, hex, alpha = 1) {
            this.corner(p, hex, alpha);
            this.corner(q, hex, alpha);
            this.corner(r, hex, alpha);
        },
        quad(p, q, r, s, hex, alpha = 1) {
            this.triangle(p, q, r, hex, alpha);
            this.triangle(p, r, s, hex, alpha);
        },
        // A leaf, a blade or a wing: a triangle seen from both sides, each corner with its colour and its sway.
        leaf(p, q, r, hexes, swaying = [0, 0, 0]) {
            for (const [first, second, third] of [[0, 1, 2], [0, 2, 1]]) {
                const points = [p, q, r];
                this.corner(points[first], hexes[first], 1, swaying[first]);
                this.corner(points[second], hexes[second], 1, swaying[second]);
                this.corner(points[third], hexes[third], 1, swaying[third]);
            }
        },
        // A blade of grass or a petal: a triangle seen from one side only, the side that looks up and to the south,
        // where the camera always is.
        blade(p, q, r, hexes, swaying = [0, 0, 0], seen = [1, 1, 1]) {
            normal.subVectors(b.fromArray(q), a.fromArray(p)).cross(edge.subVectors(c.fromArray(r), a)).normalize();
            const order = normal.y * 0.67 + normal.z * 0.74 >= 0 ? [0, 1, 2] : [0, 2, 1];
            const points = [p, q, r];
            for (const index of order) {
                this.corner(points[index], hexes[index], seen[index], swaying[index]);
            }
        },
        // A solid of Three.js, placed by a matrix. `look`: top and bottom (the colours of a face that looks up and
        // of one that looks down), cap (a colour for the faces that look most upward: moss), own (how much each
        // face differs), sway ([from, to, most]: by height), flicker (the share of faces that flicker).
        solid(geometry, matrix, look) {
            const plain = geometry.index ? geometry.toNonIndexed() : geometry;
            const from = plain.getAttribute("position");
            const own = look.own ?? 0.07;
            for (let index = 0; index < from.count; index += 3) {
                a.fromBufferAttribute(from, index).applyMatrix4(matrix);
                b.fromBufferAttribute(from, index + 1).applyMatrix4(matrix);
                c.fromBufferAttribute(from, index + 2).applyMatrix4(matrix);
                normal.subVectors(b, a).cross(edge.subVectors(c, a)).normalize();
                const up = 0.5 + 0.5 * normal.y;
                faces++;
                if (look.cap !== undefined && up > 0.82) {
                    colour.setHex(look.cap);
                } else {
                    colour.setHex(look.top).lerp(low.setHex(look.bottom ?? look.top), 1 - up);
                }
                colour.multiplyScalar(1 + own * (2 * scatter(faces, 7 + from.count) - 1));
                const flicker = look.flicker && scatter(faces, 31) < look.flicker ? 0.1 + 0.9 * scatter(faces, 53) : 0;
                for (const point of [a, b, c]) {
                    let sway = 0;
                    if (look.sway) {
                        const share = Math.min(1, Math.max(0, (point.y - look.sway[0]) / (look.sway[1] - look.sway[0])));
                        sway = look.sway[2] * share * share;
                    }
                    positions.push(point.x, point.y, point.z);
                    colours.push(colour.r, colour.g, colour.b, 1);
                    sways.push(sway);
                    flickers.push(flicker);
                }
            }
            if (plain !== geometry) {
                plain.dispose();
            }
            geometry.dispose();
        },
        // `mapped`: the ground also gets the place of each corner on its pattern. `lit`: every corner takes the light
        // as the flat ground does, whatever way its face looks (the grass and the flowers, which belong to the ground).
        build(mapped = 0, lit = false) {
            const geometry = new THREE.BufferGeometry();
            geometry.setAttribute("position", new THREE.Float32BufferAttribute(positions, 3));
            geometry.setAttribute("color", new THREE.Float32BufferAttribute(colours, 4));
            geometry.setAttribute("aSway", new THREE.Float32BufferAttribute(sways, 1));
            geometry.setAttribute("aFlicker", new THREE.Float32BufferAttribute(flickers, 1));
            if (lit) {
                const normals = new Float32Array(positions.length);
                for (let index = 1; index < normals.length; index += 3) {
                    normals[index] = 1;
                }
                geometry.setAttribute("normal", new THREE.BufferAttribute(normals, 3));
            }
            if (mapped > 0) {
                const uv = new Float32Array(positions.length / 3 * 2);
                for (let index = 0; index < positions.length / 3; index++) {
                    uv[index * 2] = positions[index * 3] / mapped;
                    uv[index * 2 + 1] = positions[index * 3 + 2] / mapped;
                }
                geometry.setAttribute("uv", new THREE.BufferAttribute(uv, 2));
            }
            return geometry;
        },
    };
}

// The tools every solid is made with: a matrix from a place, a size and a turn; a mass of leaves (a ball of twenty
// or eighty faces, each corner pushed in or out a little, so no two crowns are alike and none is a sphere); a trunk.
function tools(THREE) {
    const matrix = new THREE.Matrix4(), place = new THREE.Vector3(), size = new THREE.Vector3(), turn = new THREE.Quaternion(), euler = new THREE.Euler();
    const point = new THREE.Vector3();
    return {
        at(x, y, z, sx = 1, sy = sx, sz = sx, rx = 0, ry = 0, rz = 0) {
            return matrix.compose(place.set(x, y, z), turn.setFromEuler(euler.set(rx, ry, rz)), size.set(sx, sy, sz));
        },
        mass(detail, seed, rough = 0.16) {
            const geometry = new THREE.IcosahedronGeometry(1, detail);
            const corners = geometry.getAttribute("position");
            for (let index = 0; index < corners.count; index++) {
                point.fromBufferAttribute(corners, index);
                const key = Math.round(point.x * 61) * 7919 + Math.round(point.y * 61) * 104729 + Math.round(point.z * 61) * 15485863;
                point.multiplyScalar(1 + rough * (2 * scatter(key, seed) - 1));
                corners.setXYZ(index, point.x, point.y, point.z);
            }
            return geometry;
        },
        trunk(top, foot, height, sides) {
            return new THREE.CylinderGeometry(top, foot, height, sides, 1, true);
        },
        gem(radius) {
            return new THREE.OctahedronGeometry(radius, 0);
        },
    };
}

// A point on the upper, nearer side of a crown (an ellipsoid about a centre), by two numbers in [0, 1): where a
// fruit hangs or a blossom sits, so that the viewer sees it.
function onCrown(centre, radii, u, v, out = 0.96) {
    const around = u * Math.PI * 2;
    const up = 0.15 + 0.8 * v;
    const flat = Math.sqrt(1 - up * up);
    return [centre[0] + radii[0] * out * flat * Math.cos(around), centre[1] + radii[1] * out * up, centre[2] + radii[2] * out * flat * Math.sin(around)];
}

// What stands on a tile, by its kind: one geometry each, about the foot, drawn as instances. Heights are in the
// logical pixels of the map; a figure is 49 high.
function modelOf(THREE, kind) {
    const b = gather(THREE);
    const t = tools(THREE);
    const bark = { top: LIGHT.bark, bottom: LIGHT.barkShade };
    const masses = (list, look, seed) => list.forEach(([x, y, z, sx, sy, sz, detail = 0, rough = 0.18], index) =>
        b.solid(t.mass(detail, seed + index, rough), t.at(x, y, z, sx, sy, sz, 0, index * 1.7), look));
    const gems = (count, centre, radii, radius, looks, seed, sway) => {
        for (let index = 0; index < count; index++) {
            const [x, y, z] = onCrown(centre, radii, scatter(index, seed), scatter(index, seed + 1));
            b.solid(t.gem(radius), t.at(x, y, z, 1, 1.15, 1, 0, index), { ...looks[index % looks.length], own: 0.03, sway });
        }
    };

    if (kind === SCENERY.broadTree) {
        const leaves = { top: 0x9DCD62, bottom: 0x2B7250, sway: [30, 76, 1.7] };
        b.solid(t.trunk(3.0, 4.8, 32, 6), t.at(0, 16, 0, 1, 1, 1, 0, 0, 0.06), bark);
        masses([[0, 54, 0, 27, 21, 25, 1, 0.12], [-17, 45, 5, 15, 12, 14], [16, 47, -4, 15, 12, 14], [3, 63, 9, 13, 10, 12], [5, 43, 13, 12, 9, 11], [-6, 60, -10, 14, 10, 13]], leaves, 11);
    } else if (kind === SCENERY.tallTree) {
        const leaves = { top: 0x74B86C, bottom: 0x1D5C4A, sway: [18, 96, 2.4] };
        b.solid(t.trunk(2.2, 3.0, 16, 5), t.at(0, 8, 0), bark);
        masses([[0, 46, 0, 11, 36, 11, 1, 0.1], [0, 76, 0, 7, 20, 7, 0, 0.12]], leaves, 23);
    } else if (kind === SCENERY.fruitTree) {
        const leaves = { top: 0xA9D367, bottom: 0x39844F, sway: [22, 58, 1.3] };
        b.solid(t.trunk(2.6, 3.8, 24, 6), t.at(0, 12, 0, 1, 1, 1, 0, 0, -0.05), bark);
        masses([[0, 40, 0, 21, 17, 20, 1, 0.12], [-13, 35, 5, 12, 10, 11], [13, 36, -3, 12, 10, 11], [2, 49, 7, 11, 9, 10]], leaves, 37);
        gems(9, [0, 40, 0], [22, 18, 21], 2.6, [{ top: 0xF6A545, bottom: 0xD9642F }, { top: 0xF07F3C, bottom: 0xC9522B }], 41, leaves.sway);
    } else if (kind === SCENERY.palmTree) {
        // A trunk that leans as it rises, and fronds that arch out and droop at their tips.
        const rise = 62, lean = 8, rings = 6;
        for (let ring = 0; ring < rings; ring++) {
            const from = ring / rings, to = (ring + 1) / rings, middle = (from + to) / 2;
            const tilt = -Math.atan2(lean * (to * to - from * from), rise / rings);
            b.solid(t.trunk(2.5 - 0.9 * to, 2.7 - 0.9 * from, rise / rings + 0.6, 6), t.at(lean * middle * middle, rise * middle, 0, 1, 1, 1, 0, 0, tilt),
                ring % 2 ? { top: 0xB39A6E, bottom: 0x80684A } : { top: 0xA38A60, bottom: 0x775F42 });
        }
        const fronds = 9, steps = 5;
        for (let frond = 0; frond < fronds; frond++) {
            const around = (frond + 0.4 * scatter(frond, 3)) / fronds * Math.PI * 2;
            const reach = 30 + 8 * scatter(frond, 5), cos = Math.cos(around), sin = Math.sin(around);
            const spine = share => {
                const out = reach * share;
                return [lean + cos * out, rise + 9 * Math.sin(Math.PI * share * 0.8) - 24 * share * share, sin * out];
            };
            for (let step = 0; step < steps; step++) {
                const from = step / steps, to = (step + 1) / steps;
                const p = spine(from), q = spine(to);
                const wide = share => 5.5 * Math.pow(1 - share, 0.6) + 0.3;
                const side = (at, share, sign) => [at[0] - sin * wide(share) * sign, at[1] - 1.6, at[2] + cos * wide(share) * sign];
                const hue = share => (share < 0.5 ? 0x8ECB60 : 0x4F9C54);
                const sway = share => 3 * share * share;
                for (const sign of [-1, 1]) {
                    b.leaf(p, side(p, from, sign), q, [0xA6D76B, hue(from), 0x97D062], [sway(from), sway(from), sway(to)]);
                    b.leaf(side(p, from, sign), side(q, to, sign), q, [hue(from), hue(to), 0x97D062], [sway(from), sway(to), sway(to)]);
                }
            }
        }
        for (const [x, z] of [[1.8, 1.2], [-1.6, 1.5], [0.2, -2]]) {
            b.solid(t.mass(0, 71, 0.05), t.at(lean + x, rise - 2.5, z, 2.3), { top: 0x7A5636, bottom: 0x4C3524 });
        }
    } else if (kind === SCENERY.figTree) {
        const leaves = { top: 0x8BC565, bottom: 0x336D45, sway: [16, 46, 1.2] };
        b.solid(t.trunk(3.0, 4.6, 16, 6), t.at(0, 8, 0), bark);
        b.solid(t.trunk(1.8, 2.7, 15, 5), t.at(-5, 21, 0, 1, 1, 1, 0, 0, 0.55), bark);
        b.solid(t.trunk(1.8, 2.7, 15, 5), t.at(5, 21, 1, 1, 1, 1, 0, 0, -0.55), bark);
        masses([[0, 34, 0, 30, 14, 24, 1, 0.14], [-20, 30, 4, 14, 10, 12, 0, 0.22], [20, 31, -2, 14, 10, 12, 0, 0.22], [0, 42, 4, 16, 9, 14, 0, 0.22], [6, 30, 14, 13, 8, 10, 0, 0.22]], leaves, 53);
        gems(5, [0, 33, 2], [28, 12, 22], 2.2, [{ top: 0x8A5A9A, bottom: 0x5B3A6B }], 59, leaves.sway);
    } else if (kind === SCENERY.treeOfLife) {
        // Tall and upright, white-gold bark, green-gold leaves in calm tiers, pale blossoms (design, section 5.2).
        const gold = { top: 0xF5EBC6, bottom: 0xCDB77C, own: 0.03 };
        const leaves = { top: 0xDFEC88, bottom: 0x6CA651, sway: [58, 132, 1.1], own: 0.05 };
        b.solid(t.trunk(6.0, 11.5, 12, 8), t.at(0, 6, 0), gold);
        b.solid(t.trunk(4.4, 6.0, 52, 8), t.at(0, 37, 0), gold);
        for (let limb = 0; limb < 4; limb++) {
            const around = limb * Math.PI / 2 + 0.5;
            b.solid(t.trunk(1.8, 3.2, 24, 5), t.at(Math.cos(around) * 8, 68, Math.sin(around) * 8, 1, 1, 1, Math.sin(around) * 0.6, 0, -Math.cos(around) * 0.6), gold);
        }
        const tiers = [[0, 92, 0, 36, 28, 34, 1, 0.09], [0, 119, 0, 15, 12, 14, 0, 0.1]];
        for (let lobe = 0; lobe < 6; lobe++) {
            const around = lobe * Math.PI / 3;
            tiers.push([Math.cos(around) * 27, 76, Math.sin(around) * 26, 18, 14, 17, 0, 0.12]);
        }
        for (let lobe = 0; lobe < 4; lobe++) {
            const around = lobe * Math.PI / 2 + 0.7;
            tiers.push([Math.cos(around) * 15, 107, Math.sin(around) * 15, 15, 12, 14, 0, 0.12]);
        }
        masses(tiers, leaves, 67);
        gems(BLOSSOMS, [0, 86, 0], [44, 38, 42], 2.2, [{ top: 0xFFFDF0, bottom: 0xF6E7B8 }], 73, leaves.sway);
    } else if (kind === SCENERY.treeOfKnowledge) {
        // Dark silver bark on a trunk that forks and leans; a wide, low crown of angular masses whose leaves are
        // nearly black and turn a silver side now and then (design, section 5.2). No fruit is drawn yet.
        const silver = { top: 0x9BA1A9, bottom: 0x4E545E, own: 0.05 };
        const leaves = { top: 0x3A404D, bottom: 0x12151B, sway: [50, 96, 0.9], flicker: 0.5, own: 0.1 };
        b.solid(t.trunk(5.0, 9.0, 12, 7), t.at(0, 6, 0), silver);
        b.solid(t.trunk(3.8, 5.2, 28, 7), t.at(-2, 25, 0, 1, 1, 1, 0, 0, 0.13), silver);
        b.solid(t.trunk(2.4, 3.8, 32, 6), t.at(-13, 51, 0, 1, 1, 1, 0, 0, 0.6), silver);
        b.solid(t.trunk(2.4, 3.6, 30, 6), t.at(9, 51, -1, 1, 1, 1, 0, 0, -0.62), silver);
        b.solid(t.trunk(1.8, 2.8, 24, 5), t.at(-2, 52, 8, 1, 1, 1, 0.6, 0, 0), silver);
        masses([[-22, 71, 0, 26, 13, 22, 0, 0.26], [22, 69, -4, 26, 13, 22, 0, 0.26], [0, 81, 5, 30, 14, 24, 1, 0.2], [-41, 61, 6, 16, 9, 14, 0, 0.28], [41, 59, 2, 16, 9, 14, 0, 0.28], [0, 66, 19, 22, 10, 14, 0, 0.26], [4, 73, -18, 22, 10, 16, 0, 0.26]], leaves, 79);
    } else if (kind === SCENERY.forestTree) {
        const leaves = { top: 0x5FA25A, bottom: 0x1E5240, sway: [20, 82, 1.5], own: 0.1 };
        b.solid(t.trunk(2.5, 3.6, 18, 4), t.at(0, 9, 0), { top: 0x5E4630, bottom: 0x3F2F22 });
        masses([[0, 40, 0, 17, 26, 16, 1, 0.14], [3, 62, -2, 11, 16, 11, 0, 0.2], [-8, 30, 6, 10, 12, 10, 0, 0.2]], leaves, 83);
    } else if (kind === SCENERY.shrub) {
        const leaves = { top: 0x8CC665, bottom: 0x2E7345, sway: [2, 18, 0.7] };
        masses([[0, 8, 0, 13, 9, 11, 0, 0.2], [-9, 6, 3, 9, 7, 8, 0, 0.2], [9, 7, -2, 9, 7, 8, 0, 0.2]], leaves, 89);
        gems(10, [0, 7, 1], [15, 10, 12], 2.0, [{ top: 0xF9B4C8, bottom: 0xE07A9B }, { top: 0xFFF6E2, bottom: 0xF2DDB4 }], 97, leaves.sway);
    } else if (kind === SCENERY.rock) {
        const stone = { top: 0xB5AD9C, bottom: 0x7E776B, cap: LIGHT.moss, own: 0.06 };
        b.solid(t.mass(0, 101, 0.3), t.at(0, 4.5, 0, 12, 8, 10), stone);
        b.solid(t.mass(0, 103, 0.3), t.at(9, 2.5, 4, 6, 4, 5), stone);
    } else if (kind === SCENERY.reeds) {
        // Blades that stand in the water at the bank, and two heads.
        for (let blade = 0; blade < 9; blade++) {
            const around = scatter(blade, 107) * Math.PI * 2, out = 8 * scatter(blade, 109);
            const x = Math.cos(around) * out, z = Math.sin(around) * out, height = 15 + 12 * scatter(blade, 113);
            const leanX = (scatter(blade, 127) - 0.5) * 6, leanZ = (scatter(blade, 131) - 0.5) * 4;
            b.leaf([x - 0.9, -5, z], [x + 0.9, -5, z], [x + leanX, height, z + leanZ], [0x4F8448, 0x4F8448, 0xB2CF6E], [0, 0, 2.4]);
            if (blade < 2) {
                b.solid(t.gem(1), t.at(x + leanX * 0.86, height * 0.86, z + leanZ * 0.86, 1.3, 3.4, 1.3), { top: 0x7A5636, bottom: 0x4C3524, sway: [0, height, 2.2] });
            }
        }
    }
    return b.build();
}

// A small pattern made here, as a texture that repeats: its grey values are computed, never read from a file.
function pattern(THREE, side, valueAt) {
    const pixels = new Uint8Array(side * side * 4);
    for (let y = 0; y < side; y++) {
        for (let x = 0; x < side; x++) {
            const value = Math.round(255 * Math.min(1, Math.max(0, valueAt(x / side, y / side))));
            pixels.set([value, value, value, 255], (y * side + x) * 4);
        }
    }
    const texture = new THREE.DataTexture(pixels, side, side, THREE.RGBAFormat);
    texture.wrapS = texture.wrapT = THREE.RepeatWrapping;
    texture.magFilter = THREE.LinearFilter;
    texture.minFilter = THREE.LinearMipmapLinearFilter;
    texture.generateMipmaps = true;
    texture.colorSpace = THREE.SRGBColorSpace;
    texture.needsUpdate = true;
    return texture;
}

// The ground from the tiles. No tile shows its edge: the colour of the ground changes smoothly from place to place,
// the shore of the river wanders a little and slopes down to a bed under the water, the grass darkens under the
// thicket, and the thicket rises from its edge as an uneven mass. Beyond the map the thicket goes on, with the
// trees of a forest in it. x is east, y is up, z is south, in the logical pixels of the map.
function buildGround(THREE, g) {
    const size = g.tileSize;
    const t = tools(THREE);
    const ground = gather(THREE);
    const thicket = gather(THREE);
    // The thicket beyond the map and the forest in it: far from every place a character stands, it casts no shadow.
    const beyond = gather(THREE);
    const water = gather(THREE);
    const mixer = new THREE.Color(), other = new THREE.Color();
    const kindAt = (x, y) => (x < 0 || y < 0 || x >= g.mapWidth || y >= g.mapHeight ? KIND.thicket : g.tiles[y * g.mapWidth + x]);
    const wet = kind => kind === KIND.water || kind === KIND.crossing;
    // A corner of the grid, shared by the four tiles around it: where it lies and how high.
    const corners = new Map();
    const cornerOf = (column, row) => {
        const key = (row + 64) * 4096 + column + 64;
        let corner = corners.get(key);
        if (!corner) {
            const kinds = [kindAt(column - 1, row - 1), kindAt(column, row - 1), kindAt(column - 1, row), kindAt(column, row)];
            const wetAround = kinds.filter(wet).length;
            const thickAround = kinds.filter(kind => kind === KIND.thicket).length;
            corner = { x: column * size, y: 0, z: row * size, wet: wetAround, thick: thickAround };
            if (wetAround === 4) {
                corner.y = RIVER_BED;
            } else if (wetAround > 0) {
                corner.x += (scatter(column * 3 + 1, row) - 0.5) * 9;
                corner.z += (scatter(column, row * 3 + 2) - 0.5) * 9;
            } else if (thickAround === 4) {
                corner.y = 13 + 9 * scatter(column, row);
            }
            corners.set(key, corner);
        }
        return corner;
    };
    const shadeAt = (x, z) => 0.7 * drift(x / 150, z / 150) + 0.3 * drift(x / 41 + 9, z / 41 + 5);
    const hexOf = colour => colour.getHex();
    // The colour of a corner of a tile of dry ground: the grass of its place, sand at the shore, shade at the thicket.
    const landColour = (style, corner) => {
        mixer.setHex(g.ground[style]).lerp(other.setHex(g.ground[style + 1]), shadeAt(corner.x, corner.z));
        if (corner.wet > 0) {
            mixer.lerp(other.setHex(LIGHT.sand), 0.55);
        } else if (corner.thick > 0) {
            mixer.lerp(other.setHex(0x3F7F4A), 0.5);
        }
        return hexOf(mixer);
    };
    const point = corner => [corner.x, corner.y, corner.z];
    const greens = [g.ground[KIND.thicket * 4], g.ground[KIND.thicket * 4 + 1], 0x47884A, 0x356F45, 0x5A9A52];
    const forest = { top: 0x4C8F55, bottom: 0x1C4C3C, own: 0.12 };

    for (let y = -THICKET_BEYOND; y < g.mapHeight + THICKET_BEYOND; y++) {
        for (let x = -THICKET_BEYOND; x < g.mapWidth + THICKET_BEYOND; x++) {
            const kind = kindAt(x, y);
            const style = kind * 4;
            const nw = cornerOf(x, y), sw = cornerOf(x, y + 1), se = cornerOf(x + 1, y + 1), ne = cornerOf(x + 1, y);
            if (wet(kind)) {
                // The bed: sand at the shore, dark under the deep water. And the water over it, clear at the shore.
                const bed = corner => (corner.wet === 4 ? LIGHT.bed : LIGHT.sand);
                ground.corner(point(nw), bed(nw)); ground.corner(point(sw), bed(sw)); ground.corner(point(se), bed(se));
                ground.corner(point(nw), bed(nw)); ground.corner(point(se), bed(se)); ground.corner(point(ne), bed(ne));
                const surface = corner => [[corner.x, WATER_LEVEL, corner.z], corner.wet === 4 ? g.ground[style] : LIGHT.shallow, corner.wet === 4 ? 0.86 : 0.5];
                for (const corner of [nw, sw, se, nw, se, ne]) {
                    water.corner(...surface(corner));
                }
            } else if (kind === KIND.thicket) {
                const middle = [(x + 0.5) * size, 19 + 11 * scatter(x + 977, y + 131), (y + 0.5) * size];
                const pick = turn => greens[Math.floor(scatter(x * 4 + turn, y) * greens.length)];
                const outside = x < 0 || y < 0 || x >= g.mapWidth || y >= g.mapHeight;
                const mass = outside ? beyond : thicket;
                mass.triangle(point(nw), point(sw), middle, pick(0));
                mass.triangle(point(sw), point(se), middle, pick(1));
                mass.triangle(point(se), point(ne), middle, pick(2));
                mass.triangle(point(ne), point(nw), middle, pick(3));
                // Beyond the map, to the north, the west and the east: the trees of the forest, as plain masses.
                if (outside && y < g.mapHeight && scatter(x + 53, y + 71) < 0.3) {
                    const tall = 24 + 22 * scatter(x, y + 300), wide = 13 + 7 * scatter(x + 300, y);
                    beyond.solid(t.mass(0, x * 131 + y, 0.2), t.at(middle[0], middle[1] + tall * 0.6, middle[2], wide, tall, wide), forest);
                }
            } else {
                ground.corner(point(nw), landColour(style, nw)); ground.corner(point(sw), landColour(style, sw)); ground.corner(point(se), landColour(style, se));
                ground.corner(point(nw), landColour(style, nw)); ground.corner(point(se), landColour(style, se)); ground.corner(point(ne), landColour(style, ne));
            }
        }
    }

    // Beyond that the thicket is flat, as far as the haze lets anything be seen.
    const far = 6000;
    const top = 22;
    const west = -THICKET_BEYOND * size, east = (g.mapWidth + THICKET_BEYOND) * size;
    const north = -THICKET_BEYOND * size, south = (g.mapHeight + THICKET_BEYOND) * size;
    const flat = (x0, z0, x1, z1) => ground.quad([x0, top, z0], [x0, top, z1], [x1, top, z1], [x1, top, z0], greens[1]);
    flat(west - far, north - far, east + far, north);
    flat(west - far, south, east + far, south + far);
    flat(west - far, north, west, south);
    flat(east, north, east + far, south);

    // The ground has a faint mottle of its own, so that near the viewer it is not one flat sheet of colour.
    // It repeats without a seam: every wave in it closes on itself across the pattern.
    const wave = (u, v, across, down, phase) => Math.sin((u * across + v * down) * Math.PI * 2 + phase);
    const mottle = pattern(THREE, 128, (u, v) => 0.9 + 0.035 * wave(u, v, 1, 2, 0.3) + 0.03 * wave(u, v, -2, 1, 1.7) + 0.025 * wave(u, v, 3, 3, 4.1) * wave(u, v, 2, -3, 0.9)
        + 0.02 * wave(u, v, 5, -4, 2.2) - 0.07 * Math.pow(scatter(Math.floor(u * 128), Math.floor(v * 128)), 16));
    g.groundMaterial = new THREE.MeshLambertMaterial({ vertexColors: true, map: mottle });
    const groundMesh = new THREE.Mesh(ground.build(93), g.groundMaterial);
    groundMesh.receiveShadow = true;
    groundMesh.frustumCulled = false;
    const thicketMesh = new THREE.Mesh(thicket.build(), g.solidMaterial);
    thicketMesh.receiveShadow = true;
    thicketMesh.frustumCulled = false;
    const beyondMesh = new THREE.Mesh(beyond.build(), g.solidMaterial);
    beyondMesh.receiveShadow = true;
    beyondMesh.frustumCulled = false;

    // The water: one sheet over the bed, which takes the light and the shadows like everything else and has its
    // own slow crests, glints and rings (WATER_COLOUR).
    g.waterMaterial = new THREE.MeshLambertMaterial({ vertexColors: true, transparent: true, depthWrite: false });
    g.waterMaterial.onBeforeCompile = shader => {
        shader.uniforms.uTime = g.time;
        shader.uniforms.uRings = g.rings;
        shader.uniforms.uSkyTint = { value: new THREE.Color(LIGHT.sky) };
        shader.uniforms.uShadeTint = { value: new THREE.Color(0x1F5C5A) };
        shader.vertexShader = shader.vertexShader
            .replace("#include <common>", "#include <common>\nvarying vec3 vWater;")
            .replace("#include <begin_vertex>", "#include <begin_vertex>\n    vWater = (modelMatrix * vec4(transformed, 1.0)).xyz;");
        shader.fragmentShader = shader.fragmentShader
            .replace("#include <common>", "#include <common>\n" + WATER_PARS)
            .replace("#include <color_fragment>", "#include <color_fragment>\n" + WATER_COLOUR);
    };
    const waterMesh = new THREE.Mesh(water.build(), g.waterMaterial);
    waterMesh.receiveShadow = true;
    waterMesh.frustumCulled = false;
    waterMesh.renderOrder = 1;
    g.scene.add(groundMesh, thicketMesh, beyondMesh, waterMesh);
}

// What lies on the ground and is not planted: the flat stones of a crossing, a few to a tile with clear water
// between them, and the smooth pale slab of the resting place.
function buildProps(THREE, g) {
    const size = g.tileSize;
    const t = tools(THREE);
    const still = gather(THREE);
    for (let y = 0; y < g.mapHeight; y++) {
        for (let x = 0; x < g.mapWidth; x++) {
            const kind = g.tiles[y * g.mapWidth + x];
            const colour = g.ground[kind * 4 + 2];
            const centreX = (x + 0.5) * size, centreZ = (y + 0.5) * size;
            if (kind === KIND.crossing) {
                for (let stone = 0; stone < 4; stone++) {
                    const stoneX = centreX + (stone % 2 ? 7.5 : -7.5) + (scatter(x * 4 + stone, y) - 0.5) * 4;
                    const stoneZ = centreZ + (stone < 2 ? -7.5 : 7.5) + (scatter(x, y * 4 + stone) - 0.5) * 4;
                    const radius = 6.2 + 1.8 * scatter(x + stone, y + 17);
                    const height = 1.3 - RIVER_BED;
                    still.solid(new THREE.CylinderGeometry(radius - 1.2, radius + 1, height, 7), t.at(stoneX, RIVER_BED + height / 2, stoneZ, 1, 1, 0.9, 0, stone * 0.9),
                        { top: colour, bottom: LIGHT.stoneShade, own: 0.05 });
                }
            } else if (kind === KIND.restingPlace) {
                still.solid(new THREE.CylinderGeometry(12.5, 14, 1.8, 12), t.at(centreX, 0.9, centreZ), { top: colour, bottom: 0xB9A56E, own: 0.03 });
            }
        }
    }

    const stillMesh = new THREE.Mesh(still.build(), g.solidMaterial);
    stillMesh.castShadow = true;
    stillMesh.receiveShadow = true;
    stillMesh.frustumCulled = false;
    g.scene.add(stillMesh);
}

// The grass and the flowers: tufts of three blades on every tile a character can walk on (and about the foot of
// each tree), and blossoms where the game says a drift of flowers lies, in the colour of the drift. They are low:
// nothing here looks like a thing in the way. Instances, in chunks of 16 by 16 tiles, each drawn only when the
// camera sees it; they cast no shadow.
function buildCover(THREE, g) {
    const size = g.tileSize;
    const tuft = gather(THREE);
    for (let blade = 0; blade < 3; blade++) {
        const x = (blade - 1) * 1.7, z = blade === 1 ? 0.6 : -0.4, height = 5.5 + 1.5 * blade;
        tuft.blade([x - 0.8, 0, z], [x + 0.8, 0, z], [x * 2.6 + 0.4, height, z - 1.2], [0x8FC463, 0x8FC463, 0xD6EC96], [0, 0, 1.5], [0.25, 0.25, 0.85]);
    }
    const blossom = gather(THREE);
    for (let petal = 0; petal < 5; petal++) {
        const from = petal / 5 * Math.PI * 2, to = (petal + 1) / 5 * Math.PI * 2;
        blossom.blade([0, 0, 0], [Math.cos(to) * 2.3, 0.7, Math.sin(to) * 2.3], [Math.cos(from) * 2.3, 0.7, Math.sin(from) * 2.3], [0xF2C84B, 0xFFFFFF, 0xFFFFFF], [1.2, 1.2, 1.2]);
    }
    blossom.blade([-0.3, -5, 0], [0.3, -5, 0], [0, 0, 0], [0x3D7A3A, 0x3D7A3A, 0x3D7A3A], [0, 0, 1.2]);
    // Grass and flowers take no light of their own and no shadow lookup: they are painted thinly over the ground,
    // which has both, so a blade in the shade of a tree is darker with the ground under it. That keeps thousands of
    // blades cheap. They are painted before the figures and write no depth, like everything in the air.
    g.coverMaterial = windy(new THREE.MeshBasicMaterial({ vertexColors: true, transparent: true, depthWrite: false }), g);
    const tuftGeometry = tuft.build(), blossomGeometry = blossom.build();

    const chunks = new Map();
    const chunkOf = (x, y) => {
        const key = Math.floor(y / CHUNK_TILES) * 64 + Math.floor(x / CHUNK_TILES);
        if (!chunks.has(key)) {
            chunks.set(key, { tufts: [], blossoms: [] });
        }
        return chunks.get(key);
    };
    const grows = kind => kind !== KIND.water && kind !== KIND.thicket && kind !== KIND.crossing && kind !== KIND.restingPlace;
    for (let y = 0; y < g.mapHeight; y++) {
        for (let x = 0; x < g.mapWidth; x++) {
            if (!grows(g.tiles[y * g.mapWidth + x])) {
                continue;
            }
            const chunk = chunkOf(x, y);
            for (let index = 0; index < TUFTS_ON_A_TILE; index++) {
                const tuftX = (x + scatter(x * 8 + index, y + 11)) * size, tuftZ = (y + scatter(x + 13, y * 8 + index)) * size;
                chunk.tufts.push({ x: tuftX, z: tuftZ, turn: (scatter(x * 8 + index, y * 8 + 3) - 0.5) * 1.6, size: 0.75 + 0.7 * scatter(x * 8 + index, y * 8 + 5), shade: drift(tuftX / 150, tuftZ / 150) });
            }
            const flowers = g.cover[y * g.mapWidth + x];
            for (let index = 0; index < (flowers === 0 ? 0 : flowers & 8 ? 9 : 3); index++) {
                chunk.blossoms.push({
                    x: (x + 0.08 + 0.84 * scatter(x * 16 + index, y + 23)) * size, z: (y + 0.08 + 0.84 * scatter(x + 29, y * 16 + index)) * size,
                    turn: (scatter(x * 16 + index, y * 16 + 1) - 0.5) * 1.6, size: 0.8 + 0.5 * scatter(x * 16 + index, y * 16 + 7), colour: g.ground[g.driftColours + (flowers & 7) - 1],
                });
            }
        }
    }

    const t = tools(THREE);
    const colour = new THREE.Color(), deep = new THREE.Color(0xB8DDA8), warm = new THREE.Color(0xFFFFFF);
    const plant = (geometry, places, placeOf) => {
        if (places.length === 0) {
            return;
        }
        const mesh = new THREE.InstancedMesh(geometry, g.coverMaterial, places.length);
        places.forEach((place, index) => {
            mesh.setMatrixAt(index, placeOf(place));
            mesh.setColorAt(index, place.colour === undefined ? colour.copy(warm).lerp(deep, place.shade) : colour.setHex(place.colour));
        });
        mesh.renderOrder = 2;
        mesh.computeBoundingSphere();
        g.scene.add(mesh);
        g.coverMeshes.push(mesh);
    };
    for (const chunk of chunks.values()) {
        plant(tuftGeometry, chunk.tufts, place => t.at(place.x, 0, place.z, place.size, place.size, place.size, 0, place.turn));
        plant(blossomGeometry, chunk.blossoms, place => t.at(place.x, 4.2 + 2.2 * place.size, place.z, place.size, place.size, place.size, 0.25, place.turn, 0.2));
    }
}

// Gives a material of Three.js the wind (WIND_MOVE) and the flicker of leaves (WIND_COLOUR).
function windy(material, g) {
    material.onBeforeCompile = shader => {
        shader.uniforms.uTime = g.time;
        shader.vertexShader = shader.vertexShader
            .replace("#include <common>", "#include <common>\n" + WIND_PARS)
            .replace("#include <color_vertex>", "#include <color_vertex>\n" + WIND_COLOUR)
            .replace("#include <begin_vertex>", "#include <begin_vertex>\n" + WIND_MOVE);
    };
    return material;
}

// What stands on a tile, as the game lists it each frame: one mesh of instances for each kind, made when the kind
// is first seen. A plant turns and grows a little by the place it stands on, so no two are alike; the two trees in
// the midst of the garden and the fig tree stand as they were made.
function sceneryOf(THREE, g, kind) {
    let scenery = g.scenery[kind];
    if (!scenery) {
        const geometry = modelOf(THREE, kind);
        const mesh = new THREE.InstancedMesh(geometry, g.solidMaterial, TREE_CAPACITY);
        mesh.instanceMatrix.setUsage(THREE.DynamicDrawUsage);
        if (kind < SCENERY.forestTree) {
            // The planted trees cast a shadow; the forest behind the thicket and what is low do not.
            mesh.castShadow = true;
        }
        mesh.receiveShadow = true;
        mesh.frustumCulled = false;
        mesh.count = 0;
        g.scene.add(mesh);
        const special = kind === SCENERY.treeOfLife || kind === SCENERY.treeOfKnowledge || kind === SCENERY.figTree;
        const grow = kind === SCENERY.forestTree ? [0.95, 0.65] : kind > LAST_TREE ? [0.8, 0.5] : [0.88, 0.3];
        scenery = g.scenery[kind] = { mesh, count: 0, tree: kind <= LAST_TREE, special, grow, shafts: kind === SCENERY.broadTree || kind === SCENERY.fruitTree || kind === SCENERY.treeOfLife };
    }
    return scenery;
}

// Light in the air. Three meshes of instances, none of which takes the light or casts a shadow:
// shafts of sunlight that fall beside a crown to the ground; soft round glows that face the viewer (the steady warm
// light about the tree of life, the cool one about the other tree, the mist over the spring, fireflies in the
// shade, pollen on the wind); and the wings of butterflies over the flowers.
function buildAir(THREE, g) {
    const size = g.tileSize;
    const glowing = () => new THREE.MeshBasicMaterial({ vertexColors: true, transparent: true, blending: THREE.AdditiveBlending, depthWrite: false, fog: false, side: THREE.DoubleSide });

    // A shaft: a strip from the ground (y 0) to the crown (y 1), brightest along its middle, fading at both ends.
    const shaft = gather(THREE);
    const rows = [[0, 0.5], [0.45, 1], [1, 0]];
    for (let row = 0; row < rows.length - 1; row++) {
        for (const [left, right, leftLight, rightLight] of [[-0.5, 0, 0, 1], [0, 0.5, 1, 0]]) {
            const [y0, light0] = rows[row], [y1, light1] = rows[row + 1];
            shaft.corner([left, y0, 0], 0xFFFFFF, leftLight * light0); shaft.corner([right, y0, 0], 0xFFFFFF, rightLight * light0); shaft.corner([right, y1, 0], 0xFFFFFF, rightLight * light1);
            shaft.corner([left, y0, 0], 0xFFFFFF, leftLight * light0); shaft.corner([right, y1, 0], 0xFFFFFF, rightLight * light1); shaft.corner([left, y1, 0], 0xFFFFFF, leftLight * light1);
        }
    }
    g.shafts = new THREE.InstancedMesh(shaft.build(), glowing(), SHAFT_CAPACITY);
    // A glow: a disc that is bright in its middle and nothing at its rim.
    const glow = gather(THREE);
    for (let step = 0; step < 10; step++) {
        const from = step / 10 * Math.PI * 2, to = (step + 1) / 10 * Math.PI * 2;
        glow.corner([0, 0, 0], 0xFFFFFF, 1); glow.corner([Math.cos(from), Math.sin(from), 0], 0xFFFFFF, 0); glow.corner([Math.cos(to), Math.sin(to), 0], 0xFFFFFF, 0);
    }
    g.glows = new THREE.InstancedMesh(glow.build(), glowing(), GLOW_CAPACITY);
    // A wing: flat, hinged on the body (the z axis), reaching out along x.
    const wing = gather(THREE);
    wing.leaf([0, 0, -1.2], [4.4, 0, -3.2], [3.4, 0, 0.4], [0xFFFFFF, 0xFFFFFF, 0xD8D8D8]);
    wing.leaf([0, 0, -0.6], [3.4, 0, 0.4], [2.2, 0, 3], [0xFFFFFF, 0xD8D8D8, 0xFFFFFF]);
    // Like the glows, the wings are painted before the figures and write no depth: nothing in the air is ever
    // painted over a figure.
    g.wings = new THREE.InstancedMesh(wing.build(), new THREE.MeshBasicMaterial({ vertexColors: true, transparent: true, depthWrite: false }), BUTTERFLIES * 2);
    for (const mesh of [g.shafts, g.glows, g.wings]) {
        mesh.instanceMatrix.setUsage(THREE.DynamicDrawUsage);
        mesh.frustumCulled = false;
        mesh.count = 0;
        mesh.renderOrder = 4;
        mesh.setColorAt(0, new THREE.Color(0xFFFFFF));
        mesh.instanceColor.setUsage(THREE.DynamicDrawUsage);
        g.scene.add(mesh);
    }

    // The blossoms of the tree of life, where its model has them (modelOf): each gets a glow.
    for (let index = 0; index < BLOSSOMS; index++) {
        g.blossoms.push(onCrown([0, 86, 0], [44, 38, 42], scatter(index, 73), scatter(index, 74)));
    }

    // Where the air has something of its own: the spring (water with water all around it) has mist; the shade at
    // the foot of trees and at the edge of the thicket has fireflies; the thick drifts of flowers have butterflies.
    const kindAt = (x, y) => (x < 0 || y < 0 || x >= g.mapWidth || y >= g.mapHeight ? KIND.thicket : g.tiles[y * g.mapWidth + x]);
    const tints = [0xF6A545, 0x8CC7F0, 0xFFF3B0, 0xC79BEA, 0xF08A7B];
    for (let y = 0; y < g.mapHeight; y++) {
        for (let x = 0; x < g.mapWidth; x++) {
            const kind = kindAt(x, y);
            const centreX = (x + 0.5) * size, centreZ = (y + 0.5) * size;
            let open = 0, wetAround = 0;
            for (let dy = -1; dy <= 1; dy++) {
                for (let dx = -1; dx <= 1; dx++) {
                    const near = kindAt(x + dx, y + dy);
                    open += near !== KIND.thicket && near !== KIND.water ? 1 : 0;
                    wetAround += near === KIND.water ? 1 : 0;
                }
            }
            if (kind === KIND.water && wetAround === 9) {
                for (let puff = 0; puff < 3; puff++) {
                    g.mist.push({ x: centreX + (scatter(x * 4 + puff, y) - 0.5) * 110, z: centreZ + (scatter(x, y * 4 + puff) - 0.5) * 110, phase: scatter(x + puff, y + 7) * 6.28 });
                }
            } else if (((kind === KIND.thicket && open > 0) || (kind >= 4 && kind <= 7)) && scatter(x + 5, y + 3) < 0.4) {
                for (let fly = 0; fly < 2; fly++) {
                    g.fireflies.push({ x: centreX + (scatter(x * 2 + fly, y) - 0.5) * 44, z: centreZ + (scatter(x, y * 2 + fly) - 0.2) * 34, phase: scatter(x + fly, y + 19) * 6.28 });
                }
            } else if (g.cover[y * g.mapWidth + x] & 8 && scatter(x + 31, y + 37) < 0.2) {
                g.butterflies.push({ x: centreX, z: centreZ, phase: scatter(x, y + 41) * 6.28, tint: tints[Math.floor(scatter(x + 43, y) * tints.length)] });
            }
        }
    }
}

// The flat shape of a part of a character, as the canvas paints it. An ellipse is a polygon drawn around the
// ellipse, never inside it, and so is a rounded corner: a cover is never smaller here than the check takes it to be.
function flatOf(THREE, image) {
    if (image.flat) {
        return image.flat;
    }
    const shape = image.shapes[0];
    let geometry;
    if (shape.kind === 1) {
        geometry = new THREE.PlaneGeometry(shape.width, shape.height);
    } else if (shape.kind === 2) {
        // A rectangle with rounded corners: a fan about its middle. Each corner is a polygon drawn around its
        // quarter circle, as an ellipse is.
        const beyond = shape.round / Math.cos(Math.PI / 4 / CORNER_SEGMENTS) - shape.round;
        const outline = [];
        for (let corner = 0; corner < 4; corner++) {
            const centreX = (corner === 0 || corner === 3 ? 1 : -1) * (shape.width / 2 - shape.round);
            const centreY = (corner < 2 ? 1 : -1) * (shape.height / 2 - shape.round);
            for (let step = 0; step <= CORNER_SEGMENTS; step++) {
                const angle = (corner + step / CORNER_SEGMENTS) * Math.PI / 2;
                outline.push(centreX + Math.cos(angle) * (shape.round + beyond), centreY + Math.sin(angle) * (shape.round + beyond));
            }
        }
        const corners = [];
        for (let index = 0; index < outline.length; index += 2) {
            const next = (index + 2) % outline.length;
            corners.push(0, 0, 0, outline[index], outline[index + 1], 0, outline[next], outline[next + 1], 0);
        }
        geometry = new THREE.BufferGeometry();
        geometry.setAttribute("position", new THREE.Float32BufferAttribute(corners, 3));
    } else {
        const around = 1 / Math.cos(Math.PI / ELLIPSE_SEGMENTS);
        geometry = new THREE.CircleGeometry(1, ELLIPSE_SEGMENTS);
        geometry.scale(shape.width / 2 * around, shape.height / 2 * around, 1);
    }
    geometry.translate(shape.x, -shape.y, 0);
    image.flat = { geometry, colour: shape.colour };
    return image.flat;
}

// The material of the parts of one character in one colour. It takes no light, no shadow and no texture: the colour
// of a part is the colour the rig gives it (in the distance, with the haze of the place the figure stands on). It
// writes the one depth of its character for every pixel, and paints over what was painted before it.
function figureMaterial(THREE, g, character, colour) {
    const key = character * 0x1000000 + colour;
    let material = g.figureMaterials.get(key);
    if (!material) {
        const figure = g.figures[character - 1];
        material = new THREE.ShaderMaterial({
            uniforms: {
                uColour: { value: new THREE.Color(colour) },
                uHazeColour: { value: g.hazeColour },
                uHaze: figure.haze,
                uDepth: figure.depth,
            },
            vertexShader: FIGURE_VERTEX,
            fragmentShader: FIGURE_FRAGMENT,
            side: THREE.DoubleSide, transparent: true, depthTest: true, depthWrite: false, fog: false,
        });
        g.figureMaterials.set(key, material);
    }
    return material;
}

// The far layer: the sky with its slow clouds, birds crossing far off, and three ridges of hills whose colour
// deepens from the haze toward blue, on a sheet that hangs before the camera where the haze closes. Its foot has
// the colour of the haze, so the ground runs into it without a seam. It is light and air, not a place: it belongs
// to the camera, and its ridges slide a little against the camera's travel.
function buildFarLayer(THREE, g) {
    const haze = LIGHT.haze;
    const mixed = (hex, share) => new THREE.Color(hex).lerp(new THREE.Color(haze), share).getHex();
    // It writes its depth: water, glows and shafts that lie beyond it are not painted over it.
    const material = new THREE.MeshBasicMaterial({ vertexColors: true, fog: false, transparent: true });
    const add = (builder, order) => {
        const mesh = new THREE.Mesh(builder.build(), material);
        mesh.frustumCulled = false;
        mesh.renderOrder = order;
        g.far.add(mesh);
        return mesh;
    };

    // Across from -1 to 1 is the width of the picture there; up from 0 (the line where the haze closes) to 1 (the
    // top of the picture). The sky: the haze, then a warm low light, then blue.
    const sky = gather(THREE);
    const band = (from, to, low, high) => {
        sky.corner([-1.2, from, 0], low); sky.corner([1.2, from, 0], low); sky.corner([1.2, to, 0], high);
        sky.corner([-1.2, from, 0], low); sky.corner([1.2, to, 0], high); sky.corner([-1.2, to, 0], high);
    };
    band(-0.3, 0.1, haze, haze);
    band(0.1, 0.5, haze, LIGHT.skyLow);
    band(0.5, 1.2, LIGHT.skyLow, LIGHT.skyTop);
    add(sky, -6);

    // Clouds: each a few flat rounds on one flat foot, lit from above. They repeat every 2 across and drift.
    const clouds = gather(THREE);
    const round = (x, y, wide, high) => {
        for (let step = 0; step < 14; step++) {
            const from = step / 14 * Math.PI, to = (step + 1) / 14 * Math.PI;
            clouds.corner([x, y, 0.4], LIGHT.cloudShade, 0.85);
            clouds.corner([x + Math.cos(from) * wide, y + Math.sin(from) * high, 0.4], LIGHT.cloud, 0.9);
            clouds.corner([x + Math.cos(to) * wide, y + Math.sin(to) * high, 0.4], LIGHT.cloud, 0.9);
        }
    };
    for (const copy of [-2, 0, 2]) {
        for (const [x, y, scale] of [[-0.78, 0.74, 1], [-0.2, 0.86, 0.7], [0.36, 0.7, 1.15], [0.84, 0.82, 0.8]]) {
            round(copy + x, y, 0.12 * scale, 0.07 * scale);
            round(copy + x - 0.1 * scale, y, 0.08 * scale, 0.04 * scale);
            round(copy + x + 0.11 * scale, y, 0.09 * scale, 0.05 * scale);
        }
    }
    g.clouds = add(clouds, -5);

    // Birds, far off: five pairs of wings, plain and dark, that cross the sky together.
    const birds = gather(THREE);
    for (const [x, y, wide] of [[0, 0, 0.017], [0.05, 0.03, 0.013], [-0.045, 0.026, 0.014], [0.1, -0.008, 0.012], [-0.095, -0.016, 0.013]]) {
        for (const sign of [-1, 1]) {
            birds.triangle([x, y - 0.003, 0.5], [x + sign * wide, y + 0.008, 0.5], [x + sign * wide * 0.5, y + 0.007, 0.5], 0x3F4A5E, 0.8);
            birds.triangle([x, y - 0.003, 0.5], [x + sign * wide * 0.5, y + 0.007, 0.5], [x, y + 0.002, 0.5], 0x3F4A5E, 0.8);
        }
    }
    g.birds = add(birds, -4);

    // A ridge: a line of heights that repeats every 2 across, so it can slide without end. It hangs a little
    // nearer than what is behind it.
    const ridge = (heightAt, crest, depth, order) => {
        const builder = gather(THREE);
        const steps = 96;
        const from = -3, to = 3;
        for (let step = 0; step < steps; step++) {
            const u0 = from + (to - from) * step / steps, u1 = from + (to - from) * (step + 1) / steps;
            const h0 = heightAt(u0), h1 = heightAt(u1);
            builder.corner([u0, -0.05, depth], haze);
            builder.corner([u1, -0.05, depth], haze);
            builder.corner([u1, h1, depth], crest);
            builder.corner([u0, -0.05, depth], haze);
            builder.corner([u1, h1, depth], crest);
            builder.corner([u0, h0, depth], crest);
        }
        return add(builder, order);
    };
    const turn = Math.PI;
    g.farRidge = ridge(
        u => 0.5 + 0.17 * Math.sin(u * turn + 0.6) + 0.09 * Math.sin(u * turn * 2 + 2.1) + 0.04 * Math.sin(u * turn * 5 + 0.3),
        mixed(LIGHT.farRidge, 0.3), 1, -3);
    g.midRidge = ridge(
        u => 0.34 + 0.1 * Math.sin(u * turn + 2.2) + 0.06 * Math.sin(u * turn * 3 + 0.4) + 0.02 * Math.sin(u * turn * 7 + 1.9),
        mixed(LIGHT.midRidge, 0.22), 1.5, -2);
    g.nearRidge = ridge(
        u => 0.2 + 0.07 * Math.sin(u * turn + 3.9) + 0.045 * Math.sin(u * turn * 3 + 1.2) + 0.028 * Math.abs(Math.sin(u * turn * 11)) + 0.018 * Math.abs(Math.sin(u * turn * 17 + 0.8)),
        mixed(LIGHT.nearRidge, 0.12), 2, -1);
}

function buildScene(THREE, g) {
    g.scene = new THREE.Scene();
    g.hazeColour = new THREE.Color(LIGHT.haze);
    g.scene.background = new THREE.Color(LIGHT.skyTop);
    g.scene.fog = new THREE.Fog(LIGHT.haze, 1, 2);
    g.solidMaterial = windy(new THREE.MeshLambertMaterial({ vertexColors: true, flatShading: true }), g);

    // The camera: where it stands, how far it looks down and how wide it sees are set each frame from the render
    // list. Nothing else moves it.
    g.camera = new THREE.PerspectiveCamera(40, 1, 12, 6000);
    g.camera.rotation.order = "YXZ";
    g.far = new THREE.Group();
    g.camera.add(g.far);
    g.scene.add(g.camera);

    // The light: a low warm sun, and the sky as the light of the shade, so that a shadow is cool and never black.
    g.scene.add(new THREE.AmbientLight(0xFFF4E0, 0.35));
    g.scene.add(new THREE.HemisphereLight(LIGHT.sky, LIGHT.earth, 1.25));
    g.sun = new THREE.DirectionalLight(LIGHT.sun, 2.9);
    g.sun.castShadow = true;
    g.sun.shadow.mapSize.set(SHADOW_MAP, SHADOW_MAP);
    g.sun.shadow.radius = 4;
    g.sun.shadow.bias = -0.0015;
    g.sun.shadow.normalBias = 0.8;
    g.scene.add(g.sun, g.sun.target);
    g.sunDirection = new THREE.Vector3(...SUN).normalize();
    g.sunRight = new THREE.Vector3(0, 1, 0).cross(g.sunDirection).normalize();
    g.sunUp = g.sunDirection.clone().cross(g.sunRight).normalize();

    buildGround(THREE, g);
    buildProps(THREE, g);
    buildCover(THREE, g);
    buildAir(THREE, g);
    buildFarLayer(THREE, g);

    // Under each character a soft dark patch on the ground, drawn out toward the east as the shadows are: the
    // figures cast no shadow of their shape.
    const patch = new THREE.CircleGeometry(1, 24);
    patch.rotateX(-Math.PI / 2);
    for (let index = 0; index < ANCHOR_COUNT; index++) {
        const material = new THREE.MeshBasicMaterial({ color: 0x1B2A3A, transparent: true, opacity: 0.26, depthWrite: false, fog: false });
        const mesh = new THREE.Mesh(patch, material);
        mesh.scale.set(15, 1, 5);
        mesh.rotation.y = Math.atan2(g.sunDirection.z, -g.sunDirection.x);
        mesh.renderOrder = 3;
        mesh.visible = false;
        mesh.frustumCulled = false;
        g.scene.add(mesh);
        g.patches.push(mesh);
    }
}

function resize() {
    game.shell.measure();
    game.renderer.setPixelRatio(game.shell.ratio);
    game.renderer.setSize(game.shell.viewWidth, game.shell.viewHeight, false);
    game.shadowKey = "";
}

function partMesh(index) {
    const THREE = game.three;
    let mesh = game.parts[index];
    if (!mesh) {
        mesh = new THREE.Mesh();
        mesh.matrixAutoUpdate = false;
        mesh.frustumCulled = false;
        mesh.renderOrder = 10 + index;
        game.scene.add(mesh);
        game.parts[index] = mesh;
    }
    return mesh;
}

// The point of the ground the camera sees at a point of its picture (-1 to 1 across and up).
function groundUnder(g, across, up, target) {
    target.set(across, up, 0.5).unproject(g.camera).sub(g.camera.position);
    return target.multiplyScalar(-g.camera.position.y / target.y).add(g.camera.position);
}

// The shafts of light of one crown: each falls along the light of the sun, through a point of the crown, to the
// ground, as a strip that is turned toward the viewer about its own line. They are faint, and breathe slowly.
function shaftsOf(g, index, x, z, grown, count, tilt, seconds) {
    const sun = g.sunDirection;
    const sin = Math.sin(tilt), cos = Math.cos(tilt);
    for (let shaft = 0; shaft < count && index < SHAFT_CAPACITY; shaft++) {
        const first = scatter(Math.round(x) + shaft * 17, Math.round(z)), second = scatter(Math.round(z) + shaft * 29, Math.round(x));
        const throughX = x + (first - 0.5) * 46 * grown, throughZ = z + (second - 0.3) * 30 * grown;
        const throughY = (44 + 26 * first) * grown * (count > 1 ? 1.5 : 1);
        const down = throughY / sun.y;
        const length = down + 16;
        const alongX = sun.x * length, alongY = sun.y * length, alongZ = sun.z * length;
        // Across the shaft: at right angles to its line and to the line of sight.
        let acrossX = -alongY * cos + alongZ * sin, acrossY = alongX * cos, acrossZ = -alongX * sin;
        const wide = (5 + 6 * second) * grown / Math.hypot(acrossX, acrossY, acrossZ);
        acrossX *= wide; acrossY *= wide; acrossZ *= wide;
        g.matrix.set(
            acrossX, alongX, 0, throughX - sun.x * down,
            acrossY, alongY, 0, 0.6,
            acrossZ, alongZ, 1, throughZ - sun.z * down,
            0, 0, 0, 1);
        g.shafts.setMatrixAt(index, g.matrix);
        const strength = (0.03 + 0.025 * first) * (0.8 + 0.2 * Math.sin(seconds * 0.5 + second * 6.28));
        g.shafts.setColorAt(index, g.colour.setHex(LIGHT.shaft).multiplyScalar(strength));
        index++;
    }
    return index;
}

// The glows and the wings of one frame: only those near what the camera shows, each placed by the one clock.
function air(g, list, tilt, seconds) {
    const sin = Math.sin(tilt), cos = Math.cos(tilt);
    const focusX = list[EYE_X], focusZ = list[EYE_Y] - list[EYE_HEIGHT] * cos / sin;
    const seen = (x, z) => Math.abs(x - focusX) < 620 && z > focusZ - 900 && z < focusZ + 280;
    const centred = (value, period) => value - period * Math.round(value / period);
    let glows = 0;
    // A glow faces the viewer: across is east, up leans back by the tilt of the camera.
    const glow = (x, y, z, radius, hex, strength) => {
        if (glows >= GLOW_CAPACITY || strength <= 0.004) {
            return;
        }
        g.matrix.set(
            radius, 0, 0, x,
            0, radius * cos, radius * sin, y,
            0, -radius * sin, radius * cos, z,
            0, 0, 0, 1);
        g.glows.setMatrixAt(glows, g.matrix);
        g.glows.setColorAt(glows++, g.colour.setHex(hex).multiplyScalar(strength));
    };

    // The two trees in the midst of the garden: about the tree of life a warm light that is calm and steady, about
    // the tree of the knowledge of good and evil a cool one that is never quite still (design, section 5.2).
    const standing = kind => {
        const scenery = g.scenery[kind];
        return scenery && scenery.count > 0 ? scenery.mesh.instanceMatrix.array : null;
    };
    const life = standing(SCENERY.treeOfLife);
    if (life) {
        glow(life[12], 94, life[14] - 6, 88, 0xFFE2A0, 0.09);
        glow(life[12], 96, life[14] + 40, 44, 0xFFF4CC, 0.035);
        // Its blossoms glow, each a small pale light of its own.
        for (const [x, y, z] of g.blossoms) {
            glow(life[12] + x, y + 0.8, life[14] + z + 2.5, 3.8, 0xFFF6D6, 0.42);
        }
    }
    const knowledge = standing(SCENERY.treeOfKnowledge);
    if (knowledge) {
        const unrest = 0.74 + 0.26 * Math.sin(seconds * 2.3) * Math.sin(seconds * 0.71 + 1);
        glow(knowledge[12], 70, knowledge[14] - 6, 80, 0x93A8FF, 0.085 * unrest);
    }

    for (const puff of g.mist) {
        if (seen(puff.x, puff.z)) {
            glow(puff.x + 14 * Math.sin(seconds * 0.13 + puff.phase), 9 + 5 * Math.sin(seconds * 0.21 + puff.phase * 2), puff.z + 8 * Math.cos(seconds * 0.1 + puff.phase),
                46 + 9 * Math.sin(seconds * 0.17 + puff.phase), LIGHT.mist, 0.017);
        }
    }
    let flies = 0;
    for (const fly of g.fireflies) {
        if (flies < FIREFLIES_SEEN && Math.abs(fly.x - focusX) < 520 && fly.z > focusZ - 700 && fly.z < focusZ + 260) {
            const pulse = Math.max(0, Math.sin(seconds * 1.4 + fly.phase * 3));
            glow(fly.x + 6 * Math.sin(seconds * 0.6 + fly.phase), 9 + 5 * Math.sin(seconds * 0.9 + fly.phase * 2), fly.z + 5 * Math.cos(seconds * 0.5 + fly.phase), 3.2, LIGHT.firefly, 0.6 * pulse * pulse);
            flies++;
        }
    }
    for (let grain = 0; grain < POLLEN; grain++) {
        const x = focusX + centred(scatter(grain, 201) * 640 + seconds * (5 + 4 * scatter(grain, 207)) - focusX, 640);
        const z = focusZ - 60 + centred(scatter(grain, 205) * 520 + seconds * (scatter(grain, 211) - 0.5) * 3 - focusZ, 520);
        glow(x, 6 + scatter(grain, 203) * 70 + 4 * Math.sin(seconds * 0.5 + grain), z, 0.9 + 0.7 * scatter(grain, 209), LIGHT.pollen, 0.2 + 0.12 * Math.sin(seconds * 0.8 + grain));
    }
    g.glows.count = glows;
    g.glows.instanceMatrix.needsUpdate = true;
    g.glows.instanceColor.needsUpdate = true;

    let wings = 0;
    for (const butterfly of g.butterflies) {
        if (wings >= BUTTERFLIES * 2 || !seen(butterfly.x, butterfly.z)) {
            continue;
        }
        const along = seconds * 0.5 + butterfly.phase;
        const heading = Math.atan2(24 * Math.cos(along), 20.8 * Math.cos(along * 1.3 + 1));
        const flap = 0.25 + 0.95 * Math.abs(Math.sin(seconds * 9 + butterfly.phase));
        g.vector.set(butterfly.x + 24 * Math.sin(along), 11 + 4 * Math.sin(along * 2.1), butterfly.z + 16 * Math.sin(along * 1.3 + 1));
        for (const side of [-1, 1]) {
            g.euler.set(0, heading, side * flap);
            g.matrix.compose(g.vector, g.turn.setFromEuler(g.euler), g.scale.set(side * 1.25, 1.25, 1.25));
            g.wings.setMatrixAt(wings, g.matrix);
            g.wings.setColorAt(wings++, g.colour.setHex(butterfly.tint));
        }
    }
    g.wings.count = wings;
    g.wings.visible = wings > 0;
    g.wings.instanceMatrix.needsUpdate = true;
    g.wings.instanceColor.needsUpdate = true;
}

function draw(list, seconds) {
    const g = game;
    const THREE = g.three;
    const shell = g.shell;
    const tilt = list[TILT];
    const view = list[FIELD_OF_VIEW];
    const hazeStart = list[HAZE_START];
    const hazeEnd = list[HAZE_END];
    const sin = Math.sin(tilt), cos = Math.cos(tilt);

    // The camera of the game: its eye, its tilt, its field of view. It looks north and down; it has no other turn.
    const camera = g.camera;
    const aspect = shell.viewWidth / shell.viewHeight;
    const degrees = view * 180 / Math.PI;
    if (camera.fov !== degrees || camera.aspect !== aspect) {
        camera.fov = degrees;
        camera.aspect = aspect;
        camera.updateProjectionMatrix();
    }
    camera.position.set(list[EYE_X], list[EYE_HEIGHT], list[EYE_Y]);
    camera.rotation.set(-tilt, 0, 0);
    g.scene.fog.near = hazeStart;
    g.scene.fog.far = hazeEnd;

    // The far layer hangs where the haze closes: from the line the ground reaches there up to the top of the picture.
    const half = Math.tan(view / 2);
    const foot = (hazeEnd - list[EYE_HEIGHT] * sin) * sin / cos - list[EYE_HEIGHT] * cos;
    const top = hazeEnd * half;
    const width = top * aspect;
    g.far.position.set(0, foot, -hazeEnd);
    g.far.scale.set(width, Math.max(1, top - foot), 1);
    const travel = g.motion ? list[EYE_X] : 0;
    const wrap = (value, period) => value - period * Math.round(value / period);
    g.farRidge.position.x = -wrap(travel / FAR_RIDGE_DISTANCE / width, 2);
    g.midRidge.position.x = -wrap(travel * 2 / (FAR_RIDGE_DISTANCE + NEAR_RIDGE_DISTANCE) / width, 2);
    g.nearRidge.position.x = -wrap(travel / NEAR_RIDGE_DISTANCE / width, 2);
    g.clouds.position.x = wrap(seconds * 0.006 - travel / (FAR_RIDGE_DISTANCE * 2) / width, 2);
    g.birds.position.set(wrap(seconds * 0.021 + 0.4, 2.6), 0.62 + 0.02 * Math.sin(seconds * 0.5), 0);
    g.birds.scale.y = 1 + 0.5 * Math.sin(seconds * 6);
    camera.updateMatrixWorld(true);

    // The sun lights what the picture shows of the ground, as far as the haze: it follows in whole texels of its
    // shadow map, so the shadows do not crawl while the camera slides.
    const key = shell.viewWidth + "x" + shell.viewHeight + ":" + list[EYE_HEIGHT] + ":" + hazeEnd;
    const farUp = foot / top;
    const middle = groundUnder(g, 0, (farUp - 1) / 2, g.vector);
    const centreX = middle.x, centreZ = middle.z;
    if (g.shadowKey !== key) {
        g.shadowKey = key;
        let reach = 0;
        for (const [across, up] of [[-1, -1], [1, -1], [-1, farUp], [1, farUp]]) {
            const corner = groundUnder(g, across, up, g.corner);
            reach = Math.max(reach, Math.hypot(corner.x - centreX, corner.z - centreZ));
        }
        reach += 64;
        const shadow = g.sun.shadow.camera;
        shadow.left = shadow.bottom = -reach;
        shadow.right = shadow.top = reach;
        shadow.near = 1;
        shadow.far = 2400;
        shadow.updateProjectionMatrix();
        g.shadowTexel = (2 * reach) / SHADOW_MAP;
    }
    const target = g.sun.target.position.set(centreX, 0, centreZ);
    const alongRight = target.dot(g.sunRight), alongUp = target.dot(g.sunUp);
    target.addScaledVector(g.sunRight, Math.round(alongRight / g.shadowTexel) * g.shadowTexel - alongRight);
    target.addScaledVector(g.sunUp, Math.round(alongUp / g.shadowTexel) * g.shadowTexel - alongUp);
    g.sun.position.copy(target).addScaledVector(g.sunDirection, 1200);

    // Each character: the one depth of all its parts (that of the top of a figure standing upright on its feet), and
    // the haze of the place it stands on.
    for (let index = 0; index < ANCHOR_COUNT; index++) {
        const feetX = list[ANCHORS + index * 2], feetZ = list[ANCHORS + index * 2 + 1];
        const figure = g.figures[index];
        figure.depth.value = g.vector.set(feetX, list[FIGURE_DEPTH_HEIGHT], feetZ).project(camera).z * 0.5 + 0.5;
        const depth = list[EYE_HEIGHT] * sin + (list[EYE_Y] - feetZ) * cos;
        const share = Math.min(1, Math.max(0, (depth - hazeStart) / (hazeEnd - hazeStart)));
        figure.haze.value = share * share * (3 - 2 * share);
    }

    for (const scenery of g.scenery) {
        if (scenery) {
            scenery.count = 0;
        }
    }
    let parts = 0;
    let shafts = 0;
    const count = list[0];
    const matrix = g.matrix;
    for (let entry = 0; entry < count; entry++) {
        const at = HEADER + entry * ENTRY;
        const image = g.atlas[list[at]];
        if (!image) {
            continue;
        }
        const character = list[at + CHARACTER];
        const e = list[at + 5], f = list[at + 6];
        if (character === 0) {
            // Scenery: what stands on a tile, where the list puts its foot. It turns and grows by its place.
            const kind = g.kinds[list[at]];
            const scenery = kind > 0 ? sceneryOf(THREE, g, kind) : null;
            if (!scenery || scenery.count >= TREE_CAPACITY) {
                continue;
            }
            const first = scatter(Math.round(e), Math.round(f)), second = scatter(Math.round(f) + 7, Math.round(e) + 3);
            const grown = scenery.special ? 1 : scenery.grow[0] + scenery.grow[1] * first;
            g.euler.set(0, scenery.special ? 0 : second * Math.PI * 2, 0);
            g.scale.set(grown, grown * (scenery.special ? 1 : 0.9 + 0.25 * second), grown);
            matrix.compose(g.vector.set(e, 0, f), g.turn.setFromEuler(g.euler), g.scale);
            scenery.mesh.setMatrixAt(scenery.count++, matrix);
            if (scenery.shafts && shafts < SHAFT_CAPACITY && (scenery.special || first < 0.6)) {
                shafts = shaftsOf(g, shafts, e, f, grown, scenery.special ? 2 : 1, tilt, seconds);
            }
            continue;
        }

        // A part of a character, in the plane of that character: the plane stands on the feet and faces the camera
        // squarely (it leans back by the camera's tilt). Across the plane is east, up the plane is up the picture.
        // The transform is the one the canvas draws the part with; the list's order is the order of painting.
        const feet = list[ANCHORS + (character - 1) * 2 + 1];
        const flat = flatOf(THREE, image);
        const mesh = partMesh(parts++);
        mesh.geometry = flat.geometry;
        mesh.material = figureMaterial(THREE, g, character, flat.colour);
        mesh.visible = true;
        mesh.matrix.set(
            list[at + 1], -list[at + 3], 0, e,
            -list[at + 2] * cos, list[at + 4] * cos, sin, (feet - f) * cos,
            list[at + 2] * sin, -list[at + 4] * sin, cos, feet - (feet - f) * sin,
            0, 0, 0, 1);
        mesh.matrixWorldNeedsUpdate = true;
    }
    for (let index = parts; index < g.parts.length; index++) {
        g.parts[index].visible = false;
    }
    for (const scenery of g.scenery) {
        if (scenery) {
            scenery.mesh.count = scenery.count;
            scenery.mesh.visible = scenery.count > 0;
            scenery.mesh.instanceMatrix.needsUpdate = true;
        }
    }
    g.shafts.count = shafts;
    g.shafts.visible = shafts > 0;
    g.shafts.instanceMatrix.needsUpdate = true;
    g.shafts.instanceColor.needsUpdate = true;
    for (let index = 0; index < ANCHOR_COUNT; index++) {
        const patch = g.patches[index];
        patch.visible = parts > 0;
        patch.position.set(list[ANCHORS + index * 2] - g.sunDirection.x * 8, 1.6, list[ANCHORS + index * 2 + 1] - 1 - g.sunDirection.z * 8);
        // A character on the stones of a crossing sends rings over the water.
        const tileX = Math.floor(list[ANCHORS + index * 2] / g.tileSize), tileY = Math.floor((list[ANCHORS + index * 2 + 1] - 8) / g.tileSize);
        const onStones = parts > 0 && tileX >= 0 && tileY >= 0 && tileX < g.mapWidth && tileY < g.mapHeight && g.tiles[tileY * g.mapWidth + tileX] === KIND.crossing;
        g.rings.value[index].set(list[ANCHORS + index * 2], list[ANCHORS + index * 2 + 1], onStones ? 0.9 : 0);
    }
    air(g, list, tilt, seconds);
    g.time.value = seconds;


    g.renderer.render(g.scene, camera);
    for (let index = 0; index < ANCHOR_COUNT * 2; index++) {
        g.lastAnchors[index] = list[ANCHORS + index];
    }
    g.drawCalls = g.renderer.info.render.calls;
    g.triangles = g.renderer.info.render.triangles;
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
    if (!game) {
        return;
    }

    // Everything that moves by itself moves with one clock; under prefers-reduced-motion that clock stands.
    if (game.motion) {
        game.motionSeconds += Math.min(0.1, Math.max(0, (now - (game.lastNow ?? now)) / 1000));
    }
    game.lastNow = now;

    if (!game.lost) {
        draw(list, game.motionSeconds);
    }
    game.shell.publish(list, { motion: game.motion ? "on" : "off" });
}

// Starts this renderer on the canvas of the page. False when the browser has no WebGL 2: nothing was started,
// Three.js was not asked for, and the canvas is free for the canvas renderer.
export async function attach(listView, inputView, tiles, ground, atlas, kinds, cover, verdicts, mapWidth, mapHeight, tileSize, backdrop) {
    detach();
    const page = await openPage();
    if (!hasWebGL()) {
        return false;
    }
    const THREE = await import("../lib/three/three.module.min.js");
    const antialias = (window.devicePixelRatio || 1) < 2;
    let renderer;
    try {
        renderer = new THREE.WebGLRenderer({ canvas: page.canvas, antialias, alpha: false, stencil: false, powerPreference: "high-performance" });
    } catch {
        return false;
    }
    renderer.shadowMap.enabled = true;
    renderer.shadowMap.type = THREE.PCFSoftShadowMap;

    const reduced = window.matchMedia("(prefers-reduced-motion: reduce)");
    const shell = createShell({ ...page, listView, inputView, verdicts, projection: PERSPECTIVE });
    const g = game = {
        three: THREE, renderer, shell, antialias,
        root: page.root, canvas: page.canvas,
        tiles, ground, kinds, cover, mapWidth, mapHeight, tileSize, backdrop,
        // After the four numbers of each of the ten tile kinds, the ground lists the colours of the drifts of flowers.
        driftColours: 40,
        atlas: readAtlas(atlas),
        scenery: [], parts: [], patches: [], coverMeshes: [], mist: [], fireflies: [], butterflies: [], blossoms: [],
        time: { value: 0 },
        rings: { value: Array.from({ length: ANCHOR_COUNT }, () => new THREE.Vector3()) },
        scale: new THREE.Vector3(), turn: new THREE.Quaternion(), colour: new THREE.Color(), tint: new THREE.Color(),
        figures: Array.from({ length: ANCHOR_COUNT }, () => ({ depth: { value: 0 }, haze: { value: 0 } })),
        figureMaterials: new Map(),
        matrix: new THREE.Matrix4(), vector: new THREE.Vector3(), corner: new THREE.Vector3(), euler: new THREE.Euler(),
        motion: !reduced.matches, motionSeconds: 0, lastNow: undefined,
        frameRequest: 0, shadowTexel: 1, shadowKey: "", drawCalls: 0, triangles: 0, lastAnchors: new Float64Array(ANCHOR_COUNT * 2),
        lost: false, lostTimer: 0,
    };
    buildScene(THREE, g);
    shell.listen(reduced, "change", () => { g.motion = !reduced.matches; });

    // A lost WebGL context: the browser is asked to give it back, and Three.js then builds everything anew by
    // itself. If it does not come back in time, the game is told, and it goes on with the canvas renderer.
    shell.listen(page.canvas, "webglcontextlost", event => {
        event.preventDefault();
        g.lost = true;
        page.root.dataset.rendererContext = "lost";
        clearTimeout(g.lostTimer);
        g.lostTimer = setTimeout(() => {
            if (game === g && g.lost) {
                page.interop.RendererLost();
            }
        }, RESTORE_WAIT_MS);
    });
    shell.listen(page.canvas, "webglcontextrestored", () => {
        clearTimeout(g.lostTimer);
        g.lost = false;
        g.shadowKey = "";
        page.root.dataset.rendererContext = "restored";
    });
    g.frameRequest = requestAnimationFrame(tick);
    page.root.dataset.ready = "true";
    return true;
}

export function detach() {
    if (!game) {
        return;
    }
    const g = game;
    game = null;
    cancelAnimationFrame(g.frameRequest);
    clearTimeout(g.lostTimer);
    g.shell.dispose();
    g.scene.traverse(object => {
        if (object.geometry) {
            object.geometry.dispose();
        }
        for (const material of [].concat(object.material ?? [])) {
            if (material.map) {
                material.map.dispose();
            }
            material.dispose();
        }
    });
    for (const image of g.atlas) {
        if (image.flat) {
            image.flat.geometry.dispose();
        }
    }
    for (const material of g.figureMaterials.values()) {
        material.dispose();
    }
    g.renderer.dispose();
    if (!g.lost) {
        g.renderer.forceContextLoss();
    }
}

// What the renderer itself can say of its last frame, for the tests: how much it drew (a bound on these is the guard
// of the frame budget that needs no graphics chip), how far the two ridges of the far layer have slid (in CSS
// pixels), and where its own camera puts points of the garden on the screen (each point: x on the ground, height,
// y on the ground; the answer in CSS pixels of the play area), which the tests compare with the camera of the game.
export function probe(points) {
    if (!game) {
        return null;
    }
    const g = game;
    return {
        drawCalls: g.drawCalls,
        triangles: g.triangles,
        shadowMap: g.sun.shadow.mapSize.x,
        pixelRatio: g.renderer.getPixelRatio(),
        antialias: g.antialias,
        contextLost: g.lost,
        trees: g.scenery.reduce((sum, scenery) => sum + (scenery && scenery.tree ? scenery.count : 0), 0),
        plants: g.scenery.reduce((sum, scenery) => sum + (scenery && !scenery.tree ? scenery.count : 0), 0),
        shafts: g.shafts.count,
        glows: g.glows.count,
        wings: g.wings.count,
        seconds: g.motionSeconds,
        anchors: Array.from({ length: ANCHOR_COUNT }, (_, index) => [g.lastAnchors[index * 2], g.lastAnchors[index * 2 + 1]]),
        parts: g.parts.filter(mesh => mesh.visible).length,
        parallax: [g.farRidge.position.x * g.shell.viewWidth / 2, g.nearRidge.position.x * g.shell.viewWidth / 2],
        screen: (points ?? []).map(([x, height, y]) => {
            g.vector.set(x, height, y).project(g.camera);
            return [(g.vector.x + 1) / 2 * g.shell.viewWidth, (1 - g.vector.y) / 2 * g.shell.viewHeight];
        }),
    };
}
