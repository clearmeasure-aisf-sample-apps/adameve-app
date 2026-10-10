// The Three.js renderer of the trial (docs/spike-threejs.md), chosen with ?renderer=three or in the settings.
//
// It takes the same numbers as render.js and decides as little: the camera, the order of the parts of a character
// and the M1 verdict are the game's. What it adds is depth: the ground is a plane, the trees, the thicket and the
// stones have height and hide each other by where they stand, a sun casts soft shadows, and the foliage and the
// water move a little (not under prefers-reduced-motion).
//
// Rule M1 holds by the construction of the canvas renderer. The camera is fixed and orthographic: it looks north
// and down at 45 degrees, and its picture is stretched so that the ground lies on the screen exactly as the canvas
// draws it, and one unit of height is one unit up the screen. A character is not a body in space: every part the
// game lists for it, its companion foliage and its fail-closed cluster included, is a flat shape in ONE upright
// plane that stands where the character stands, placed by the very transform the canvas would draw it with. On the
// screen the figure is therefore the figure of the canvas, at the same scale, and what covers a zone there covers
// it here. Inside the plane a part listed later lies a hair nearer the viewer, along the line of sight, so no
// scenery can hide a cover and leave what it covers. The figures take no light and no shadow.
//
// Three.js itself is asked for only here, when this renderer attaches and the browser has WebGL. Nothing here asks
// any other origin.

const HEADER = 16;
const ENTRY = 8;
const ANCHORS = 10;
const ANCHOR_COUNT = 2;
const FLAGS = 7;
const CHARACTER_SHIFT = 2;
const UP = 1, DOWN = 2, LEFT = 4, RIGHT = 8;
const FACINGS = ["N", "NE", "E", "SE", "S", "SW", "W", "NW"];
const KEYS = {
    ArrowUp: UP, KeyW: UP, ArrowDown: DOWN, KeyS: DOWN,
    ArrowLeft: LEFT, KeyA: LEFT, ArrowRight: RIGHT, KeyD: RIGHT,
};
const DIRECTIONS = { up: UP, down: DOWN, left: LEFT, right: RIGHT };

// The tile kinds that get height or depth here, by their numbers in AdamEve.Core.World.TileKind (a unit test
// compares them).
const KIND = { water: 1, thicket: 2, crossing: 3, restingPlace: 8, flowers: 9 };

const WATER_LEVEL = -3;
const THICKET_HEIGHT = 12;
const TREE_CAPACITY = 64;
const CAMERA_DISTANCE = 2000;
const ELLIPSE_SEGMENTS = 64;
// A part of a character lies this much nearer the viewer than the part listed before it, along the line of sight.
const PART_STEP = 0.02;
// The plane of a character lies this much nearer the viewer than its feet: the ground never cuts a part.
const PLANE_LIFT = 3;
const SHADOW_MAP = 1024;
const SUN = [-0.72, 1.0, 0.42];
// The bank of the river has the colour of a trunk. The rest is light, not art: the sun, the sky, the haze.
const LIGHT = { bank: 0x7A5A3A, haze: 0xDCEBD2, sun: 0xFFF2D8, sky: 0xD6ECFF, earth: 0x4E7A44 };

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

function readAtlas(numbers) {
    const images = [];
    let at = 1;
    for (let image = 0; image < numbers[0]; image++) {
        const shapes = [];
        const count = numbers[at++];
        for (let shape = 0; shape < count; shape++, at += 6) {
            const [kind, x, y, width, height, colour] = numbers.slice(at, at + 6);
            shapes.push({ kind, x, y, width, height, colour });
        }
        images.push({ shapes, flat: null, tree: null });
    }
    return images;
}

