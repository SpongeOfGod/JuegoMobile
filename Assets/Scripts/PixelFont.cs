using System.Collections.Generic;
using UnityEngine;

namespace Gluttony
{
    public static class PixelFont
    {
        public const int GlyphHeight = 9;
        public const int CapHeight = 7;
        public const int SpaceWidth = 3;

        public struct Glyph
        {
            public int Width;
            public Rect Uv;
        }

        private const int CellWidth = 6;
        private const int CellHeight = 10;
        private const int Columns = 16;

        private static readonly Dictionary<char, string> Shapes = new Dictionary<char, string>
        {
            ['A'] = ".###.|#...#|#...#|#####|#...#|#...#|#...#",
            ['B'] = "####.|#...#|#...#|####.|#...#|#...#|####.",
            ['C'] = ".###.|#...#|#....|#....|#....|#...#|.###.",
            ['D'] = "####.|#...#|#...#|#...#|#...#|#...#|####.",
            ['E'] = "#####|#....|#....|####.|#....|#....|#####",
            ['F'] = "#####|#....|#....|####.|#....|#....|#....",
            ['G'] = ".###.|#...#|#....|#.###|#...#|#...#|.####",
            ['H'] = "#...#|#...#|#...#|#####|#...#|#...#|#...#",
            ['I'] = "###|.#.|.#.|.#.|.#.|.#.|###",
            ['J'] = "..###|...#.|...#.|...#.|#..#.|#..#.|.##..",
            ['K'] = "#...#|#..#.|#.#..|##...|#.#..|#..#.|#...#",
            ['L'] = "#....|#....|#....|#....|#....|#....|#####",
            ['M'] = "#...#|##.##|#.#.#|#.#.#|#...#|#...#|#...#",
            ['N'] = "#...#|#...#|##..#|#.#.#|#..##|#...#|#...#",
            ['O'] = ".###.|#...#|#...#|#...#|#...#|#...#|.###.",
            ['P'] = "####.|#...#|#...#|####.|#....|#....|#....",
            ['Q'] = ".###.|#...#|#...#|#...#|#.#.#|#..#.|.##.#",
            ['R'] = "####.|#...#|#...#|####.|#.#..|#..#.|#...#",
            ['S'] = ".####|#....|#....|.###.|....#|....#|####.",
            ['T'] = "#####|..#..|..#..|..#..|..#..|..#..|..#..",
            ['U'] = "#...#|#...#|#...#|#...#|#...#|#...#|.###.",
            ['V'] = "#...#|#...#|#...#|#...#|#...#|.#.#.|..#..",
            ['W'] = "#...#|#...#|#...#|#.#.#|#.#.#|#.#.#|.#.#.",
            ['X'] = "#...#|#...#|.#.#.|..#..|.#.#.|#...#|#...#",
            ['Y'] = "#...#|#...#|.#.#.|..#..|..#..|..#..|..#..",
            ['Z'] = "#####|....#|...#.|..#..|.#...|#....|#####",
            ['0'] = ".###.|#...#|#..##|#.#.#|##..#|#...#|.###.",
            ['1'] = ".#.|##.|.#.|.#.|.#.|.#.|###",
            ['2'] = ".###.|#...#|....#|...#.|..#..|.#...|#####",
            ['3'] = "####.|....#|....#|.###.|....#|....#|####.",
            ['4'] = "...#.|..##.|.#.#.|#..#.|#####|...#.|...#.",
            ['5'] = "#####|#....|####.|....#|....#|#...#|.###.",
            ['6'] = ".###.|#....|#....|####.|#...#|#...#|.###.",
            ['7'] = "#####|....#|...#.|..#..|.#...|.#...|.#...",
            ['8'] = ".###.|#...#|#...#|.###.|#...#|#...#|.###.",
            ['9'] = ".###.|#...#|#...#|.####|....#|....#|.###.",
            ['.'] = ".|.|.|.|.|.|#",
            [','] = "..|..|..|..|..|.#|#.",
            [':'] = ".|#|.|.|.|#|.",
            ['!'] = "#|#|#|#|#|.|#",
            ['¡'] = "#|.|#|#|#|#|#",
            ['?'] = ".###.|#...#|....#|...#.|..#..|.....|..#..",
            ['¿'] = "..#..|.....|..#..|.#...|#....|#...#|.###.",
            ['-'] = "...|...|...|###|...|...|...",
            ['+'] = ".....|..#..|..#..|#####|..#..|..#..|.....",
            ['/'] = "....#|...#.|...#.|..#..|.#...|.#...|#....",
            ['('] = ".#|#.|#.|#.|#.|#.|.#",
            [')'] = "#.|.#|.#|.#|.#|.#|#.",
            ['%'] = "##..#|##..#|...#.|..#..|.#...|#..##|#..##",
            ['\''] = "#|#|.|.|.|.|.",
            ['<'] = "...#|..#.|.#..|#...|.#..|..#.|...#",
            ['>'] = "#...|.#..|..#.|...#|..#.|.#..|#...",
        };

