using System;
using System.Collections.Generic;

// Offline, deterministic slicing/edge-normalisation helper. Not a Unity runtime script.
public static class MountainFootPixels
{
    // Clockwise from north. Bitmap Y points down; Unity rule positions use the opposite Y.
    public static readonly int[] DX = { 0, 1, 1, 1, 0, -1, -1, -1 };
    public static readonly int[] DY = { -1, -1, 0, 1, 1, 1, 0, -1 };
    public static int Normalise(int mask)
    {
        for (int i = 1; i < 8; i += 2)
            if ((mask & (1 << (i - 1))) == 0 || (mask & (1 << ((i + 1) % 8))) == 0)
                mask &= ~(1 << i);
        return mask;
    }

    public static int[] Masks()
    {
        var result = new List<int>();
        for (int i = 0; i < 256; i++)
            if (Normalise(i) == i) result.Add(i);
        return result.ToArray();
    }

    public static int Distance(int mask, int x, int y)
    {
        int distance = 32;
        for (int i = 0; i < 8; i++)
        {
            if ((mask & (1 << i)) != 0) continue;
            int left = DX[i] * 16, top = DY[i] * 16;
            int dx = Math.Max(left - x, Math.Max(x - (left + 15), 0));
            int dy = Math.Max(top - y, Math.Max(y - (top + 15), 0));
            distance = Math.Min(distance, Math.Max(dx, dy));
        }
        return distance;
    }

    public static int[][] Render(int[] sourceRock, int grass, int grassShadow, int[] rockPalette)
    {
        var tiles = new int[256][];
        foreach (int mask in Masks())
        {
            var pixels = new int[256];
            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                int d = Distance(mask, x, y);
                int color = sourceRock[y * 16 + x];
                if (d < 4) color = grass;
                else if (d == 4) color = grassShadow;
                else if (d <= 6)
                {
                    bool west = Distance(mask, x - 1, y) < d;
                    bool east = Distance(mask, x + 1, y) < d;
                    if (west || east)
                        color = d == 5 ? rockPalette[3] : west ? rockPalette[1] : rockPalette[2];
                    else if (d == 5) color = rockPalette[2];
                }
                pixels[y * 16 + x] = color;
            }
            tiles[mask] = pixels;
        }
        return tiles;
    }

    // For summits a zero bit means an adjacent cliff face, NOT empty space.
    // Consequently grass/void do not create a rim; diagonal-only faces do.
    public static int[][] RenderSummit(int[] rock, int summitColor, int[] palette)
    {
        var tiles = new int[256][];
        foreach (int mask in Masks())
        {
            var pixels = new int[256];
            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                int distance = Distance(mask, x, y);
                pixels[y * 16 + x] = distance <= 3 ? rock[y * 16 + x]
                    : distance == 4 ? palette[2] : summitColor;
            }
            tiles[mask] = pixels;
        }
        return tiles;
    }

    public static int SummitMaskAt(bool[,] map, int[,] parts, int x, int y)
    {
        int mask = 255;
        for (int i = 0; i < 8; i++)
        {
            int nx = x + DX[i], ny = y + DY[i];
            if (nx >= 0 && ny >= 0 && nx < map.GetLength(0) && ny < map.GetLength(1)
                && map[nx, ny] && parts[nx, ny] != 2)
                mask &= ~(1 << i);
        }
        return Normalise(mask);
    }

    public static int MaskAt(bool[,] map, int x, int y)
    {
        int mask = 0;
        for (int i = 0; i < 8; i++)
        {
            int nx = x + DX[i], ny = y + DY[i];
            if (nx >= 0 && ny >= 0 && nx < map.GetLength(0) && ny < map.GetLength(1) && map[nx, ny])
                mask |= 1 << i;
        }
        return Normalise(mask);
    }

    public static int VerifySeams(int[][] tiles, int grass)
    {
        int checkedPixels = 0;
        // Exhaust all 4x3 / 3x4 neighbourhoods around two adjacent occupied cells.
        for (int vertical = 0; vertical < 2; vertical++)
        for (int occupancy = 0; occupancy < 1024; occupancy++)
        {
            int width = vertical == 0 ? 4 : 3, height = vertical == 0 ? 3 : 4;
            int bx = vertical == 0 ? 2 : 1, by = vertical == 0 ? 1 : 2;
            var map = new bool[width, height];
            int bit = 0;
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                map[x, y] = (x == 1 && y == 1) || (x == bx && y == by) || (occupancy & (1 << bit++)) != 0;
            int a = MaskAt(map, 1, 1), b = MaskAt(map, bx, by);
            for (int p = 0; p < 16; p++)
            {
                int ca = tiles[a][vertical == 0 ? p * 16 + 15 : 15 * 16 + p];
                int cb = tiles[b][vertical == 0 ? p * 16 : p];
                if (ca != cb)
                    throw new Exception("Seam mismatch: axis=" + vertical + " masks=" + a + "," + b + " pixel=" + p);
                checkedPixels++;
            }
        }
        foreach (int mask in Masks())
        for (int direction = 0; direction < 8; direction += 2)
        {
            if ((mask & (1 << direction)) != 0) continue;
            for (int p = 0; p < 16; p++)
            {
                int index = direction == 0 ? p : direction == 2 ? p * 16 + 15 : direction == 4 ? 240 + p : p * 16;
                if (tiles[mask][index] != grass) throw new Exception("Exterior edge is not ground colour.");
                checkedPixels++;
            }
        }
        return checkedPixels;
    }
}
