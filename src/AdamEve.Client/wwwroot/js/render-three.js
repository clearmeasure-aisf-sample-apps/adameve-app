// The renderer of the garden (design, section 7.1 and decision D18): Three.js on WebGL 2, with a perspective camera.
//
// It decides as little as the canvas renderer does. The camera is the game's: each frame the render list carries the
// eye, the tilt and the field of view of AdamEve.Core.World.PerspectiveCamera, and the camera here is set from those
// numbers and from nothing else. It looks north and down, it follows the player, and nothing here can turn it or
// zoom it. The game culls and reads a tap with the same camera, so what is listed is what can be seen and a tap
// names the ground under it.
//
// What the eye reads as depth: nearer things are larger and cross the screen faster; the ground has height (the
// thicket, the stones, the river's banks); a sun casts soft shadows; haze grows with distance; and where the haze
// closes stands the far layer, hills and sky, which slides less than anything on the ground (layered parallax: it is
// not a place of the map). Sway, shimmer and that sliding stand still under prefers-reduced-motion.
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

import { HEADER, ENTRY, CHARACTER, ANCHORS, ANCHOR_COUNT, PERSPECTIVE, createShell, openPage, readAtlas } from "./shell.js";

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

const WATER_LEVEL = -3;
const THICKET_HEIGHT = 12;
// The thicket goes on beyond the edge of the map: this many tiles as the uneven mass it is, then flat.
const THICKET_BEYOND = 6;
const TREE_CAPACITY = 64;
const ELLIPSE_SEGMENTS = 64;
// What keeps a frame light enough for a phone: one shadow map of this size, device pixels capped at 2 (shell.js),
// antialiasing only below that, trees and flowers drawn as instances.
const SHADOW_MAP = 1024;
const SUN = [-0.72, 1.0, 0.42];
// How long a lost WebGL context may take to come back before the game goes on with the canvas renderer.
const RESTORE_WAIT_MS = 2000;
// The far layer slides as if it stood this many times farther away than the line where the haze closes.
const FAR_RIDGE_DISTANCE = 6;
const NEAR_RIDGE_DISTANCE = 3;
// The bank of the river has the colour of a trunk. The rest is light and air, not art: the sun, the sky, the haze,
// the far hills.
const LIGHT = { bank: 0x7A5A3A, haze: 0xDCEBD2, sun: 0xFFF2D8, sky: 0xD6ECFF, earth: 0x4E7A44, skyTop: 0xA9D3F0, farRidge: 0x9DBFB4, nearRidge: 0x6FA07A };

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

let game = null;

function hasWebGL() {
    try {
        const probe = document.createElement("canvas");
        return typeof WebGL2RenderingContext === "function" && probe.getContext("webgl2") !== null;
    } catch {
        return false;
    }
}

// A number in [0, 1) from two whole numbers: the same garden every time, with no chance in it.
function scatter(x, y) {
    let n = (Math.imul(x, 374761393) + Math.imul(y, 668265263)) | 0;
    n = Math.imul(n ^ (n >>> 13), 1274126177);
    return ((n ^ (n >>> 16)) >>> 0) / 4294967296;
}

// Gathers triangles with a colour for each corner into one geometry. Every solid is shaded flat, from its faces.
function gather(THREE) {
    const positions = [];
    const colours = [];
    const colour = new THREE.Color();
    const point = new THREE.Vector3();
    return {
        corner(at, hex) {
            colour.setHex(hex);
            positions.push(at[0], at[1], at[2]);
            colours.push(colour.r, colour.g, colour.b);
        },
        triangle(a, b, c, hex) {
            this.corner(a, hex);
            this.corner(b, hex);
            this.corner(c, hex);
        },
        quad(a, b, c, d, hex) {
            this.triangle(a, b, c, hex);
            this.triangle(a, c, d, hex);
        },
        solid(geometry, matrix, hex) {
            const plain = geometry.index ? geometry.toNonIndexed() : geometry;
            const from = plain.getAttribute("position");
            colour.setHex(hex);
            for (let index = 0; index < from.count; index++) {
                point.fromBufferAttribute(from, index).applyMatrix4(matrix);
                positions.push(point.x, point.y, point.z);
                colours.push(colour.r, colour.g, colour.b);
            }
            if (plain !== geometry) {
                plain.dispose();
            }
            geometry.dispose();
        },
        build() {
            const geometry = new THREE.BufferGeometry();
            geometry.setAttribute("position", new THREE.Float32BufferAttribute(positions, 3));
            geometry.setAttribute("color", new THREE.Float32BufferAttribute(colours, 3));
            return geometry;
        },
    };
}

