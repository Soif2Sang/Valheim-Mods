using System;

namespace BiomeClarity
{
    // Pure pixel processing, also exercised without Unity by tests/PaletteTests.cs.
    internal static class Palette
    {
        internal const uint Forest = 0x36876BFF;
        internal const uint Border = 0x243142FF;

        internal static uint[] Render(uint[] source, int width, uint meadows, uint forest, bool borders)
        {
            if (source == null || width <= 0 || source.Length % width != 0)
                throw new ArgumentException("Invalid texture dimensions");
            if (meadows == forest) throw new ArgumentException("Biome colors overlap");
            uint[] result = (uint[])source.Clone();
            bool[] edge = borders ? new bool[source.Length] : null;
            for (int i = 0; i < source.Length; i++)
            {
                uint color = source[i];
                if (color != forest) continue;
                result[i] = Forest;
                if (borders) edge[i] =
                    (i % width > 0 && source[i - 1] == meadows) ||
                    (i % width < width - 1 && i + 1 < source.Length && source[i + 1] == meadows) ||
                    (i >= width && source[i - width] == meadows) ||
                    (i + width < source.Length && source[i + width] == meadows);
            }
            // Expand one pixel into the forest only; preserve native meadows and coastlines.
            if (borders) for (int i = 0; i < source.Length; i++)
                if (source[i] == forest && (edge[i] ||
                    (i % width > 0 && edge[i - 1]) ||
                    (i % width < width - 1 && i + 1 < source.Length && edge[i + 1]) ||
                    (i >= width && edge[i - width]) ||
                    (i + width < source.Length && edge[i + width]))) result[i] = Border;
            return result;
        }
    }
}

