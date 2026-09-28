using System;

// Checks saved atlas pixels, including joins between different mountain roles.
public static class MountainSeamChecks
{
    public static int Summit(int[][] top, int[][] wall)
    {
        int checks = 0;
        for (int vertical = 0; vertical < 2; vertical++)
        for (int occupancy = 0; occupancy < 1024; occupancy++)
        {
            int width = vertical == 0 ? 4 : 3, height = vertical == 0 ? 3 : 4;
            int bx = vertical == 0 ? 2 : 1, by = vertical == 0 ? 1 : 2;
            var notCliff = new bool[width, height];
            int bit = 0;
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                notCliff[x, y] = (x == 1 && y == 1) || (x == bx && y == by) || (occupancy & (1 << bit++)) != 0;
            int a = MountainFootPixels.MaskAt(notCliff, 1, 1), b = MountainFootPixels.MaskAt(notCliff, bx, by);
            for (int p = 0; p < 16; p++)
            {
                int ca = top[a][vertical == 0 ? p * 16 + 15 : 240 + p];
                int cb = top[b][vertical == 0 ? p * 16 : p];
                if (ca != cb) throw new Exception("Summit rim seam: " + a + "," + b + " pixel=" + p);
                checks++;
            }
        }
        // Straight rock-to-rock interfaces; top never injects grass into a face.
        for (int direction = 0; direction < 8; direction += 2)
        {
            int mask = MountainFootPixels.Normalise(255 ^ (1 << direction));
            for (int p = 0; p < 16; p++)
            {
                int a = direction == 0 ? p : direction == 2 ? p * 16 + 15 : direction == 4 ? 240 + p : p * 16;
                int b = direction == 0 ? 240 + p : direction == 2 ? p * 16 : direction == 4 ? p : p * 16 + 15;
                if (top[mask][a] != wall[255][b]) throw new Exception("Summit/wall interface mismatch");
                checks++;
            }
        }
        return checks;
    }

    public static int Vertical(int[][] upper, int[][] lower, bool upperIsCap)
    {
        int checks = 0;
        for (int occupancy = 0; occupancy < 1024; occupancy++)
        {
            var map = new bool[3, 4];
            int bit = 0;
            for (int y = 0; y < 4; y++)
            for (int x = 0; x < 3; x++)
                map[x, y] = (x == 1 && (y == 1 || y == 2)) || (occupancy & (1 << bit++)) != 0;
            if (upperIsCap && !map[1, 0]) continue;
            int a = MountainFootPixels.MaskAt(map, 1, 1), b = MountainFootPixels.MaskAt(map, 1, 2);
            for (int p = 0; p < 16; p++)
            {
                if (upper[a][240 + p] != lower[b][p])
                    throw new Exception("Vertical role seam: " + a + "," + b + " pixel=" + p);
                checks++;
            }
        }
        return checks;
    }

    public static int HorizontalCaps(int[][] caps)
    {
        int checks = 0;
        for (int occupancy = 0; occupancy < 1024; occupancy++)
        {
            var map = new bool[4, 3];
            int bit = 0;
            for (int y = 0; y < 3; y++)
            for (int x = 0; x < 4; x++)
                map[x, y] = (y == 1 && (x == 1 || x == 2)) || (occupancy & (1 << bit++)) != 0;
            if (!map[1, 0] || !map[2, 0]) continue;
            int a = MountainFootPixels.MaskAt(map, 1, 1), b = MountainFootPixels.MaskAt(map, 2, 1);
            for (int p = 0; p < 16; p++)
            {
                if (caps[a][p * 16 + 15] != caps[b][p * 16])
                    throw new Exception("Horizontal cap seam: " + a + "," + b + " pixel=" + p);
                checks++;
            }
        }
        return checks;
    }
}
