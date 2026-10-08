"""Generate review-only 16x16 mountain-front tiles fitted to the real grass edge."""

from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
GRASS_ATLAS = ROOT / "Assets/Res/Sprites/Village/Village_Tileset.png"
MOUNTAIN_ATLAS = ROOT / "Assets/Res/Sprites/Dungeon/MountainUpper16/MountainWall_Grass1.png"
FOOT_ATLAS = ROOT / "Assets/Res/Sprites/Dungeon/MountainFoot16/MountainFoot_Grass1.png"
OUTPUT = ROOT / "Tools/Previews/MountainFrontDraft"
SIZE = 16
SCALE = 4

HIGHLIGHT = (179, 153, 92, 255)
STONE = (160, 128, 67, 255)
SHADOW = (128, 91, 50, 255)
DARK = (81, 67, 50, 255)
CLEAR = (0, 0, 0, 0)


def grass_edge() -> tuple[Image.Image, Image.Image]:
    atlas = Image.open(GRASS_ATLAS).convert("RGBA")
    # Grass1 g1_2: straight upper edge, directly below the mountain foot.
    original = atlas.crop((64, 0, 128, 64))
    logical = Image.new("RGBA", (SIZE, SIZE))
    for y in range(SIZE):
        for x in range(SIZE):
            colors = {original.getpixel((x * SCALE + dx, y * SCALE + dy))
                      for dy in range(SCALE) for dx in range(SCALE)}
            if len(colors) != 1:
                raise ValueError(f"Grass1 source block {(x, y)} is not a 4x4 logical pixel")
            logical.putpixel((x, y), colors.pop())
    return original, logical


def close_horizontal_seam(tile: Image.Image) -> None:
    for y in range(SIZE):
        tile.putpixel((SIZE - 1, y), tile.getpixel((0, y)))


def mountain_wall() -> Image.Image:
    atlas = Image.open(MOUNTAIN_ATLAS).convert("RGBA")
    # Reuse the existing mountain's exact rock palette and hand-drawn strata.
    tile = atlas.crop((4 * SIZE, 4 * SIZE, 5 * SIZE, 5 * SIZE))
    for y in range(SIZE):
        for x in range(SIZE):
            if tile.getpixel((x, y))[3] == 0:
                tile.putpixel((x, y), DARK)
    for x in range(SIZE):
        tile.putpixel((x, SIZE - 1), tile.getpixel((x, 0)))
    close_horizontal_seam(tile)
    return tile


def mountain_foot(grass: Image.Image, wall: Image.Image) -> Image.Image:
    atlas = Image.open(FOOT_ATLAS).convert("RGBA")
    tile = atlas.crop((4 * SIZE, 3 * SIZE, 5 * SIZE, 4 * SIZE))
    for y in range(SIZE):
        for x in range(SIZE):
            color = tile.getpixel((x, y))
            green = color[1] > color[0] * 1.05
            if green and y < 8:
                color = STONE
            elif y >= 8 and (green or color[3] == 0):
                color = grass.getpixel((x, SIZE - 1 - y))
            elif color[3] == 0:
                color = DARK
            tile.putpixel((x, y), color)
    for x in range(SIZE):
        tile.putpixel((x, 0), wall.getpixel((x, SIZE - 1)))
        tile.putpixel((x, SIZE - 1), grass.getpixel((x, 0)))
    close_horizontal_seam(tile)
    return tile


def flat_summit() -> Image.Image:
    tile = Image.new("RGBA", (SIZE, SIZE), HIGHLIGHT)
    for y in range(SIZE):
        for x in range(SIZE):
            grain = (x * 17 + y * 11 + x * y * 3) % 31
            if (3 <= x <= 5 and 4 <= y <= 5) or (10 <= x <= 12 and 10 <= y <= 11):
                color = STONE
            elif grain in (0, 1, 2, 3):
                color = STONE
            elif grain == 4:
                color = SHADOW
            else:
                color = HIGHLIGHT
            tile.putpixel((x, y), color)
    for x in range(SIZE):
        tile.putpixel((x, SIZE - 1), tile.getpixel((x, 0)))
    close_horizontal_seam(tile)
    return tile


