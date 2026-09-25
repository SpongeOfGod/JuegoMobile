using UnityEngine;
using UnityEngine.UI;

namespace Gluttony
{
    [RequireComponent(typeof(CanvasRenderer))]
    public class PixelLabel : MaskableGraphic
    {
        [SerializeField, TextArea] private string text = "";
        [SerializeField] private int scale = 1;
        [SerializeField] private TextAnchor alignment = TextAnchor.MiddleCenter;
        [SerializeField] private bool shadow = true;
        [SerializeField] private Color shadowColor = Color.black;
        [SerializeField] private bool shrinkToFit = true;

        public string Text
        {
            get => text;
            set
            {
                if (text == value)
                    return;
                text = value;
                SetVerticesDirty();
            }
        }

        public int Scale
        {
            get => scale;
            set
            {
                scale = Mathf.Max(1, value);
                SetVerticesDirty();
            }
        }

        public override Texture mainTexture => PixelFont.Texture;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (string.IsNullOrEmpty(text))
                return;

            string[] lines = text.ToUpperInvariant().Split('\n');
            Rect rect = rectTransform.rect;
            int size = Mathf.Max(1, scale);
            int widest = 0;
            foreach (string line in lines)
                widest = Mathf.Max(widest, PixelFont.MeasureLine(line));
            while (shrinkToFit && size > 1 && widest * size > rect.width)
                size--;

            int lineAdvance = (PixelFont.CapHeight + 3) * size;
            int blockHeight = PixelFont.CapHeight * size + (lines.Length - 1) * lineAdvance;
            int row = (int)alignment / 3;
            int column = (int)alignment % 3;
            float top = row == 0 ? rect.yMax : row == 1 ? rect.center.y + blockHeight * 0.5f : rect.yMin + blockHeight;
            top = Mathf.Floor(top);
            Color32 main = new Color(1f, 1f, 1f, color.a);
            Color32 dark = new Color(shadowColor.r, shadowColor.g, shadowColor.b, shadowColor.a * color.a);

            for (int i = 0; i < lines.Length; i++)
            {
                int width = PixelFont.MeasureLine(lines[i]) * size;
                float left = column == 0 ? rect.xMin : column == 1 ? rect.center.x - width * 0.5f : rect.xMax - width;
                float x = Mathf.Floor(left);
                float baseline = top - PixelFont.CapHeight * size - i * lineAdvance;
                foreach (char c in lines[i])
                {
                    if (!PixelFont.TryGet(c, out var glyph))
                    {
                        x += (PixelFont.SpaceWidth + 1) * size;
                        continue;
                    }
                    if (shadow)
                        AddQuad(vh, x + size, baseline - size, glyph, size, dark);
                    AddQuad(vh, x, baseline, glyph, size, main);
                    x += (glyph.Width + 1) * size;
                }
            }
        }

        private static void AddQuad(VertexHelper vh, float x, float y, PixelFont.Glyph glyph, int size, Color32 tint)
        {
            int start = vh.currentVertCount;
            float w = glyph.Width * size;
            float h = PixelFont.GlyphHeight * size;
            vh.AddVert(new Vector3(x, y), tint, new Vector2(glyph.Uv.xMin, glyph.Uv.yMin));
            vh.AddVert(new Vector3(x, y + h), tint, new Vector2(glyph.Uv.xMin, glyph.Uv.yMax));
            vh.AddVert(new Vector3(x + w, y + h), tint, new Vector2(glyph.Uv.xMax, glyph.Uv.yMax));
            vh.AddVert(new Vector3(x + w, y), tint, new Vector2(glyph.Uv.xMax, glyph.Uv.yMin));
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start + 2, start + 3, start);
        }
    }
}