        private static Texture2D texture;
        private static readonly Dictionary<char, Glyph> Glyphs = new Dictionary<char, Glyph>();

        public static Texture2D Texture
        {
            get
            {
                Build();
                return texture;
            }
        }

        public static bool TryGet(char c, out Glyph glyph)
        {
            Build();
            return Glyphs.TryGetValue(c, out glyph);
        }

        public static int Advance(char c) => (TryGet(c, out var glyph) ? glyph.Width : SpaceWidth) + 1;

        public static int MeasureLine(string line)
        {
            int width = 0;
            foreach (char c in line)
                width += Advance(c);
            return Mathf.Max(0, width - 1);
        }

        private static void Build()
        {
            if (texture != null)
                return;

            var bitmaps = new List<KeyValuePair<char, bool[,]>>();
            foreach (var pair in Shapes)
                bitmaps.Add(new KeyValuePair<char, bool[,]>(pair.Key, Parse(pair.Value)));
            AddMarked(bitmaps, 'Á', 'A', Acute);
            AddMarked(bitmaps, 'É', 'E', Acute);
            AddMarked(bitmaps, 'Í', 'I', Acute);
            AddMarked(bitmaps, 'Ó', 'O', Acute);
            AddMarked(bitmaps, 'Ú', 'U', Acute);
            AddMarked(bitmaps, 'Ü', 'U', Diaeresis);
            AddMarked(bitmaps, 'Ñ', 'N', Tilde);

            int rows = (bitmaps.Count + Columns - 1) / Columns;
            int width = Columns * CellWidth;
            int height = rows * CellHeight;
            texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
                name = "PixelFont",
            };
            var pixels = new Color32[width * height];

            Glyphs.Clear();
            for (int i = 0; i < bitmaps.Count; i++)
            {
                var bitmap = bitmaps[i].Value;
                int glyphWidth = bitmap.GetLength(0);
                int originX = (i % Columns) * CellWidth;
                int originY = (i / Columns) * CellHeight;
                for (int y = 0; y < GlyphHeight; y++)
                    for (int x = 0; x < glyphWidth; x++)
                        if (bitmap[x, y])
                            pixels[(originY + y) * width + originX + x] = new Color32(255, 255, 255, 255);
                Glyphs[bitmaps[i].Key] = new Glyph
                {
                    Width = glyphWidth,
                    Uv = new Rect(originX / (float)width, originY / (float)height, glyphWidth / (float)width, GlyphHeight / (float)height),
                };
            }
            texture.SetPixels32(pixels);
            texture.Apply();
        }

        private static bool[,] Parse(string shape)
        {
            string[] rows = shape.Split('|');
            int width = 0;
            foreach (string row in rows)
                width = Mathf.Max(width, row.Length);
            var bitmap = new bool[width, GlyphHeight];
            for (int r = 0; r < rows.Length && r < CapHeight; r++)
                for (int x = 0; x < rows[r].Length; x++)
                    bitmap[x, CapHeight - 1 - r] = rows[r][x] == '#';
            return bitmap;
        }

        private static void AddMarked(List<KeyValuePair<char, bool[,]>> bitmaps, char c, char baseChar, System.Action<bool[,]> mark)
        {
            var bitmap = (bool[,])Parse(Shapes[baseChar]).Clone();
            mark(bitmap);
            bitmaps.Add(new KeyValuePair<char, bool[,]>(c, bitmap));
        }

        private static void Acute(bool[,] bitmap)
        {
            int middle = bitmap.GetLength(0) / 2;
            bitmap[Mathf.Min(middle + 1, bitmap.GetLength(0) - 1), 8] = true;
            bitmap[middle, 7] = true;
        }

        private static void Diaeresis(bool[,] bitmap)
        {
            bitmap[1, 8] = true;
            bitmap[3, 8] = true;
        }

        private static void Tilde(bool[,] bitmap)
        {
            bitmap[1, 8] = true;
            bitmap[2, 8] = true;
            bitmap[4, 8] = true;
            bitmap[0, 7] = true;
            bitmap[3, 7] = true;
        }
    }
}