def wall_to_summit(wall: Image.Image, summit: Image.Image) -> Image.Image:
    tile = wall.copy()
    for y in range(8):
        for x in range(SIZE):
            if y <= 4:
                color = summit.getpixel((x, (y + 9) % SIZE))
            elif y == 5:
                color = HIGHLIGHT if x % 6 not in (0, 1) else STONE
            elif y == 6:
                color = SHADOW if x % 5 else DARK
            else:
                color = STONE if x % 4 else SHADOW
            tile.putpixel((x, y), color)
    for x in range(SIZE):
        tile.putpixel((x, 0), summit.getpixel((x, SIZE - 1)))
        tile.putpixel((x, SIZE - 1), wall.getpixel((x, 0)))
    close_horizontal_seam(tile)
    return tile


def far_summit_edge(summit: Image.Image) -> Image.Image:
    tile = Image.new("RGBA", (SIZE, SIZE), CLEAR)
    for x in range(SIZE):
        ridge = 5 + ((x * 7 + (x // 4) * 3) % 3)
        for y in range(ridge, SIZE):
            if y == ridge:
                color = SHADOW
            elif y == ridge + 1:
                color = STONE
            elif y == ridge + 2:
                color = HIGHLIGHT
            else:
                color = summit.getpixel((x, y))
            tile.putpixel((x, y), color)
    for x in range(SIZE):
        tile.putpixel((x, SIZE - 1), summit.getpixel((x, 0)))
    close_horizontal_seam(tile)
    return tile


def splice_preview(original_grass: Image.Image, tiles: list[Image.Image]) -> Image.Image:
    width, height = 4 * 64, (len(tiles) + 1) * 64
    preview = Image.new("RGBA", (width, height), (40, 42, 45, 255))
    for y in range(0, height, 8):
        for x in range(0, width, 8):
            if (x // 8 + y // 8) % 2 == 0:
                preview.paste((54, 56, 59, 255), (x, y, x + 8, y + 8))
    for col in range(4):
        for row, tile in enumerate(tiles):
            scaled = tile.resize((64, 64), Image.Resampling.NEAREST)
            preview.alpha_composite(scaled, (col * 64, row * 64))
        preview.alpha_composite(original_grass, (col * 64, len(tiles) * 64))
    return preview.resize((width * 2, height * 2), Image.Resampling.NEAREST)


def main() -> None:
    original_grass, grass = grass_edge()
    wall = mountain_wall()
    foot = mountain_foot(grass, wall)
    summit = flat_summit()
    shoulder = wall_to_summit(wall, summit)
    far_edge = far_summit_edge(summit)

    for tile in (foot, wall, shoulder, summit, far_edge):
        assert tile.size == (SIZE, SIZE)
        assert all(tile.getpixel((0, y)) == tile.getpixel((15, y)) for y in range(SIZE))
    assert all(foot.getpixel((x, 15)) == grass.getpixel((x, 0)) for x in range(SIZE))
    assert all(foot.getpixel((x, 0)) == wall.getpixel((x, 15)) for x in range(SIZE))
    assert all(wall.getpixel((x, 0)) == wall.getpixel((x, 15)) for x in range(SIZE))
    assert all(wall.getpixel((x, 0)) == shoulder.getpixel((x, 15)) for x in range(SIZE))
    assert all(shoulder.getpixel((x, 0)) == summit.getpixel((x, 15)) for x in range(SIZE))
    assert all(summit.getpixel((x, 0)) == summit.getpixel((x, 15)) for x in range(SIZE))
    assert all(summit.getpixel((x, 0)) == far_edge.getpixel((x, 15)) for x in range(SIZE))
    assert all(far_edge.getpixel((x, 0))[3] == 0 for x in range(SIZE))
    assert any(far_edge.getpixel((x, y))[3] == 0 for y in range(SIZE) for x in range(SIZE))

    OUTPUT.mkdir(parents=True, exist_ok=True)
    stages = (
        ("MountainFoot_Grass1_16.png", foot),
        ("MountainWall_16.png", wall),
        ("MountainWallToSummit_16.png", shoulder),
        ("MountainSummitFlat_16.png", summit),
        ("MountainSummitFarEdge_16.png", far_edge),
    )
    for filename, tile in stages:
        tile.save(OUTPUT / filename)
    preview = splice_preview(original_grass, [far_edge, summit, shoulder, wall, foot])
    preview.save(OUTPUT / "MountainFrontSplicePreview.png")
    print(f"Generated five 16x16 review tiles and preview in {OUTPUT}")


if __name__ == "__main__":
    main()
