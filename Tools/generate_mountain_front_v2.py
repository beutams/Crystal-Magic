"""Build a review-only, six-column 16px mountain front fitted to Grass1."""

from pathlib import Path

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[1]
GRASS_ATLAS = ROOT / "Assets/Res/Sprites/Village/Village_Tileset.png"
OUT = ROOT / "Tools/Previews/MountainFrontV2"
TILE = 16
COLS = 6
W = TILE * COLS
H = TILE * 5
SCALE = 6

# The four mountain colours are taken from the project's existing atlas.
LIGHT = (179, 153, 92, 255)
STONE = (160, 128, 67, 255)
SHADE = (128, 91, 50, 255)
DARK = (81, 67, 50, 255)
MOSS_SHADOW = (112, 134, 58, 255)
CLEAR = (0, 0, 0, 0)


def grass_source() -> tuple[Image.Image, Image.Image]:
    atlas = Image.open(GRASS_ATLAS).convert("RGBA")
    original = atlas.crop((64, 0, 128, 64))  # Grass1 g1_2, upper straight edge
    logical = Image.new("RGBA", (TILE, TILE))
    for y in range(TILE):
        for x in range(TILE):
            block = {
                original.getpixel((x * 4 + dx, y * 4 + dy))
                for dy in range(4) for dx in range(4)
            }
            if len(block) != 1:
                raise ValueError(f"Grass1 pixel {(x, y)} is not a solid 4x4 block")
            logical.putpixel((x, y), block.pop())
    return original, logical


def stepped_profile(knots: list[tuple[int, int]]) -> list[int]:
    result = []
    for x in range(W):
        for i in range(len(knots) - 1):
            x0, y0 = knots[i]
            x1, y1 = knots[i + 1]
            if x0 <= x <= x1:
                t = (x - x0) / (x1 - x0)
                result.append(round(y0 + (y1 - y0) * t))
                break
    assert len(result) == W
    return result


def plateau_stones(draw: ImageDraw.ImageDraw) -> None:
    # Broad, sparse shapes read as stone slabs instead of random sand noise.
    slabs = [
        ([(4, 16), (12, 14), (18, 18), (16, 21), (7, 22)], STONE),
        ([(26, 25), (34, 22), (42, 25), (40, 29), (30, 30)], STONE),
        ([(55, 13), (65, 11), (72, 15), (67, 18), (57, 18)], STONE),
        ([(78, 27), (88, 24), (94, 27), (92, 31), (81, 32)], STONE),
        ([(0, 32), (10, 29), (19, 31), (14, 35), (2, 36)], STONE),
        ([(46, 33), (55, 30), (63, 31), (65, 35), (51, 37)], STONE),
    ]
    for points, color in slabs:
        draw.polygon(points, fill=color)
        x, y = points[0]
        draw.line([(x + 2, y + 1), (x + 5, y)], fill=LIGHT)
    for crack in (
        [(11, 18), (14, 19), (16, 21)],
        [(34, 25), (38, 27), (40, 27)],
        [(64, 13), (67, 15), (69, 15)],
        [(85, 27), (88, 28), (90, 30)],
        [(53, 33), (57, 34), (59, 35)],
    ):
        draw.line(crack, fill=SHADE, width=1)
    for x, y in ((20, 13), (49, 19), (76, 21), (23, 35), (72, 33), (92, 13)):
        draw.rectangle((x, y, x + 2, y + 1), fill=STONE)
        draw.point((x + 1, y + 2), fill=SHADE)


def rock_face(draw: ImageDraw.ImageDraw) -> None:
    # Different-sized interlocking facets cross tile boundaries; no copied columns.
    facets = [
        ([(0, 43), (9, 42), (17, 48), (14, 57), (5, 61), (0, 58)], SHADE),
        ([(15, 42), (24, 39), (34, 44), (38, 51), (27, 55), (17, 51)], LIGHT),
        ([(36, 42), (47, 40), (56, 45), (54, 54), (43, 58), (35, 52)], SHADE),
        ([(56, 42), (65, 39), (75, 45), (70, 53), (60, 56), (53, 51)], LIGHT),
        ([(74, 42), (85, 41), (95, 46), (95, 55), (82, 57), (72, 51)], SHADE),
        ([(0, 60), (12, 55), (24, 59), (28, 66), (16, 73), (0, 70)], LIGHT),
        ([(24, 57), (35, 53), (47, 59), (44, 69), (31, 73), (20, 67)], SHADE),
        ([(47, 56), (59, 52), (67, 57), (70, 66), (58, 73), (45, 68)], LIGHT),
        ([(69, 54), (82, 52), (95, 60), (95, 71), (82, 74), (70, 67)], SHADE),
    ]
    for points, color in facets:
        draw.polygon(points, fill=color)
    # Chipped highlights on the lit sides, deep narrow cracks on opposing edges.
    for line in (
        [(1, 46), (6, 43), (11, 44)],
        [(20, 44), (25, 41), (30, 43)],
        [(39, 45), (45, 42), (49, 43)],
        [(59, 45), (64, 42), (69, 44)],
        [(7, 62), (14, 58), (20, 60)],
        [(49, 59), (57, 55), (62, 56)],
    ):
        draw.line(line, fill=LIGHT, width=2)
    for line in (
        [(15, 48), (18, 51), (14, 57), (17, 61)],
        [(37, 49), (34, 53), (39, 58), (35, 62)],
        [(54, 47), (51, 54), (54, 58)],
        [(73, 47), (70, 54), (75, 58), (72, 62)],
        [(27, 62), (30, 66), (27, 70)],
        [(65, 60), (69, 65), (65, 70)],
        [(88, 59), (86, 64), (90, 68)],
    ):
        draw.line(line, fill=DARK, width=1)
    for points in (
        [(4, 53), (9, 52), (12, 55), (9, 58)],
        [(26, 50), (31, 48), (35, 51), (32, 54)],
        [(44, 63), (49, 61), (52, 64), (49, 67)],
        [(80, 48), (85, 46), (89, 49), (85, 52)],
    ):
        draw.polygon(points, fill=STONE)


