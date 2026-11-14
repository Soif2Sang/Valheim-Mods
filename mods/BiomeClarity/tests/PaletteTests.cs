using System;
using BiomeClarity;

class PaletteTests
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static void Main()
    {
        const uint m = 1, f = 2, other = 3;
        uint[] input = { m, m, f, other, f, f };
        uint[] output = Palette.Render(input, 3, m, f, true);
        Check(output[0] == m && output[1] == m, "Native meadows preserved");
        Check(output[2] == Palette.Border, "Horizontal border");
        Check(output[4] == Palette.Border, "Vertical border");
        Check(output[3] == other, "Other biomes preserved");
        Check(input[0] == m && input[2] == f, "Original untouched for toggle/cache");
        output = Palette.Render(new uint[] { m, f }, 1, m, f, false);
        Check(output[1] == Palette.Forest, "Borders optional");
        output = Palette.Render(new uint[] { other, other, m, f, other, other }, 3, m, f, true);
        Check(output[3] == Palette.Forest, "No border wrapping between rows");
        output = Palette.Render(new uint[] { m, f, f, f, other }, 5, m, f, true);
        Check(output[1] == Palette.Border && output[2] == Palette.Border && output[3] == Palette.Forest, "Two-pixel border, bounded width");
        output = Palette.Render(new uint[] { f, f, f, m }, 4, m, f, true);
        Check(output[0] == Palette.Forest && output[1] == Palette.Border && output[2] == Palette.Border, "Meadow on right detected");
        output = Palette.Render(new uint[] { f, f, f, m }, 1, m, f, true);
        Check(output[0] == Palette.Forest && output[1] == Palette.Border && output[2] == Palette.Border, "Meadow below detected");
        bool rejected = false;
        try { Palette.Render(input, 4, m, f, true); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "Invalid dimensions rejected");
        Check(Palette.Border != Palette.Forest, "Distinct palette");
        output = Palette.Render(new uint[] { f, m }, 2, m, f, true);
        Check(output[1] == m, "Border never darkens meadow pixels");
        Check(((Palette.Forest >> 16) & 255) > ((Palette.Forest >> 8) & 255), "Forest greener than blue");
        Console.WriteLine("PASS: recoloring, borders, row edges, original preservation, optional borders, dimensions.");
    }
}
