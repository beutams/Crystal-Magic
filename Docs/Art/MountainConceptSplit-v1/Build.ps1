$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
if (-not ('MountainConceptSplitter' -as [type])) {
    Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;

public static class MountainConceptSplitter
{
    // Measured from the generated 1448x1086 reference, not the requested layout.
    static readonly int[] Cuts = { 118, 230, 420, 550, 727, 884, 975 };
    static readonly string[] Names = {
        "05-Summit-Back", "04-Summit", "03-Summit-Front",
        "02-Wall-Upper", "02-Wall-Lower", "01-Foot"
    };
    static readonly string[] Labels = {
        "05  BACK RIM", "04  SUMMIT", "03  FRONT LIP",
        "02A  UPPER WALL", "02B  LOWER WALL", "01  FOOT"
    };
    static readonly Color Grass = ColorTranslator.FromHtml("#89A043");
    static readonly Color[] Palette = Array.ConvertAll(new[] {
        "#DDC183", "#C9A967", "#B3995C", "#AD8B49", "#9B7B43",
        "#886938", "#765B35", "#615031", "#48432E",
        "#A0AE44", "#89A043", "#748630", "#596A2D", "#3F4E29"
    }, s => ColorTranslator.FromHtml(s));

    static Color Quantize(Color sample)
    {
        if (sample.A < 192) return Color.FromArgb(0, 0, 0, 0);
        double best = double.MaxValue;
        Color result = Palette[0];
        foreach (Color candidate in Palette)
        {
            double r = sample.R - candidate.R, g = sample.G - candidate.G, b = sample.B - candidate.B;
            double distance = r*r*0.30 + g*g*0.59 + b*b*0.11;
            if (distance < best) { best = distance; result = candidate; }
        }
        return result;
    }

    static void Save(Bitmap image, string path) { image.Save(path, ImageFormat.Png); }

    static Bitmap SampleMaster(Bitmap source, int size)
    {
        Bitmap master = new Bitmap(size * 8, size * 6, PixelFormat.Format32bppArgb);
        for (int row = 0; row < 6; row++)
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size * 8; x++)
        {
            int sx = Math.Min(source.Width - 1, (int)((x + 0.5) * source.Width / (size * 8)));
            int sy = Cuts[row] + (int)((y + 0.5) * (Cuts[row + 1] - Cuts[row]) / size);
            Color c = Quantize(source.GetPixel(sx, sy));
            // The last three logical 16px rows meet the project's exact grass color.
            if (row == 5 && y >= size * 13 / 16) c = Grass;
            master.SetPixel(x, row * size + y, c);
        }
        return master;
    }

    static void Blit(Bitmap source, Bitmap target, int left, int top, int scale, bool checker)
    {
        for (int y = 0; y < source.Height * scale; y++)
        for (int x = 0; x < source.Width * scale; x++)
        {
            Color c = source.GetPixel(x / scale, y / scale);
            if (c.A == 0)
            {
                if (!checker) continue;
                c = ((x / 12 + y / 12) % 2 == 0)
                    ? Color.FromArgb(211, 213, 204) : Color.FromArgb(239, 239, 229);
            }
            target.SetPixel(left + x, top + y, c);
        }
    }

    static Font Font(float size, bool bold = false)
    {
        return new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
    }

    static void Text(Bitmap target, string text, int x, int y, float size, bool bold = false)
    {
        using (Graphics g = Graphics.FromImage(target))
        using (Font f = Font(size, bold))
        using (Brush b = new SolidBrush(Color.FromArgb(43, 48, 40)))
        {
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.DrawString(text, f, b, x, y);
        }
    }

    static Bitmap Canvas(int width, int height, Color fill)
    {
        Bitmap result = new Bitmap(width, height);
        using (Graphics g = Graphics.FromImage(result)) g.Clear(fill);
        return result;
    }

    static void ExportTiles(Bitmap master, int size, string root)
    {
        string directory = Path.Combine(root, "Native" + size);
        Directory.CreateDirectory(directory);
        for (int row = 0; row < 6; row++)
        for (int column = 0; column < 8; column++)
        {
            using (Bitmap tile = master.Clone(new Rectangle(column*size, row*size, size, size), PixelFormat.Format32bppArgb))
                Save(tile, Path.Combine(directory, Names[row] + "-" + (char)('A'+column) + ".png"));
        }
        Save(master, Path.Combine(root, "Mountain-Master-" + (size*8) + "x" + (size*6) + ".png"));
    }

    static void ContactSheet(Bitmap master, int size, string root)
    {
        using (Bitmap board = Canvas(1176, 916, Color.FromArgb(244, 243, 233)))
        {
            Text(board, "MOUNTAIN / CONCEPT TO TILES", 24, 18, 26, true);
            Text(board, "48 native 16 x 16 slices  |  A-H are ordered pieces, not interchangeable variants", 24, 56, 17);
            for (int column = 0; column < 8; column++)
                Text(board, ((char)('A'+column)).ToString(), 211+column*120, 94, 18, true);
            for (int row = 0; row < 6; row++)
            {
                Text(board, Labels[row], 24, 142+row*126, 16, true);
                for (int column = 0; column < 8; column++)
                using (Bitmap tile = master.Clone(new Rectangle(column*size, row*size, size, size), PixelFormat.Format32bppArgb))
                    Blit(tile, board, 208+column*120, 126+row*126, 6, true);
            }
            Text(board, "Nearest-neighbor 6x preview. Transparency is shown as checkerboard only in this preview.", 24, 888, 15);
            Save(board, Path.Combine(root, "Mountain-Tiles-16-ContactSheet.png"));
        }
    }

    static void Assembled(Bitmap sixteen, Bitmap thirtyTwo, string root)
    {
        using (Bitmap board = Canvas(1224, 668, Color.FromArgb(244, 243, 233)))
        {
            Text(board, "MOUNTAIN / NATIVE RESOLUTION COMPARISON", 24, 18, 27, true);
            Text(board, "Same generated art, same composition, same 14-color palette; shown at equal display size.", 24, 57, 18);
            Text(board, "16 x 16 per tile / current game specification", 24, 102, 19, true);
            Text(board, "32 x 32 per tile / detail reference", 624, 102, 19, true);
            using (Bitmap scene16 = Canvas(144, 120, Grass))
            using (Bitmap scene32 = Canvas(288, 240, Grass))
            {
                Blit(sixteen, scene16, 8, 8, 1, false);
                Blit(thirtyTwo, scene32, 16, 16, 1, false);
                Blit(scene16, board, 24, 140, 4, false);
                Blit(scene32, board, 624, 140, 2, false);
            }
            Text(board, "Ordered front-section study. Side caps, corners and independent randomized placement are not part of this trial.", 24, 637, 16);
            Save(board, Path.Combine(root, "Mountain-Assembled-16-vs-32.png"));
        }
    }

    static void RepeatPreview(Bitmap master, string root)
    {
        using (Bitmap board = Canvas(1088, 488, Grass))
        {
            Blit(master, board, 32, 40, 4, false);
            Blit(master, board, 544, 40, 4, false);
            Save(board, Path.Combine(root, "Mountain-Horizontal-Repeat-Inspection.png"));
        }
    }

    static void Validate(Bitmap master, int size, string root)
    {
        int transparent = 0, seamMismatch = 0, reconstructMismatch = 0;
        for (int y = 0; y < master.Height; y++)
        {
            if (master.GetPixel(0,y).ToArgb() != master.GetPixel(master.Width-1,y).ToArgb()) seamMismatch++;
            for (int x = 0; x < master.Width; x++)
            {
                Color c = master.GetPixel(x,y);
                if (c.A != 0 && c.A != 255) throw new Exception("Non-binary alpha.");
                if (c.A == 0) transparent++;
                else if (Array.FindIndex(Palette, p => p.ToArgb() == c.ToArgb()) < 0) throw new Exception("Off-palette pixel.");
            }
        }
        for (int row = 0; row < 6; row++)
        for (int column = 0; column < 8; column++)
        using (Bitmap tile = new Bitmap(Path.Combine(root, "Native"+size, Names[row]+"-"+(char)('A'+column)+".png")))
        {
            if (tile.Width != size || tile.Height != size) throw new Exception("Wrong tile size.");
            for(int y=0;y<size;y++) for(int x=0;x<size;x++)
                if(tile.GetPixel(x,y).ToArgb()!=master.GetPixel(column*size+x,row*size+y).ToArgb()) reconstructMismatch++;
        }
        if (transparent == 0 || reconstructMismatch != 0) throw new Exception("Invalid alpha or reconstruction.");
        File.WriteAllText(Path.Combine(root, "verification-"+size+".json"),
            "{\n  \"tileSize\": "+size+",\n  \"tiles\": 48,\n  \"paletteColors\": 14,\n  \"binaryAlpha\": true,\n  \"transparentPixels\": "+transparent+",\n  \"reconstructionMismatches\": "+reconstructMismatch+",\n  \"outerRepeatEdgeMismatches\": "+seamMismatch+",\n  \"freeRandomMixingValidated\": false,\n  \"cornersIncluded\": false,\n  \"installedInUnity\": false\n}\n");
        Console.WriteLine("Native{0}: 48 tiles; exact reconstruction; {1} transparent pixels; {2} unmatched horizontal-repeat edge pairs.", size, transparent, seamMismatch);
    }

    public static void Run(string root)
    {
        using (Bitmap source = new Bitmap(Path.Combine(root, "Sources", "Mountain-Front-Generated.png")))
        {
            if (source.Width != 1448 || source.Height != 1086) throw new Exception("Source dimensions changed; revise measured cuts.");
            using (Bitmap sixteen = SampleMaster(source,16))
            using (Bitmap thirtyTwo = SampleMaster(source,32))
            {
                ExportTiles(sixteen,16,root); ExportTiles(thirtyTwo,32,root);
                ContactSheet(sixteen,16,root);
                Assembled(sixteen,thirtyTwo,root);
                RepeatPreview(sixteen,root);
                Validate(sixteen,16,root); Validate(thirtyTwo,32,root);
            }
        }
    }
}
'@
}
[MountainConceptSplitter]::Run($PSScriptRoot)