def make_panel(grass: Image.Image) -> Image.Image:
    panel = Image.new("RGBA", (W, H), CLEAR)
    draw = ImageDraw.Draw(panel)
    back_ridge = stepped_profile([
        (0, 7), (7, 5), (15, 7), (25, 4), (33, 6), (42, 5),
        (51, 7), (61, 4), (70, 6), (79, 5), (88, 7), (95, 7),
    ])
    front_lip = stepped_profile([
        (0, 39), (8, 38), (18, 40), (28, 37), (39, 39), (48, 40),
        (60, 37), (70, 39), (82, 38), (95, 39),
    ])
    foot_edge = stepped_profile([
        (0, 75), (9, 73), (20, 76), (28, 74), (38, 77), (49, 73),
        (59, 76), (70, 74), (81, 77), (90, 74), (95, 75),
    ])

    for x in range(W):
        for y in range(back_ridge[x], front_lip[x]):
            panel.putpixel((x, y), LIGHT)
        for y in range(front_lip[x], H):
            panel.putpixel((x, y), STONE)
    plateau_stones(draw)
    rock_face(draw)

    # The far silhouette exposes real alpha rather than a painted background.
    for x in range(W):
        r = back_ridge[x]
        for y in range(r):
            panel.putpixel((x, y), CLEAR)
        panel.putpixel((x, r), DARK)
        panel.putpixel((x, r + 1), SHADE)
        if x % 9 not in (0, 1):
            panel.putpixel((x, r + 2), STONE)

    # A broken lit lip and a two-pixel cast shadow make the top read as raised.
    for x in range(W):
        l = front_lip[x]
        panel.putpixel((x, l - 2), LIGHT)
        panel.putpixel((x, l - 1), STONE if x % 11 in (0, 1, 2) else LIGHT)
        panel.putpixel((x, l), SHADE)
        panel.putpixel((x, l + 1), DARK)
        if x % 7 not in (0, 1):
            panel.putpixel((x, l + 2), SHADE)

    # Let grass climb between rocks; retain the source's exact colour at y=79.
    for x in range(W):
        edge = foot_edge[x]
        for y in range(edge, H):
            panel.putpixel((x, y), grass.getpixel((x % TILE, (y - edge + 2) % TILE)))
        if x % 5 in (0, 1):
            panel.putpixel((x, edge - 1), MOSS_SHADOW)
        if x % 13 in (4, 5):
            panel.putpixel((x, edge), SHADE)
        if x % 17 in (7, 8, 9) and edge + 1 < H - 1:
            panel.putpixel((x, edge + 1), STONE)
        panel.putpixel((x, H - 1), grass.getpixel((x % TILE, 0)))
    return panel


def save_tiles(panel: Image.Image) -> None:
    names = ("FarEdge", "SummitFlat", "WallToSummit", "Wall", "FootGrass")
    OUT.mkdir(parents=True, exist_ok=True)
    for row, name in enumerate(names):
        for col in range(COLS):
            tile = panel.crop((col * TILE, row * TILE, (col + 1) * TILE, (row + 1) * TILE))
            assert tile.size == (TILE, TILE)
            tile.save(OUT / f"MountainFrontV2_{name}_{col}.png")


def make_preview(panel: Image.Image, original_grass: Image.Image) -> None:
    full = Image.new("RGBA", (W * 4, (H + TILE) * 4), (51, 60, 67, 255))
    full.alpha_composite(panel.resize((W * 4, H * 4), Image.Resampling.NEAREST))
    for col in range(COLS):
        full.alpha_composite(original_grass, (col * 64, H * 4))
    full.resize((W * SCALE, (H + TILE) * SCALE), Image.Resampling.NEAREST).save(
        OUT / "MountainFrontV2_SplicePreview.png"
    )


def main() -> None:
    original_grass, grass = grass_source()
    panel = make_panel(grass)
    assert panel.size == (W, H)
    assert all(panel.getpixel((x, H - 1)) == grass.getpixel((x % TILE, 0)) for x in range(W))
    assert all(panel.getpixel((x, 0))[3] == 0 for x in range(W))
    assert all(panel.getpixel((x, y))[3] == 255 for y in range(TILE, H) for x in range(W))
    save_tiles(panel)
    make_preview(panel, original_grass)
    print(f"Saved {COLS * 5} true 16x16 tiles and splice preview to {OUT}")


if __name__ == "__main__":
    main()
