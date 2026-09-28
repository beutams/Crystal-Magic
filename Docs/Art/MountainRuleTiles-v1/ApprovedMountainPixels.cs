using System;
using System.Collections.Generic;

// Offline assembly of the approved v1 pixels; no random/procedural rock texture.
public sealed class ApprovedMountainPixels
{
    public const int CaseCount = 6561;
    public static readonly int[] DX = { 0, 1, 1, 1, 0, -1, -1, -1 };
    public static readonly int[] DY = { -1, -1, 0, 1, 1, 1, 0, -1 };
    private static readonly int[] EdgeOrder = { 0, 2, 4, 6, 1, 3, 5, 7 };
    public readonly List<int[]>[] Tiles = { new List<int[]>(), new List<int[]>(), new List<int[]>() };
    public readonly int[][] Lookup = { new int[CaseCount], new int[CaseCount], new int[CaseCount] };
    private readonly int[] foot, wall, cap, back;
    public readonly int Grass, Summit, Rock, Shade;

    public ApprovedMountainPixels(int[] footPixels, int[] wallPixels, int[] capPixels, int[] summitPixels, int[] backPixels, int[] grassPixels)
    {
        foot = (int[])footPixels.Clone(); wall = (int[])wallPixels.Clone();
        cap = (int[])capPixels.Clone(); back = (int[])backPixels.Clone();
        Grass = grassPixels[0]; Summit = summitPixels[0]; Rock = wall[0];
        Shade = unchecked((int)0xff805b32);
        // Keep the approved centre; join only the two boundary columns when
        // extending the one-column sample into a horizontally tiling surface.
        for (int y = 0; y < 16; y++)
        {
            wall[y * 16] = wall[y * 16 + 15] = Rock;
            foot[y * 16] = foot[y * 16 + 15] = y < 12 ? Rock : y == 12 ? unchecked((int)0xff647b36) : Grass;
            cap[y * 16 + 15] = cap[y * 16];
            back[y * 16 + 15] = back[y * 16];
        }
        for (int part = 0; part < 3; part++)
        {
            var unique = new Dictionary<string, int>();
            for (int key = 0; key < CaseCount; key++)
            {
                int[] pixels = Render(part, Decode(key));
                byte[] bytes = new byte[1024]; Buffer.BlockCopy(pixels, 0, bytes, 0, 1024);
                string hash = Convert.ToBase64String(bytes);
                int index;
                if (!unique.TryGetValue(hash, out index))
                {
                    index = Tiles[part].Count; unique.Add(hash, index); Tiles[part].Add(pixels);
                }
                Lookup[part][key] = index;
            }
        }
    }