// Gathers triangles with a colour for each corner into one geometry. Every solid is shaded flat, from its faces.
function gather(THREE) {
    const positions = [];
    const colours = [];
    const colour = new THREE.Color();
    const point = new THREE.Vector3();
    return {
        triangle(a, b, c, hex) {
            colour.setHex(hex);
            for (const corner of [a, b, c]) {
                positions.push(corner[0], corner[1], corner[2]);
                colours.push(colour.r, colour.g, colour.b);
            }
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

// The ground from the tiles: grass and flowers at height 0, the river a little lower with a bank on its north
// shore, the thicket as a raised, uneven mass with a wall where it ends toward the viewer. x is east, y is up,
// z is south, in the logical pixels of the map.
function buildGround(THREE, g) {
    const size = g.tileSize;
    const land = gather(THREE);
    const water = gather(THREE);
    const kindAt = (x, y) => (x < 0 || y < 0 || x >= g.mapWidth || y >= g.mapHeight ? KIND.thicket : g.tiles[y * g.mapWidth + x]);
    const wet = kind => kind === KIND.water || kind === KIND.crossing;
    const cornerHeight = (x, y) => THICKET_HEIGHT + 7 * scatter(x, y);
    for (let y = 0; y < g.mapHeight; y++) {
        for (let x = 0; x < g.mapWidth; x++) {
            const kind = kindAt(x, y);
            const style = kind * 4;
            const colour = g.ground[style + ((x + y) & 1)];
            const x0 = x * size, x1 = x0 + size, z0 = y * size, z1 = z0 + size;
            if (wet(kind)) {
                water.quad([x0, WATER_LEVEL, z0], [x0, WATER_LEVEL, z1], [x1, WATER_LEVEL, z1], [x1, WATER_LEVEL, z0], colour);
                if (!wet(kindAt(x, y - 1))) {
                    land.quad([x0, WATER_LEVEL, z0], [x1, WATER_LEVEL, z0], [x1, 0, z0], [x0, 0, z0], LIGHT.bank);
                }
            } else if (kind === KIND.thicket) {
                const h00 = cornerHeight(x, y), h10 = cornerHeight(x + 1, y), h01 = cornerHeight(x, y + 1), h11 = cornerHeight(x + 1, y + 1);
                const middle = [x0 + size / 2, THICKET_HEIGHT + 6 + 8 * scatter(x + 977, y + 131), z0 + size / 2];
                const a = [x0, h00, z0], b = [x0, h01, z1], c = [x1, h11, z1], d = [x1, h10, z0];
                land.triangle(a, b, middle, colour);
                land.triangle(b, c, middle, g.ground[style + ((x + y + 1) & 1)]);
                land.triangle(c, d, middle, colour);
                land.triangle(d, a, middle, g.ground[style + ((x + y + 1) & 1)]);
                if (kindAt(x, y + 1) !== KIND.thicket) {
                    land.quad([x0, 0, z1], [x1, 0, z1], c, b, g.ground[style + 1]);
                }
            } else {
                land.quad([x0, 0, z0], [x0, 0, z1], [x1, 0, z1], [x1, 0, z0], colour);
            }
        }
    }

    const landMesh = new THREE.Mesh(land.build(), g.solidMaterial);
    landMesh.receiveShadow = true;
    landMesh.castShadow = true;
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
// ellipse a smaller mass on the side of the crown that faces the viewer, where the flat image has it. The crown
// takes the place on the screen the flat ellipse has: its height and its depth together make the ellipse's height.
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
            // Along the line of sight (up and south alike) to the surface of the crown: the place on the screen stays.
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

// A part of a character: the flat shape of its image, as the canvas paints it. An ellipse is a polygon drawn
// around the ellipse, never inside it: a cover is never smaller here than the check takes it to be.
function flatOf(THREE, g, image) {
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
    let material = g.flatMaterials.get(shape.colour);
    if (!material) {
        // No light, no shadow, no haze: the colour of a part is the colour the rig gives it.
        material = new THREE.MeshBasicMaterial({ color: shape.colour, side: THREE.DoubleSide, fog: false });
        g.flatMaterials.set(shape.colour, material);
    }
    image.flat = { geometry, material };
    return image.flat;
}

function buildScene(THREE, g) {
    g.scene = new THREE.Scene();
    g.scene.background = new THREE.Color(g.backdrop);
    g.scene.fog = new THREE.Fog(LIGHT.haze, 1, 2);
    g.solidMaterial = new THREE.MeshLambertMaterial({ vertexColors: true, flatShading: true });

    // The fixed camera: south of what it shows and above it, looking north and down at 45 degrees. It only slides.
    g.camera = new THREE.OrthographicCamera(-1, 1, 1, -1, 1, CAMERA_DISTANCE * 4);
    g.camera.position.set(0, CAMERA_DISTANCE, CAMERA_DISTANCE);
    g.camera.lookAt(0, 0, 0);

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

    // Under each character a soft dark patch on the ground: the figures cast no shadow of their shape.
    const patch = new THREE.CircleGeometry(1, 24);
    patch.rotateX(-Math.PI / 2);
    for (let index = 0; index < ANCHOR_COUNT; index++) {
        const material = new THREE.MeshBasicMaterial({ color: 0x1B2A18, transparent: true, opacity: 0.28, depthWrite: false, fog: false });
        const mesh = new THREE.Mesh(patch, material);
        mesh.scale.set(11, 1, 5);
        mesh.renderOrder = 3;
        mesh.visible = false;
        g.scene.add(mesh);
        g.patches.push(mesh);
    }
}

function resize() {
    const box = game.canvas.getBoundingClientRect();
    game.ratio = Math.min(2, window.devicePixelRatio || 1);
    game.viewWidth = Math.max(1, box.width);
    game.viewHeight = Math.max(1, box.height);
    game.renderer.setPixelRatio(game.ratio);
    game.renderer.setSize(game.viewWidth, game.viewHeight, false);
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

function draw(list, seconds) {
    const g = game;
    const THREE = g.three;
    const scale = list[3];
    const width = g.viewWidth / scale;
    const height = g.viewHeight / scale;
    const centreX = list[1] + width / 2;
    const centreZ = list[2] + height / 2;

    // The camera shows the rectangle of the map the game's camera names, as the canvas does: a unit of the ground
    // toward the north and a unit of height are both one unit up the screen.
    const half = height / (2 * Math.SQRT2);
    if (g.camera.right !== width / 2 || g.camera.top !== half) {
        g.camera.left = -width / 2;
        g.camera.right = width / 2;
        g.camera.top = half;
        g.camera.bottom = -half;
        g.camera.updateProjectionMatrix();
        const reach = Math.max(width, height) / 2 + 120;
        const shadow = g.sun.shadow.camera;
        shadow.left = shadow.bottom = -reach;
        shadow.right = shadow.top = reach;
        shadow.near = 1;
        shadow.far = 1600;
        shadow.updateProjectionMatrix();
        g.shadowTexel = (2 * reach) / SHADOW_MAP;
        // The haze: none at the near edge of the picture, a little at the far edge.
        const near = CAMERA_DISTANCE * Math.SQRT2 - half - 24;
        g.scene.fog.near = near;
        g.scene.fog.far = near + (2 * half + 24) / 0.3;
    }
    g.camera.position.set(centreX, CAMERA_DISTANCE, centreZ + CAMERA_DISTANCE);

    // The sun follows in whole texels of its shadow map, so the shadows do not crawl while the camera slides.
    const target = g.sun.target.position.set(centreX, 0, centreZ);
    const alongRight = target.dot(g.sunRight), alongUp = target.dot(g.sunUp);
    target.addScaledVector(g.sunRight, Math.round(alongRight / g.shadowTexel) * g.shadowTexel - alongRight);
    target.addScaledVector(g.sunUp, Math.round(alongUp / g.shadowTexel) * g.shadowTexel - alongUp);
    g.sun.position.copy(target).addScaledVector(g.sunDirection, 800);

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
        const character = list[at + FLAGS] >> CHARACTER_SHIFT;
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

        // A part of a character, in the plane of that character: across is east, up is up, and the plane stands at
        // the feet. The transform is the one the canvas draws the part with; the list's order is the order of depth.
        const feet = list[ANCHORS + (character - 1) * 2 + 1];
        const nearer = PLANE_LIFT + parts * PART_STEP;
        const flat = flatOf(THREE, g, image);
        const mesh = partMesh(parts++);
        mesh.geometry = flat.geometry;
        mesh.material = flat.material;
        mesh.visible = true;
        mesh.matrix.set(
            list[at + 1], -list[at + 3], 0, e,
            -list[at + 2], list[at + 4], 0, feet - f + nearer,
            0, 0, 1, feet + nearer,
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

    g.renderer.render(g.scene, g.camera);
}

// What the tests and a screen reader's status line read: on the game root, changed only when a value changes.
function publish(list) {
    const values = {
        playerTile: list[4] + "," + list[5],
        concealment: game.verdicts[list[6]] || "fail:unknown:unknown",
        facing: FACINGS[list[7]],
        concealmentFailures: String(list[8]),
        moving: list[9] ? "true" : "false",
        motion: game.motion ? "on" : "off",
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

    // The foliage and the water move with the clock; under prefers-reduced-motion the clock of that motion stands.
    if (game.motion) {
        game.motionSeconds += Math.min(0.1, Math.max(0, (now - (game.lastNow ?? now)) / 1000));
    }
    game.lastNow = now;

    const list = readList();
    draw(list, game.motionSeconds);
    publish(list);
}

function inForm(target) {
    return target instanceof Element && target.closest("input, select, textarea, button, a, [data-game-panel]") !== null;
}

function listen(target, type, handler, options) {
    target.addEventListener(type, handler, options);
    game.listeners.push(() => target.removeEventListener(type, handler, options));
}

// The input adapters of render.js, as they are there: the ground lies on the screen as the canvas draws it, so a
// tap names the same tile.
function wireInput() {
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

// Starts this renderer on the canvas of the page. False when the browser has no WebGL: nothing was started, Three.js
// was not asked for, and the canvas is free for the canvas renderer.
export async function attach(listView, inputView, tiles, ground, atlas, verdicts, mapWidth, mapHeight, tileSize, backdrop) {
    detach();
    const root = document.getElementById("game");
    const canvas = document.getElementById("game-canvas");
    if (!root || !canvas) {
        throw new Error("The page has no game root and canvas.");
    }
    if (!hasWebGL()) {
        return false;
    }
    const THREE = await import("../lib/three/three.module.min.js");
    let renderer;
    try {
        renderer = new THREE.WebGLRenderer({ canvas, antialias: true, alpha: false, stencil: false, powerPreference: "high-performance" });
    } catch {
        return false;
    }
    renderer.shadowMap.enabled = true;
    renderer.shadowMap.type = THREE.PCFSoftShadowMap;

    const runtime = await globalThis.getDotnetRuntime(0);
    const exports = await runtime.getAssemblyExports("AdamEve.Client.dll");
    const reduced = window.matchMedia("(prefers-reduced-motion: reduce)");
    game = {
        three: THREE, renderer, root, canvas,
        frame: exports.AdamEve.Client.Game.GameInterop.Frame,
        listView, inputView,
        listCopy: new Float64Array(listView.length),
        input: new Float64Array(inputView.length),
        tiles, ground, verdicts, mapWidth, mapHeight, tileSize, backdrop,
        atlas: readAtlas(atlas),
        trees: [], parts: [], patches: [], shimmer: [], flowers: null,
        flatMaterials: new Map(),
        matrix: new THREE.Matrix4(), vector: new THREE.Vector3(), euler: new THREE.Euler(),
        motion: !reduced.matches, motionSeconds: 0, lastNow: undefined,
        published: {},
        listeners: [],
        held: 0, pressed: 0, action: 0, menu: 0, tapped: 0, tapX: 0, tapY: 0,
        viewWidth: 1, viewHeight: 1, ratio: 1, resized: true, frameRequest: 0, shadowTexel: 1,
    };
    buildScene(THREE, game);
    const touch = navigator.maxTouchPoints > 0 || "ontouchstart" in window || window.matchMedia("(pointer: coarse)").matches;
    root.dataset.touch = touch ? "true" : "false";
    wireInput();
    listen(reduced, "change", () => { game.motion = !reduced.matches; });
    listen(canvas, "webglcontextlost", event => event.preventDefault());
    listen(window, "resize", () => { game.resized = true; });
    listen(window, "orientationchange", () => { game.resized = true; });
    if (typeof ResizeObserver === "function") {
        const observer = new ResizeObserver(() => { if (game) { game.resized = true; } });
        observer.observe(canvas);
        game.listeners.push(() => observer.disconnect());
    }
    game.frameRequest = requestAnimationFrame(tick);
    root.dataset.ready = "true";
    return true;
}

export function detach() {
    if (!game) {
        return;
    }
    cancelAnimationFrame(game.frameRequest);
    for (const remove of game.listeners) {
        remove();
    }
    game.scene.traverse(object => {
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
    for (const image of game.atlas) {
        if (image.flat) {
            image.flat.geometry.dispose();
            image.flat.material.dispose();
        }
    }
    game.renderer.dispose();
    game.renderer.forceContextLoss();
    if (typeof game.listView.dispose === "function") {
        game.listView.dispose();
    }
    if (typeof game.inputView.dispose === "function") {
        game.inputView.dispose();
    }
    game = null;
}