// The ground from the tiles: grass and flowers at height 0, the river a little lower between its banks, the thicket
// as a raised, uneven mass with a wall wherever it ends. Beyond the map the thicket goes on. x is east, y is up,
// z is south, in the logical pixels of the map.
function buildGround(THREE, g) {
    const size = g.tileSize;
    const land = gather(THREE);
    const water = gather(THREE);
    const kindAt = (x, y) => (x < 0 || y < 0 || x >= g.mapWidth || y >= g.mapHeight ? KIND.thicket : g.tiles[y * g.mapWidth + x]);
    const wet = kind => kind === KIND.water || kind === KIND.crossing;
    const cornerHeight = (x, y) => THICKET_HEIGHT + 7 * scatter(x, y);
    const thicket = KIND.thicket * 4;
    for (let y = -THICKET_BEYOND; y < g.mapHeight + THICKET_BEYOND; y++) {
        for (let x = -THICKET_BEYOND; x < g.mapWidth + THICKET_BEYOND; x++) {
            const kind = kindAt(x, y);
            const style = kind * 4;
            const colour = g.ground[style + ((x + y) & 1)];
            const x0 = x * size, x1 = x0 + size, z0 = y * size, z1 = z0 + size;
            if (wet(kind)) {
                water.quad([x0, WATER_LEVEL, z0], [x0, WATER_LEVEL, z1], [x1, WATER_LEVEL, z1], [x1, WATER_LEVEL, z0], colour);
                // The banks the camera can see: the north shore, and the shores to the west and to the east.
                if (!wet(kindAt(x, y - 1))) {
                    land.quad([x0, WATER_LEVEL, z0], [x1, WATER_LEVEL, z0], [x1, 0, z0], [x0, 0, z0], LIGHT.bank);
                }
                if (!wet(kindAt(x - 1, y))) {
                    land.quad([x0, WATER_LEVEL, z1], [x0, WATER_LEVEL, z0], [x0, 0, z0], [x0, 0, z1], LIGHT.bank);
                }
                if (!wet(kindAt(x + 1, y))) {
                    land.quad([x1, WATER_LEVEL, z0], [x1, WATER_LEVEL, z1], [x1, 0, z1], [x1, 0, z0], LIGHT.bank);
                }
            } else if (kind === KIND.thicket) {
                const h00 = cornerHeight(x, y), h10 = cornerHeight(x + 1, y), h01 = cornerHeight(x, y + 1), h11 = cornerHeight(x + 1, y + 1);
                const middle = [x0 + size / 2, THICKET_HEIGHT + 6 + 8 * scatter(x + 977, y + 131), z0 + size / 2];
                const a = [x0, h00, z0], b = [x0, h01, z1], c = [x1, h11, z1], d = [x1, h10, z0];
                const other = g.ground[style + ((x + y + 1) & 1)];
                land.triangle(a, b, middle, colour);
                land.triangle(b, c, middle, other);
                land.triangle(c, d, middle, colour);
                land.triangle(d, a, middle, other);
                // The walls the camera can see: toward the south, the west and the east.
                if (kindAt(x, y + 1) !== KIND.thicket) {
                    land.quad([x0, 0, z1], [x1, 0, z1], c, b, g.ground[style + 1]);
                }
                if (kindAt(x - 1, y) !== KIND.thicket) {
                    land.quad([x0, 0, z0], [x0, 0, z1], b, a, g.ground[style + 1]);
                }
                if (kindAt(x + 1, y) !== KIND.thicket) {
                    land.quad([x1, 0, z1], [x1, 0, z0], d, c, g.ground[style + 1]);
                }
            } else {
                land.quad([x0, 0, z0], [x0, 0, z1], [x1, 0, z1], [x1, 0, z0], colour);
            }
        }
    }

    // Beyond that the thicket is flat, as far as the haze lets anything be seen.
    const far = 6000;
    const top = THICKET_HEIGHT + 8;
    const west = -THICKET_BEYOND * size, east = (g.mapWidth + THICKET_BEYOND) * size;
    const north = -THICKET_BEYOND * size, south = (g.mapHeight + THICKET_BEYOND) * size;
    const flat = (x0, z0, x1, z1) => land.quad([x0, top, z0], [x0, top, z1], [x1, top, z1], [x1, top, z0], g.ground[thicket]);
    flat(west - far, north - far, east + far, north);
    flat(west - far, south, east + far, south + far);
    flat(west - far, north, west, south);
    flat(east, north, east + far, south);

    const landMesh = new THREE.Mesh(land.build(), g.solidMaterial);
    landMesh.receiveShadow = true;
    landMesh.castShadow = true;
    landMesh.frustumCulled = false;
    const waterGeometry = water.build();
    const positions = waterGeometry.getAttribute("position");
    const uv = new Float32Array(positions.count * 2);
    for (let index = 0; index < positions.count; index++) {
        uv[index * 2] = positions.getX(index) / 96;
        uv[index * 2 + 1] = positions.getZ(index) / 96;
    }
    waterGeometry.setAttribute("uv", new THREE.BufferAttribute(uv, 2));
    const waterMesh = new THREE.Mesh(waterGeometry, g.solidMaterial);
    waterMesh.receiveShadow = true;
    waterMesh.frustumCulled = false;
    g.scene.add(landMesh, waterMesh);

    // The shimmer of the water: two layers of one small pattern of light, made here, drifting across each other.
    const side = 64;
    const pixels = new Uint8Array(side * side * 4);
    for (let y = 0; y < side; y++) {
        for (let x = 0; x < side; x++) {
            const u = (x / side) * Math.PI * 2, v = (y / side) * Math.PI * 2;
            const wave = Math.sin(u * 2 + Math.sin(v * 3) * 1.4) * Math.sin(v * 2 + Math.sin(u) * 1.8);
            const light = Math.max(0, wave - 0.55) / 0.45;
            const value = Math.round(255 * light * light);
            pixels.set([value, value, value, 255], (y * side + x) * 4);
        }
    }
    for (const layer of [0, 1]) {
        const pattern = new THREE.DataTexture(pixels, side, side, THREE.RGBAFormat);
        pattern.wrapS = pattern.wrapT = THREE.RepeatWrapping;
        pattern.magFilter = THREE.LinearFilter;
        pattern.minFilter = THREE.LinearMipmapLinearFilter;
        pattern.generateMipmaps = true;
        pattern.colorSpace = THREE.SRGBColorSpace;
        pattern.needsUpdate = true;
        if (layer === 1) {
            pattern.repeat.set(1.7, 1.3);
        }
        const material = new THREE.MeshBasicMaterial({
            map: pattern, transparent: true, opacity: layer === 0 ? 0.3 : 0.2,
            blending: THREE.AdditiveBlending, depthWrite: false, fog: false,
        });
        const shimmer = new THREE.Mesh(waterGeometry, material);
        shimmer.position.y = 0.2 + layer * 0.1;
        shimmer.renderOrder = 1 + layer;
        shimmer.frustumCulled = false;
        g.scene.add(shimmer);
        g.shimmer.push(pattern);
    }
}