    public static int[] Decode(int key)
    {
        var states = new int[8];
        for (int i = 0; i < 8; i++) { states[i] = key % 3; key /= 3; }
        return states;
    }
    public static int Encode(int[] states)
    {
        int key = 0, multiplier = 1;
        for (int i = 0; i < 8; i++) { key += states[i] * multiplier; multiplier *= 3; }
        return key;
    }
    public static int Distance(int direction, int x, int y)
    {
        int left = DX[direction] * 16, top = DY[direction] * 16;
        int dx = Math.Max(left - x, Math.Max(x - left - 15, 0));
        int dy = Math.Max(top - y, Math.Max(y - top - 15, 0));
        return Math.Max(dx, dy);
    }
    private int Interface(int distance, int edgeColor, int original)
    {
        if (edgeColor == Summit || distance > 5) return original;
        return distance < 5 ? edgeColor : Shade;
    }
    private int[] Render(int part, int[] s)
    {
        bool isFoot = part != 2 && s[4] == 0;
        bool isCap = part != 2 && !isFoot && s[0] != 1;
        int[] basis = isFoot ? foot : isCap ? cap : wall;
        var result = new int[256];
        for (int y = 0; y < 16; y++)
        for (int x = 0; x < 16; x++)
        {
            int color = part == 2 ? Summit : basis[y * 16 + x];
            if (part == 2 || isCap)
            {
                // A cliff to the south already uses the front-cap tile itself.
                // Its north edge is summit-coloured: do not draw a second lip.
                if (part == 2 && s[0] == 1) color = Interface(y + 1, Rock, color);
                if (s[2] == 1 && (part == 2 || s[1] == 1))
                    color = Interface(16 - x, s[1] == 1 ? Rock : cap[y * 16], color);
                if (s[6] == 1 && (part == 2 || s[7] == 1))
                    color = Interface(x + 1, s[7] == 1 ? Rock : cap[y * 16 + 15], color);
                if (s[0] != 1 && s[2] != 1 && s[1] == 1)
                    color = Interface(Distance(1, x, y), Rock, color);
                if (s[0] != 1 && s[6] != 1 && s[7] == 1)
                    color = Interface(Distance(7, x, y), Rock, color);
            }

            // The approved foot keeps its grass strip. Higher parts have a real
            // alpha silhouette, including side insets and re-entrant corners.
            if (isFoot && y >= 12) { result[y * 16 + x] = foot[y * 16 + x]; continue; }
            int nearest = -1, distance = 32;
            foreach (int direction in EdgeOrder)
            {
                if (s[direction] != 0 || (isFoot && direction == 4)) continue;
                int d = Distance(direction, x, y);
                if (d < distance) { distance = d; nearest = direction; }
            }
            if (nearest >= 0 && distance <= 5)
            {
                int along;
                if (nearest == 0 || nearest == 4) along = x;
                else if (nearest == 2 || nearest == 6) along = y;
                else
                {
                    int dx = DX[nearest] > 0 ? 16 - x : x + 1;
                    int dy = DY[nearest] > 0 ? 16 - y : y + 1;
                    along = dy >= dx ? x : y;
                }
                int edge = back[(distance - 1) * 16 + along];
                color = (edge & unchecked((int)0xff000000)) == 0 ? (isFoot ? Grass : 0) : edge;
            }
            result[y * 16 + x] = color;
        }
        // A shared boundary is selected from the same six cells on both sides.
        // This matters at height changes: independent nearest-edge decisions
        // can otherwise disagree by one pixel at a re-entrant corner.
        int centre = part == 2 ? 2 : 1;
        for (int p = 0; p < 16; p++)
        {
            if (s[2] != 0) result[p*16+15] = GroundAlpha(HorizontalEdge(centre,s[2],s[0],s[1],s[4],s[3],p),isFoot);
            if (s[6] != 0) result[p*16] = GroundAlpha(HorizontalEdge(s[6],centre,s[7],s[0],s[5],s[4],p),isFoot);
            if (s[0] != 0) result[p] = GroundAlpha(VerticalEdge(s[0],centre,s[7],s[6],s[1],s[2],p),isFoot);
            if (s[4] != 0) result[240+p] = GroundAlpha(VerticalEdge(centre,s[4],s[6],s[5],s[2],s[3],p),isFoot);
        }
        result[0] = GroundAlpha(Vertex(s[7],s[0],s[6],centre),isFoot);
        result[15] = GroundAlpha(Vertex(s[0],s[1],centre,s[2]),isFoot);
        result[240] = GroundAlpha(Vertex(s[6],centre,s[5],s[4]),isFoot);
        result[255] = GroundAlpha(Vertex(centre,s[2],s[4],s[3]),isFoot);
        return result;
    }

    private int GroundAlpha(int color, bool isFoot) { return color == 0 && isFoot ? Grass : color; }
    private int OuterEdge(int distance) { return back[(distance-1)*16]; }
    private int Vertex(int tl, int tr, int bl, int br)
    {
        if (tl == 0 || tr == 0 || bl == 0 || br == 0) return 0;
        return tl == 1 || tr == 1 ? Rock : Summit;
    }
    private int HorizontalEdge(int left, int right, int aboveLeft, int aboveRight, int belowLeft, int belowRight, int y)
    {
        if ((aboveLeft == 0 || aboveRight == 0) && y < 5) return OuterEdge(y+1);
        if (left == 1 && right == 1 && belowLeft == 0 && belowRight == 0) return foot[y*16];
        if ((belowLeft == 0 || belowRight == 0) && y >= 11) return OuterEdge(16-y);
        bool leftWall = left == 1 && aboveLeft == 1 && belowLeft != 0;
        bool rightWall = right == 1 && aboveRight == 1 && belowRight != 0;
        if (leftWall || rightWall) return Rock;
        if ((aboveLeft == 1 || aboveRight == 1) && y < 5) return y < 4 ? Rock : Shade;
        if (left == 2 && right == 2) return Summit;
        if ((left == 1 && belowLeft == 0) || (right == 1 && belowRight == 0)) return foot[y*16];
        return cap[y*16];
    }
    private int VerticalEdge(int above, int below, int leftAbove, int leftBelow, int rightAbove, int rightBelow, int x)
    {
        if ((leftAbove == 0 || leftBelow == 0) && x < 5) return OuterEdge(x+1);
        if ((rightAbove == 0 || rightBelow == 0) && x >= 11) return OuterEdge(16-x);
        int color = above == 1 ? Rock : Summit;
        if (color == Summit && leftAbove == 1 && x < 5) return x < 4 ? Rock : Shade;
        if (color == Summit && rightAbove == 1 && x >= 11) return x > 11 ? Rock : Shade;
        return color;
    }

