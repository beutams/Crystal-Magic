# Mountain cliff — three-part visual draft v2

Generated using the built-in image_gen tool on 2026-09-27.

## Images

1. `01-Front-And-Foot.png`: front-facing upright cliff and narrow grass foot.
2. `02-Sides-And-Corners.png`: upright left/right side boundaries and foot corners.
3. `03-Summit-Transition.png`: complete summit perimeter and untextured central top.

Reference: user-supplied `codex-clipboard-a5579854-34d9-4ff0-ba73-0647308e0100.png`, inspected before generation. Fourth itch.io pack was selected as a direction in the conversation, but its image previews could not be loaded, so no external pack image was supplied to the generator.

These are generated visual drafts, not production-ready 16x16 sprite sheets. Do not slice at 16 pixels or assume pixel-exact matching from these enlarged outputs. Native pixel grid, strict palette, repeatability and transition seams still need calibration. No RuleTile, runtime configuration or existing artwork was replaced.

## Full generation prompts

### Image 1

```text
Use case: stylized-concept.
Asset type: original pixel-art mountain terrain part for a 16x16-tile top-down 2D RPG, one of a coherent three-image set.
Input image 1 is ONLY a structure and pixel-style reference: the user's cropped tan upright cliff with green ground. Reproduce the readable near-90-degree change from horizontal ground to tall vertical cliff, coarse pixel clusters, and upright corner edges, not the tree or cursor.
Style: genuine very low-resolution hand-pixelled game art on a strict virtual 64 by 64 pixel grid, enlarged cleanly by nearest-neighbor to a square 1024 by 1024 PNG. Every virtual pixel is a crisp 16px square. Flat opaque colors, no subpixel strokes, no antialiasing, no gradients, no glow or shiny highlights.
Shared palette for ALL three parts: surrounding project grass is exactly #A0AE44, grass contact shadow #70843B; cliff main ochre #A08043, muted lighter plane #B3995C, shaded rock #805B32, deep structural crease #514332. Solid summit interior is #B3995C. Keep these same colors at all boundaries.
The rock is ONE continuous mountain wall with large interlocking earthy flat planes and sparse vertical fissures. Read as upright sheer cliff, NEVER paving stones, pebbles, cobblestones, a heap of round boulders, or a flat mottled rock floor. No individually outlined stones.
No text, numbers, labels, panel dividers, borders, sample swatches, grids, trees, flowers, characters, UI or other objects. This is the terrain itself, full bleed, not a poster.
Primary request — IMAGE 1 / FRONT FACE AND FOOT:
Draw a close view of a broad tall front-facing mountain cliff rising abruptly from flat grass, using the same top-down RPG projection as the reference.
The entire upper 3/4 of the square (virtual rows 0–47) is the vertical ochre wall, continuing beyond the top, left and right canvas edges. No summit is visible here. Sparse dark vertical clefts and stepped vertical buttresses communicate sheer height. Keep the front plane continuous and visually broad, not densely noisy.
The bottom 1/4 (rows 48–63) is perfectly flat plain #A0AE44 grass. At row 47–49 a restrained irregular 1–2 virtual pixel contact line of dark green and dark brown anchors the wall to the grass, without scattered rocks. The transition is abrupt like an upright retaining cliff, not a slope. At BOTH horizontal canvas edges the grass/wall transition is at the identical row and has the identical colors, so this segment can conceptually extend horizontally.
This image focuses on the front cliff and foot only, with no side caps and no top ledge.
```

### Image 2