// What stands on the ground and is not a tree: the stones of the crossing, the resting place and the flowers,
// each from the mark the canvas paints flat on that tile.
function buildProps(THREE, g) {
    const size = g.tileSize;
    const still = gather(THREE);
    const matrix = new THREE.Matrix4();
    const flowers = [];
    for (let y = 0; y < g.mapHeight; y++) {
        for (let x = 0; x < g.mapWidth; x++) {
            const kind = g.tiles[y * g.mapWidth + x];
            const mark = g.ground[kind * 4 + 3];
            const colour = g.ground[kind * 4 + 2];
            const centreX = (x + 0.5) * size, centreZ = (y + 0.5) * size;
            if (kind === KIND.crossing) {
                const height = 1.5 - WATER_LEVEL;
                matrix.makeTranslation(centreX, WATER_LEVEL + height / 2, centreZ);
                still.solid(new THREE.CylinderGeometry(mark / 2 - 1.5, mark / 2, height, 9), matrix, colour);
            } else if (kind === KIND.restingPlace) {
                matrix.makeTranslation(centreX, 1.25, centreZ);
                still.solid(new THREE.BoxGeometry(mark, 2.5, mark), matrix, colour);
            } else if (kind === KIND.flowers) {
                for (let blossom = 0; blossom < 4; blossom++) {
                    const offsetX = blossom === 0 ? 0 : (scatter(x * 4 + blossom, y) - 0.5) * (size - 8);
                    const offsetZ = blossom === 0 ? 0 : (scatter(x, y * 4 + blossom) - 0.5) * (size - 8);
                    flowers.push({ x: centreX + offsetX, z: centreZ + offsetZ, radius: mark / 2 * (blossom === 0 ? 1 : 0.75), colour });
                }
            }
        }
    }

    const stillMesh = new THREE.Mesh(still.build(), g.solidMaterial);
    stillMesh.castShadow = true;
    stillMesh.receiveShadow = true;
    stillMesh.frustumCulled = false;
    g.scene.add(stillMesh);

    if (flowers.length > 0) {
        const blossoms = new THREE.InstancedMesh(new THREE.IcosahedronGeometry(1, 0), new THREE.MeshLambertMaterial({ flatShading: true }), flowers.length);
        blossoms.instanceMatrix.setUsage(THREE.DynamicDrawUsage);
        const colour = new THREE.Color();
        flowers.forEach((flower, index) => {
            matrix.makeScale(flower.radius, flower.radius * 0.8, flower.radius).setPosition(flower.x, 3.5, flower.z);
            blossoms.setMatrixAt(index, matrix);
            blossoms.setColorAt(index, colour.setHex(flower.colour));
        });
        blossoms.castShadow = true;
        blossoms.frustumCulled = false;
        g.scene.add(blossoms);
        g.flowers = { mesh: blossoms, places: flowers };
    }
}

