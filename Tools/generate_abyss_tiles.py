"""Build 16x16 abyss tiles and splice previews from the project's grass sprites."""

from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Assets/Res/Sprites/Village/Village_Tileset.png"
OUTPUT = ROOT / "Assets/Res/Sprites/Dungeon/Abyss"
PREVIEW_OUTPUT = ROOT / "Tools/Previews"

# The straight ground edge with a non-grass neighbour below resolves to
# g1_8/g2_8/g3_8 (rule 7 in 15TileFather.asset). Unity's bottom-left rects
# are (64/384/704, 1856, 64, 64); these are their top-left PNG coordinates.
GRASS_RECTS_TOP_LEFT = {
    "Grass1": (64, 128, 128, 192),
    "Grass2": (384, 128, 448, 192),
    "Grass3": (704, 128, 768, 192),
}
GRASS_SIDE_RECTS_TOP_LEFT = {
    "Grass1": ((0, 64, 64, 128), (128, 64, 192, 128)),
    "Grass2": ((320, 64, 384, 128), (448, 64, 512, 128)),
    "Grass3": ((640, 64, 704, 128), (768, 64, 832, 128)),
}
SCALE = 4
SIZE = 16

SOIL_HIGHLIGHT = (179, 153, 92, 255)
SOIL = (160, 128, 67, 255)
SOIL_SHADOW = (128, 91, 50, 255)
ROCK_LIGHT = (107, 88, 65, 255)
ROCK = (81, 67, 50, 255)
ROCK_SHADE = (62, 52, 43, 255)
ROCK_DEEP = (41, 36, 32, 255)
BLACK = (0, 0, 0, 255)


def grass_tile(source: Image.Image, name: str, rect: tuple[int, int, int, int]) -> tuple[Image.Image, Image.Image]:
    original = source.crop(rect)
    logical = Image.new("RGBA", (SIZE, SIZE))
    for y in range(SIZE):
        for x in range(SIZE):
            colors = {
                original.getpixel((x * SCALE + dx, y * SCALE + dy))
                for dy in range(SCALE) for dx in range(SCALE)
            }
            if len(colors) != 1:
                raise ValueError(f"{name} edge sprite changed: source block {(x, y)} is not a 4x4 pixel")
            logical.putpixel((x, y), colors.pop())
    return original, logical


