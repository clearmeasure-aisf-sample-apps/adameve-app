# Three.js, vendored

For the trial of a second renderer (`docs/spike-threejs.md`). Fetched by `js/render-three.js` only when that
renderer is chosen and the browser has WebGL; never part of the first load.

- Version: 0.185.1 (release r185), MIT licence (`LICENSE.txt`, the `LICENSE` of the package).
- Source: the official npm registry tarball, `https://registry.npmjs.org/three/-/three-0.185.1.tgz`, whose
  integrity was compared with the registry's metadata (`dist.integrity`):
  `sha512-5aojFCXKwnjBRZvUnt3WFfEcvUJgkN5LlijRFN95hMy8WVkG4I0QNcJE+OuWvuJ0bOdStrbfXn0pkd6/QyiAlg==`.
- Files, copied byte for byte from `package/build/` and `package/LICENSE` (a unit test, `ThreeVendorTests`,
  compares each SHA-256):

| File | SHA-256 |
|---|---|
| `three.module.min.js` | `86bcee248b64f44bcfc23c331ae74619061957d59cab040171dcb6fb5900beb6` |
| `three.core.min.js` | `05b2609338c76cd65daf74f3ac515bc9a5045e1b3b33edc07d8c9bd55250fa90` |
| `LICENSE.txt` | `8b378ebe60e2fe500158cb0ac71cb5e8b7d92953c2abcc63a0eb90499653b5bc` |

`three.module.min.js` is the ES module build; it imports `./three.core.min.js`, so the two go together. No addon
is used. 0.185.1 is the last release whose package has the minified builds: 0.186.0 and 0.186.1 ship
`three.module.js` and `three.core.js` only.

To change the version: download the tarball, compare its SHA-512 with the registry's `dist.integrity`, replace the
three files, and write the new version and hashes here and in `NOTICE`.