    public int[] Atlas(int part, int columns)
    {
        int width = columns * 16, height = ((Tiles[part].Count + columns - 1) / columns) * 16;
        var pixels = new int[width * height];
        for (int i = 0; i < Tiles[part].Count; i++)
        for (int y = 0; y < 16; y++)
            Array.Copy(Tiles[part][i], y * 16, pixels, ((i / columns * 16 + y) * width) + i % columns * 16, 16);
        return pixels;
    }
    public static int KeyAt(int[,] parts, int x, int y)
    {
        int key = 0, multiplier = 1;
        for (int d = 0; d < 8; d++)
        {
            int nx = x + DX[d], ny = y + DY[d];
            int p = nx < 0 || ny < 0 || nx >= parts.GetLength(0) || ny >= parts.GetLength(1) ? -1 : parts[nx, ny];
            key += (p < 0 ? 0 : p == 2 ? 2 : 1) * multiplier; multiplier *= 3;
        }
        return key;
    }
    public int[] Preview(int[,] parts, int background)
    {
        int width = parts.GetLength(0) * 16, height = parts.GetLength(1) * 16;
        var pixels = new int[width * height];
        for (int cy = 0; cy < parts.GetLength(1); cy++)
        for (int cx = 0; cx < parts.GetLength(0); cx++)
        {
            int part = parts[cx, cy];
            int[] tile = part < 0 ? null : Tiles[part][Lookup[part][KeyAt(parts, cx, cy)]];
            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                int color = tile == null ? 0 : tile[y * 16 + x];
                pixels[(cy * 16 + y) * width + cx * 16 + x] = color == 0 ? background : color;
            }
        }
        return pixels;
    }
    public int[] Variant(int part, int key) { return Tiles[part][Lookup[part][key]]; }

    public int VerifyAtlas(int part, int[] pixels, int width, int columns)
    {
        int checks = 0;
        for (int i = 0; i < Tiles[part].Count; i++)
        for (int y = 0; y < 16; y++)
        for (int x = 0; x < 16; x++)
        {
            int actual = pixels[(i/columns*16+y)*width+i%columns*16+x];
            if (actual != Tiles[part][i][y*16+x]) throw new Exception("Saved atlas differs from baked tile: part="+part+" variant="+i);
            if (part == 2 && actual == Grass) throw new Exception("Summit must not contain ground grass.");
            int alpha = (int)((uint)actual >> 24);
            if (alpha != 0 && alpha != 255) throw new Exception("Only binary alpha is allowed.");
            checks++;
        }
        return checks;
    }

    public string VerifySeams()
    {
        long checks = 0; int failures = 0; string first = null;
        // All ternary neighbourhoods around adjacent cliff/summit cells. Alpha
        // is composited over the agreed grass for the ground-level foot joins.
        for (int axis = 0; axis < 2; axis++)
        for (int partA = 1; partA <= 2; partA++)
        for (int partB = 1; partB <= 2; partB++)
        for (int code = 0; code < 59049; code++)
        {
            int width = axis == 0 ? 4 : 3, height = axis == 0 ? 3 : 4;
            int bx = axis == 0 ? 2 : 1, by = axis == 0 ? 1 : 2;
            var map = new int[width, height]; int remaining = code;
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                if (x == 1 && y == 1) map[x,y] = partA;
                else if (x == bx && y == by) map[x,y] = partB;
                else { int state = remaining % 3; remaining /= 3; map[x,y] = state == 0 ? -1 : state; }
            }
            int keyA = KeyAt(map,1,1), keyB = KeyAt(map,bx,by);
            int[] a = Variant(partA,keyA), b = Variant(partB,keyB);
            for (int p = 0; p < 16; p++)
            {
                int ca = a[axis == 0 ? p*16+15 : 240+p], cb = b[axis == 0 ? p*16 : p];
                if (ca == 0) ca = Grass; if (cb == 0) cb = Grass;
                if (ca != cb)
                {
                    failures++;
                    if (first == null) first = "axis="+axis+" parts="+partA+","+partB+" keys="+keyA+","+keyB+" pixel="+p+" colours="+ca.ToString("X8")+","+cb.ToString("X8");
                }
                checks++;
            }
        }
        string report = "checks="+checks+" mismatches="+failures+" first="+(first ?? "none");
        if (failures != 0) throw new InvalidOperationException(report);
        return report;
    }
}