def wall_tile() -> Image.Image:
    tile = Image.new("RGBA", (SIZE, SIZE), ROCK)
    for y in range(SIZE):
        for x in range(SIZE):
            # Short offset columns and narrow cracks echo the existing mountain sprites.
            ridge = (x + (y // 4) * 3) % 9
            grain = (x * 13 + y * 7 + (x * y) % 11) % 17
            if ridge in (0, 1) and grain < 14:
                color = ROCK_SHADE
            elif ridge == 2 and grain < 10:
                color = ROCK_DEEP
            elif grain in (0, 1, 2):
                color = ROCK_LIGHT
            elif grain in (3, 4):
                color = ROCK_SHADE
            else:
                color = ROCK
            tile.putpixel((x, y), color)

    # Exact shared borders matter more than a nonrepeating decorative detail.
    for y in range(SIZE):
        tile.putpixel((SIZE - 1, y), tile.getpixel((0, y)))
    for x in range(SIZE):
        tile.putpixel((x, SIZE - 1), tile.getpixel((x, 0)))
    return tile


def transition_tile(grass: Image.Image, wall: Image.Image) -> Image.Image:
    tile = Image.new("RGBA", (SIZE, SIZE), ROCK)
    grass_colors = {grass.getpixel((x, y)) for y in range(SIZE) for x in range(SIZE)}
    green_colors = [color for color in grass_colors if color[1] > color[0] * 1.05]
    grass_dark = min(green_colors, key=lambda color: color[1])
    lip = (5, 5, 6, 6, 5, 5, 4, 5, 6, 6, 5, 4, 5, 6, 5, 5)
    for x in range(SIZE):
        for y in range(SIZE):
            if y < lip[x]:
                # Use pixels sampled from the matching grass tile's bottom edge.
                color = grass.getpixel((x, 15 - y))
                if y >= 3 and ((x * 7 + y * 5) % 13 == 0):
                    color = grass_dark
            elif y == lip[x]:
                color = SOIL_SHADOW
            elif y == lip[x] + 1:
                color = SOIL_HIGHLIGHT if x % 5 in (0, 1) else SOIL
            elif y <= lip[x] + 3:
                color = SOIL if (x + y) % 4 else SOIL_SHADOW
            else:
                color = wall.getpixel((x, (y + 1) % SIZE))
            tile.putpixel((x, y), color)
    for x in range(SIZE):
        tile.putpixel((x, SIZE - 1), wall.getpixel((x, 0)))
    for y in range(SIZE):
        tile.putpixel((SIZE - 1, y), tile.getpixel((0, y)))
    return tile


def wall_bottom_tile(wall: Image.Image) -> Image.Image:
    tile = Image.new("RGBA", (SIZE, SIZE), BLACK)
    for y in range(SIZE):
        fade = ((SIZE - 1 - y) / (SIZE - 1)) ** 1.3
        for x in range(SIZE):
            rock = wall.getpixel((x, y))
            tile.putpixel((x, y), tuple(round(channel * fade) for channel in rock[:3]) + (255,))
    assert all(tile.getpixel((x, y)) != BLACK for y in range(SIZE - 1) for x in range(SIZE))
    return tile


def side_transition_tile(front: Image.Image, grass: Image.Image,
                         wall: Image.Image, void_on_left: bool) -> Image.Image:
    # Rotate the approved front cliff shape, then replace both join edges with
    # pixels from the actual side grass sprite and the shared wall tile.
    rotation = Image.Transpose.ROTATE_270 if void_on_left else Image.Transpose.ROTATE_90
    tile = front.transpose(rotation)
    grass_edge = 0 if void_on_left else SIZE - 1
    near_edge = SIZE - 1 if void_on_left else 0
    far_edge = 0 if void_on_left else SIZE - 1
    wall_edge = SIZE - 1 if void_on_left else 0
    for y in range(SIZE):
        tile.putpixel((near_edge, y), grass.getpixel((grass_edge, y)))
        tile.putpixel((far_edge, y), wall.getpixel((wall_edge, y)))
    return tile


def side_fade_tile(wall: Image.Image, void_on_left: bool) -> Image.Image:
    tile = Image.new("RGBA", (SIZE, SIZE), BLACK)
    for y in range(SIZE):
        for x in range(SIZE):
            distance_from_wall = SIZE - 1 - x if void_on_left else x
            fade = ((SIZE - 1 - distance_from_wall) / (SIZE - 1)) ** 1.3
            rock = wall.getpixel((x, y))
            tile.putpixel((x, y), tuple(round(channel * fade) for channel in rock[:3]) + (255,))
    return tile


def outer_corner_tile(front: Image.Image, side: Image.Image,
                      void_on_left: bool) -> Image.Image:
    # This cell sits diagonally below and beside a grass corner. Its top edge
    # joins the side strip, and its grass-facing side joins the front strip.
    tile = Image.new("RGBA", (SIZE, SIZE), BLACK)
    corner_edge = SIZE - 1 if void_on_left else 0
    front_edge = 0 if void_on_left else SIZE - 1
    for y in range(SIZE):
        for x in range(SIZE):
            outward_x = SIZE - 1 - x if void_on_left else x
            base = side.getpixel((x, SIZE - 1)) if y <= outward_x else front.getpixel((front_edge, y))
            fade = 1 - (y * outward_x) / ((SIZE - 1) ** 2)
            tile.putpixel((x, y), tuple(round(channel * fade) for channel in base[:3]) + (255,))
    for x in range(SIZE):
        tile.putpixel((x, 0), side.getpixel((x, SIZE - 1)))
    for y in range(SIZE):
        tile.putpixel((corner_edge, y), front.getpixel((front_edge, y)))
    return tile


def inner_corner_tile(front: Image.Image, side: Image.Image,
                      void_on_left: bool) -> Image.Image:
    # Grass touches both the top and one side of this void cell. Follow the
    # nearer edge, forming a diagonal join between the two existing lips.
    tile = Image.new("RGBA", (SIZE, SIZE), BLACK)
    for y in range(SIZE):
        for x in range(SIZE):
            distance_from_side = SIZE - 1 - x if void_on_left else x
            color = front.getpixel((x, y)) if y <= distance_from_side else side.getpixel((x, y))
            tile.putpixel((x, y), color)
    return tile


def make_preview(samples: list[tuple[Image.Image, Image.Image]], wall: Image.Image,
                 bottom: Image.Image, black: Image.Image) -> Image.Image:
    preview = Image.new("RGBA", (len(samples) * 2 * 64, 5 * 64), BLACK)
    for sample_index, (original_grass, transition) in enumerate(samples):
        for repeat in range(2):
            x = (sample_index * 2 + repeat) * 64
            preview.paste(original_grass, (x, 0))
            for y, tile in enumerate((transition, wall, bottom, black), start=1):
                preview.paste(tile.resize((64, 64), Image.Resampling.NEAREST), (x, y * 64))
    return preview.resize((preview.width * 2, preview.height * 2), Image.Resampling.NEAREST)


def make_side_preview(samples: list[tuple[Image.Image, Image.Image, Image.Image, Image.Image]],
                      wall: Image.Image, left_fade: Image.Image,
                      right_fade: Image.Image, black: Image.Image) -> Image.Image:
    preview = Image.new("RGBA", (10 * 64, len(samples) * 64), BLACK)
    for row, (grass_left, left, grass_right, right) in enumerate(samples):
        tiles = (black, left_fade, wall, left, grass_left,
                 grass_right, right, wall, right_fade, black)
        for col, tile in enumerate(tiles):
            if tile.size != (64, 64):
                tile = tile.resize((64, 64), Image.Resampling.NEAREST)
            preview.paste(tile, (col * 64, row * 64))
    return preview.resize((preview.width * 2, preview.height * 2), Image.Resampling.NEAREST)


def make_island_preview(source: Image.Image, front: Image.Image, left: Image.Image,
                        right: Image.Image, left_corner: Image.Image,
                        right_corner: Image.Image, wall: Image.Image, bottom: Image.Image,
                        left_fade: Image.Image, right_fade: Image.Image,
                        black: Image.Image) -> Image.Image:
    width, height = 11, 8
    preview = Image.new("RGBA", (width * 64, height * 64), BLACK)
    for row in range(height):
        for col in range(width):
            grass = 4 <= col <= 6 and 1 <= row <= 3
            if grass:
                grass_col = col - 4
                grass_row = row - 1
                tile = source.crop((grass_col * 64, grass_row * 64,
                                    (grass_col + 1) * 64, (grass_row + 1) * 64))
            elif row == 4 and col == 3:
                tile = left_corner.resize((64, 64), Image.Resampling.NEAREST)
            elif row == 4 and col == 7:
                tile = right_corner.resize((64, 64), Image.Resampling.NEAREST)
            else:
                front_depth = row - 3 if 4 <= col <= 6 and row > 3 else 0
                left_depth = 4 - col if 1 <= row <= 3 and col < 4 else 0
                right_depth = col - 6 if 1 <= row <= 3 and col > 6 else 0
                candidates = [(front_depth, (front, wall, bottom)),
                              (left_depth, (left, wall, left_fade)),
                              (right_depth, (right, wall, right_fade))]
                active = [(depth, tiles) for depth, tiles in candidates if depth > 0]
                depth, stages = min(active, key=lambda item: item[0]) if active else (0, ())
                tile = stages[depth - 1] if 1 <= depth <= 3 else black
                tile = tile.resize((64, 64), Image.Resampling.NEAREST)
            preview.paste(tile, (col * 64, row * 64))
    return preview.resize((preview.width * 2, preview.height * 2), Image.Resampling.NEAREST)


def main() -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    source = Image.open(SOURCE).convert("RGBA")
    wall = wall_tile()
    bottom = wall_bottom_tile(wall)
    left_fade = side_fade_tile(wall, True)
    right_fade = side_fade_tile(wall, False)
    black = Image.new("RGBA", (SIZE, SIZE), BLACK)
    samples = []
    side_samples = []
    corner_samples = []

    # Compare at actual world scale: existing grass is 64 px/PPU64,
    # while the new tile is 16 px/PPU16. Both occupy one grid cell.
    for name, rect in GRASS_RECTS_TOP_LEFT.items():
        original_grass, grass = grass_tile(source, name, rect)
        for corner_offset in (-64, 64):
            corner_rect = (rect[0] + corner_offset, rect[1], rect[2] + corner_offset, rect[3])
            corner = source.crop(corner_rect)
            assert all(corner.getpixel((x, 63)) == original_grass.getpixel((x, 63)) for x in range(64))
        transition = transition_tile(grass, wall)
        scaled = transition.resize((64, 64), Image.Resampling.NEAREST)
        assert all(original_grass.getpixel((x, 63)) == scaled.getpixel((x, 0)) for x in range(64))
        assert all(transition.getpixel((x, 15)) == wall.getpixel((x, 0)) for x in range(SIZE))
        assert all(transition.getpixel((0, y)) == transition.getpixel((15, y)) for y in range(SIZE))
        transition.save(OUTPUT / f"AbyssTransition_{name}_16.png")
        samples.append((original_grass, transition))

        left_rect, right_rect = GRASS_SIDE_RECTS_TOP_LEFT[name]
        original_left, grass_left = grass_tile(source, f"{name} left side", left_rect)
        original_right, grass_right = grass_tile(source, f"{name} right side", right_rect)
        for corner_y in (0, 128):
            left_corner = source.crop((left_rect[0], corner_y, left_rect[2], corner_y + 64))
            right_corner = source.crop((right_rect[0], corner_y, right_rect[2], corner_y + 64))
            assert all(left_corner.getpixel((0, y)) == original_left.getpixel((0, y)) for y in range(64))
            assert all(right_corner.getpixel((63, y)) == original_right.getpixel((63, y)) for y in range(64))
        left = side_transition_tile(transition, grass_left, wall, True)
        right = side_transition_tile(transition, grass_right, wall, False)
        for y in range(SIZE):
            assert left.getpixel((15, y)) == grass_left.getpixel((0, y))
            assert left.getpixel((0, y)) == wall.getpixel((15, y))
            assert right.getpixel((0, y)) == grass_right.getpixel((15, y))
            assert right.getpixel((15, y)) == wall.getpixel((0, y))
            assert left.getpixel((0, y)) == left_fade.getpixel((15, y))
            assert right.getpixel((15, y)) == right_fade.getpixel((0, y))
        assert all(left.getpixel((x, 0)) == left.getpixel((x, 15)) for x in range(SIZE))
        assert all(right.getpixel((x, 0)) == right.getpixel((x, 15)) for x in range(SIZE))
        left.save(OUTPUT / f"AbyssTransition_{name}_Left_16.png")
        right.save(OUTPUT / f"AbyssTransition_{name}_Right_16.png")
        side_samples.append((original_left, left, original_right, right))
        left_corner = outer_corner_tile(transition, left, True)
        right_corner = outer_corner_tile(transition, right, False)
        assert all(left_corner.getpixel((x, 0)) == left.getpixel((x, 15)) for x in range(SIZE))
        assert all(right_corner.getpixel((x, 0)) == right.getpixel((x, 15)) for x in range(SIZE))
        assert all(left_corner.getpixel((15, y)) == transition.getpixel((0, y)) for y in range(SIZE))
        assert all(right_corner.getpixel((0, y)) == transition.getpixel((15, y)) for y in range(SIZE))
        left_corner.save(OUTPUT / f"AbyssCorner_{name}_Left_16.png")
        right_corner.save(OUTPUT / f"AbyssCorner_{name}_Right_16.png")
        corner_samples.append((left_corner, right_corner))
        left_inner = inner_corner_tile(transition, left, True)
        right_inner = inner_corner_tile(transition, right, False)
        for x in range(SIZE):
            assert left_inner.getpixel((x, 0)) == transition.getpixel((x, 0))
            assert right_inner.getpixel((x, 0)) == transition.getpixel((x, 0))
            assert left_inner.getpixel((x, 15)) == left.getpixel((x, 15))
            assert right_inner.getpixel((x, 15)) == right.getpixel((x, 15))
        for y in range(SIZE):
            assert left_inner.getpixel((15, y)) == left.getpixel((15, y))
            assert right_inner.getpixel((0, y)) == right.getpixel((0, y))
            assert left_inner.getpixel((0, y)) == transition.getpixel((0, y))
            assert right_inner.getpixel((15, y)) == transition.getpixel((15, y))
        left_inner.save(OUTPUT / f"AbyssInnerCorner_{name}_Left_16.png")
        right_inner.save(OUTPUT / f"AbyssInnerCorner_{name}_Right_16.png")

    assert all(wall.getpixel((x, 15)) == wall.getpixel((x, 0)) for x in range(16))
    assert all(wall.getpixel((x, 15)) == bottom.getpixel((x, 0)) for x in range(16))
    assert all(bottom.getpixel((x, 15)) == BLACK for x in range(SIZE))
    assert all(left_fade.getpixel((0, y)) == BLACK for y in range(SIZE))
    assert all(right_fade.getpixel((15, y)) == BLACK for y in range(SIZE))
    for tile in (wall, bottom, black):
        assert all(tile.getpixel((0, y)) == tile.getpixel((15, y)) for y in range(SIZE))
    assert all(black.getpixel((x, y)) == BLACK for y in range(SIZE) for x in range(SIZE))

    for name, tile in (
        ("AbyssWall_16.png", wall),
        ("AbyssWallBottom_16.png", bottom),
        ("AbyssFade_Left_16.png", left_fade),
        ("AbyssFade_Right_16.png", right_fade),
        ("AbyssBlack_16.png", black),
    ):
        tile.save(OUTPUT / name)

    preview = make_preview(samples, wall, bottom, black)
    PREVIEW_OUTPUT.mkdir(parents=True, exist_ok=True)
    preview.save(PREVIEW_OUTPUT / "AbyssGrassSplicePreview.png")
    side_preview = make_side_preview(side_samples, wall, left_fade, right_fade, black)
    side_preview.save(PREVIEW_OUTPUT / "AbyssGrassSideSplicePreview.png")
    island_preview = make_island_preview(source, samples[0][1], side_samples[0][1],
                                         side_samples[0][3], corner_samples[0][0],
                                         corner_samples[0][1], wall, bottom,
                                         left_fade, right_fade, black)
    island_preview.save(PREVIEW_OUTPUT / "AbyssGrassIslandPreview.png")
    print(f"Generated twenty-six {SIZE}x{SIZE} tiles in {OUTPUT}")
    print(f"Splice previews: {PREVIEW_OUTPUT}")


if __name__ == "__main__":
    main()