```text
Use case: stylized-concept.
Asset type: original pixel-art mountain terrain part for a 16x16-tile top-down 2D RPG, one of a coherent three-image set.
Input image 1 is ONLY a structure and pixel-style reference: the user's cropped tan upright cliff with green ground. Reproduce the readable near-90-degree change from horizontal ground to tall vertical cliff, coarse pixel clusters, and upright corner edges, not the tree or cursor.
Style: genuine very low-resolution hand-pixelled game art on a strict virtual 64 by 64 pixel grid, enlarged cleanly by nearest-neighbor to a square 1024 by 1024 PNG. Every virtual pixel is a crisp 16px square. Flat opaque colors, no subpixel strokes, no antialiasing, no gradients, no glow or shiny highlights.
Shared palette for ALL three parts: surrounding project grass is exactly #A0AE44, grass contact shadow #70843B; cliff main ochre #A08043, muted lighter plane #B3995C, shaded rock #805B32, deep structural crease #514332. Solid summit interior is #B3995C. Keep these same colors at all boundaries.
The rock is ONE continuous mountain wall with large interlocking earthy flat planes and sparse vertical fissures. Read as upright sheer cliff, NEVER paving stones, pebbles, cobblestones, a heap of round boulders, or a flat mottled rock floor. No individually outlined stones.
No text, numbers, labels, panel dividers, borders, sample swatches, grids, trees, flowers, characters, UI or other objects. This is the terrain itself, full bleed, not a poster.
Primary request — IMAGE 2 / LEFT AND RIGHT SIDES WITH FOOT CORNERS:
Draw the side-boundary module of the SAME upright mountain. One continuous connected mountain wall fills the middle three-quarters of the width and extends beyond the top canvas edge, with narrow 8-virtual-pixel grass bands outside its left and right sides. The cliff is tall; the upper 48 virtual rows contain the same rock as IMAGE 1. Bottom 16 rows are plain grass.
At each side there is a strong continuous vertical crease/ridge, like the dark upright ridge at the right side of the user's reference. Clearly distinguish a narrow left-facing side plane and darker right-facing side plane from the broad front-facing plane. Light direction remains consistent; do NOT rotate textures or highlights.
The side edges run straight vertically down to the foot, then turn through near-right-angle stepped corners into the horizontal mountain-foot contact at virtual row 48. This must depict BOTH left and right outside foot corners and the upward-extendable side edges, not a detached boulder. The top is cropped by the canvas and must NOT be rounded off or capped. No visible summit in this image.
Use continuous large rock planes, flat low-key shading and sparse long vertical breaks; retain plain grass outside with no scattered stones.
```

### Image 3

```text
Use case: stylized-concept.
Asset type: original pixel-art mountain terrain part for a 16x16-tile top-down 2D RPG, one of a coherent three-image set.
Input image 1 is ONLY a structure and pixel-style reference: the user's cropped tan upright cliff with green ground. Reproduce the readable near-90-degree change from horizontal ground to tall vertical cliff, coarse pixel clusters, and upright corner edges, not the tree or cursor.
Style: genuine very low-resolution hand-pixelled game art on a strict virtual 64 by 64 pixel grid, enlarged cleanly by nearest-neighbor to a square 1024 by 1024 PNG. Every virtual pixel is a crisp 16px square. Flat opaque colors, no subpixel strokes, no antialiasing, no gradients, no glow or shiny highlights.
Shared palette for ALL three parts: surrounding project grass is exactly #A0AE44, grass contact shadow #70843B; cliff main ochre #A08043, muted lighter plane #B3995C, shaded rock #805B32, deep structural crease #514332. Solid summit interior is #B3995C. Keep these same colors at all boundaries.
The rock is ONE continuous mountain wall with large interlocking earthy flat planes and sparse vertical fissures. Read as upright sheer cliff, NEVER paving stones, pebbles, cobblestones, a heap of round boulders, or a flat mottled rock floor. No individually outlined stones.
No text, numbers, labels, panel dividers, borders, sample swatches, grids, trees, flowers, characters, UI or other objects. This is the terrain itself, full bleed, not a poster.
Primary request — IMAGE 3 / COMPLETE SUMMIT TRANSITION RING AND PLAIN CENTER:
Draw a complete rectangular raised mountain summit in the SAME straight-on top-down RPG projection, with a complete closed perimeter connecting the flat summit to its upright cliff on every side: rear rim, front lip, left edge, right edge, and all four near-90-degree stepped corners.
The summit middle is a LARGE completely uniform untextured #B3995C area, occupying at least half the image width and about half its height. No cracks, dots, grass, stones, noise or shading inside this central flat area.
Only the surrounding perimeter has restrained ochre pixel steps/rock structure. The rear rim is shallow; the front rim folds sharply downward into a taller sheer front face; the left and right edges connect to matching side faces using the same #A08043, #805B32, #514332 cliff colors. Keep the same modest RPG 3/4-overhead projection as the reference, NOT a rotated isometric diamond, a 3D render, a floating island, a crater, or a round rock.
Show the whole closed top ring clearly within the square, with plain #A0AE44 grass visible beyond the mountain silhouette. The focus is the complete mountain-top-to-cliff transition and perfectly flat solid-color summit fill. Pixel silhouette corners should be near right angles, not circular. No trees or other objects.
```