// A tree from the flat shapes of its image: a rectangle is a trunk, the first ellipse the crown, and every further
// ellipse a smaller mass on the side of the crown that faces the viewer, where the flat image has it.
function treeOf(THREE, g, image) {
    if (image.tree) {
        return image.tree;
    }
    const trunk = gather(THREE);
    const crown = gather(THREE);
    const matrix = new THREE.Matrix4();
    let main = null;
    let pivot = 0;
    for (const shape of image.shapes) {
        const height = -shape.y;
        if (shape.kind === 1) {
            matrix.makeTranslation(shape.x, height, 0);
            trunk.solid(new THREE.CylinderGeometry(shape.width / 2 * 0.8, shape.width / 2, shape.height, 8), matrix, shape.colour);
            pivot = Math.max(pivot, height + shape.height / 2);
            continue;
        }
        const across = shape.width / 2, up = 0.8 * shape.height / 2, deep = 0.6 * shape.height / 2;
        const first = main === null;
        let toward = 0;
        if (first) {
            main = { x: shape.x, height, across, up, deep };
        } else {
            // Up and toward the south alike, to the surface of the crown.
            const k = 1 - ((shape.x - main.x) / main.across) ** 2;
            const a = 1 / main.up ** 2 + 1 / main.deep ** 2;
            const b = 2 * (height - main.height) / main.up ** 2;
            const c = (height - main.height) ** 2 / main.up ** 2 - k;
            const root = b * b - 4 * a * c;
            toward = k > 0 && root > 0 ? (-b + Math.sqrt(root)) / (2 * a) : 0;
        }
        matrix.makeScale(across, up, deep).setPosition(shape.x, height + toward, toward);
        crown.solid(new THREE.IcosahedronGeometry(1, first ? 2 : 1), matrix, shape.colour);
    }

    const make = geometry => {
        const mesh = new THREE.InstancedMesh(geometry, g.solidMaterial, TREE_CAPACITY);
        mesh.instanceMatrix.setUsage(THREE.DynamicDrawUsage);
        mesh.castShadow = true;
        mesh.receiveShadow = true;
        mesh.frustumCulled = false;
        mesh.count = 0;
        g.scene.add(mesh);
        return mesh;
    };
    image.tree = { trunk: make(trunk.build()), crown: make(crown.build()), pivot, count: 0 };
    g.trees.push(image.tree);
    return image.tree;
}

// The flat shape of a part of a character, as the canvas paints it. An ellipse is a polygon drawn around the
// ellipse, never inside it: a cover is never smaller here than the check takes it to be.
function flatOf(THREE, image) {
    if (image.flat) {
        return image.flat;
    }
    const shape = image.shapes[0];
    let geometry;
    if (shape.kind === 1) {
        geometry = new THREE.PlaneGeometry(shape.width, shape.height);
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

// The far layer: sky and two ridges of hills, on a sheet that hangs before the camera where the haze closes. Its
// foot has the colour of the haze, so the ground runs into it without a seam. It is light and air, not a place: it
// belongs to the camera, and its ridges slide a little against the camera's travel.
function buildFarLayer(THREE, g) {
    const haze = LIGHT.haze;
    const mixed = (hex, share) => new THREE.Color(hex).lerp(new THREE.Color(haze), share).getHex();
    const material = new THREE.MeshBasicMaterial({ vertexColors: true, fog: false });
    const add = (builder, order) => {
        const mesh = new THREE.Mesh(builder.build(), material);
        mesh.frustumCulled = false;
        mesh.renderOrder = order;
        g.far.add(mesh);
        return mesh;
    };

    // Across from -1 to 1 is the width of the picture there; up from 0 (the line where the haze closes) to 1 (the
    // top of the picture).
    const sky = gather(THREE);
    sky.quad([-1.2, -0.3, 0], [1.2, -0.3, 0], [1.2, 0.1, 0], [-1.2, 0.1, 0], haze);
    sky.corner([-1.2, 0.1, 0], haze);
    sky.corner([1.2, 0.1, 0], haze);
    sky.corner([1.2, 1.2, 0], LIGHT.skyTop);
    sky.corner([-1.2, 0.1, 0], haze);
    sky.corner([1.2, 1.2, 0], LIGHT.skyTop);
    sky.corner([-1.2, 1.2, 0], LIGHT.skyTop);
    add(sky, -3);

    // A ridge: a line of heights that repeats every 2 across, so it can slide without end. It hangs a little
    // nearer than the sky behind it.
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
        u => 0.46 + 0.16 * Math.sin(u * turn + 0.6) + 0.09 * Math.sin(u * turn * 2 + 2.1) + 0.04 * Math.sin(u * turn * 5 + 0.3),
        mixed(LIGHT.farRidge, 0.45), 1, -2);
    g.nearRidge = ridge(
        u => 0.24 + 0.08 * Math.sin(u * turn + 3.9) + 0.05 * Math.sin(u * turn * 3 + 1.2) + 0.025 * Math.abs(Math.sin(u * turn * 11)) + 0.015 * Math.abs(Math.sin(u * turn * 17 + 0.8)),
        mixed(LIGHT.nearRidge, 0.3), 2, -1);
}

function buildScene(THREE, g) {
    g.scene = new THREE.Scene();
    g.hazeColour = new THREE.Color(LIGHT.haze);
    g.scene.background = new THREE.Color(LIGHT.skyTop);
    g.scene.fog = new THREE.Fog(LIGHT.haze, 1, 2);
    g.solidMaterial = new THREE.MeshLambertMaterial({ vertexColors: true, flatShading: true });

    // The camera: where it stands, how far it looks down and how wide it sees are set each frame from the render
    // list. Nothing else moves it.
    g.camera = new THREE.PerspectiveCamera(40, 1, 12, 6000);
    g.camera.rotation.order = "YXZ";
    g.far = new THREE.Group();
    g.camera.add(g.far);
    g.scene.add(g.camera);

    g.scene.add(new THREE.AmbientLight(0xFFFFFF, 0.55));
    g.scene.add(new THREE.HemisphereLight(LIGHT.sky, LIGHT.earth, 1.0));
    g.sun = new THREE.DirectionalLight(LIGHT.sun, 2.5);
    g.sun.castShadow = true;
    g.sun.shadow.mapSize.set(SHADOW_MAP, SHADOW_MAP);
    g.sun.shadow.radius = 3;
    g.sun.shadow.bias = -0.0015;
    g.sun.shadow.normalBias = 0.6;
    g.scene.add(g.sun, g.sun.target);
    g.sunDirection = new THREE.Vector3(...SUN).normalize();
    g.sunRight = new THREE.Vector3(0, 1, 0).cross(g.sunDirection).normalize();
    g.sunUp = g.sunDirection.clone().cross(g.sunRight).normalize();

    buildGround(THREE, g);
    buildProps(THREE, g);
    buildFarLayer(THREE, g);

    // Under each character a soft dark patch on the ground: the figures cast no shadow of their shape.
    const patch = new THREE.CircleGeometry(1, 24);
    patch.rotateX(-Math.PI / 2);
    for (let index = 0; index < ANCHOR_COUNT; index++) {
        const material = new THREE.MeshBasicMaterial({ color: 0x1B2A18, transparent: true, opacity: 0.28, depthWrite: false, fog: false });
        const mesh = new THREE.Mesh(patch, material);
        mesh.scale.set(11, 1, 5);
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
    g.nearRidge.position.x = -wrap(travel / NEAR_RIDGE_DISTANCE / width, 2);
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

    for (const tree of g.trees) {
        tree.count = 0;
    }
    let parts = 0;
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
            // Scenery: a tree, standing where the list puts the foot of its trunk. The crown sways about its top.
            const tree = treeOf(THREE, g, image);
            if (tree.count >= TREE_CAPACITY) {
                continue;
            }
            matrix.makeTranslation(e, 0, f);
            tree.trunk.setMatrixAt(tree.count, matrix);
            const phase = e * 0.05 + f * 0.07;
            g.euler.set(0.02 * Math.sin(seconds * 0.8 + phase * 1.7), 0, 0.035 * Math.sin(seconds * 1.1 + phase));
            matrix.makeRotationFromEuler(g.euler);
            g.vector.set(0, tree.pivot, 0).applyMatrix4(matrix);
            matrix.setPosition(e - g.vector.x, tree.pivot - g.vector.y, f - g.vector.z);
            tree.crown.setMatrixAt(tree.count, matrix);
            tree.count++;
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
    for (const tree of g.trees) {
        tree.trunk.count = tree.crown.count = tree.count;
        tree.trunk.instanceMatrix.needsUpdate = tree.crown.instanceMatrix.needsUpdate = true;
    }
    for (let index = 0; index < ANCHOR_COUNT; index++) {
        const patch = g.patches[index];
        patch.visible = parts > 0;
        patch.position.set(list[ANCHORS + index * 2], 1.6, list[ANCHORS + index * 2 + 1] - 1);
    }

    if (g.motion && g.flowers) {
        const array = g.flowers.mesh.instanceMatrix.array;
        g.flowers.places.forEach((flower, index) => {
            array[index * 16 + 12] = flower.x + 0.9 * Math.sin(seconds * 1.6 + flower.x * 0.11 + flower.z * 0.07);
        });
        g.flowers.mesh.instanceMatrix.needsUpdate = true;
    }
    g.shimmer[0].offset.set(seconds * 0.021, seconds * 0.013);
    g.shimmer[1].offset.set(-seconds * 0.017, seconds * 0.026);

    g.renderer.render(g.scene, camera);
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

    // The foliage and the water move with the clock; under prefers-reduced-motion the clock of that motion stands.
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
export async function attach(listView, inputView, tiles, ground, atlas, verdicts, mapWidth, mapHeight, tileSize, backdrop) {
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
        tiles, ground, mapWidth, mapHeight, tileSize, backdrop,
        atlas: readAtlas(atlas),
        trees: [], parts: [], patches: [], shimmer: [], flowers: null,
        figures: Array.from({ length: ANCHOR_COUNT }, () => ({ depth: { value: 0 }, haze: { value: 0 } })),
        figureMaterials: new Map(),
        matrix: new THREE.Matrix4(), vector: new THREE.Vector3(), corner: new THREE.Vector3(), euler: new THREE.Euler(),
        motion: !reduced.matches, motionSeconds: 0, lastNow: undefined,
        frameRequest: 0, shadowTexel: 1, shadowKey: "", drawCalls: 0, triangles: 0,
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
        trees: g.trees.reduce((sum, tree) => sum + tree.count, 0),
        parts: g.parts.filter(mesh => mesh.visible).length,
        parallax: [g.farRidge.position.x * g.shell.viewWidth / 2, g.nearRidge.position.x * g.shell.viewWidth / 2],
        screen: (points ?? []).map(([x, height, y]) => {
            g.vector.set(x, height, y).project(g.camera);
            return [(g.vector.x + 1) / 2 * g.shell.viewWidth, (1 - g.vector.y) / 2 * g.shell.viewHeight];
        }),
    };
}
