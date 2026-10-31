using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Gluttony
{
    [RequireComponent(typeof(RawImage))]
    public class DiscView : MonoBehaviour
    {
        private const int BakeScale = 2;
        private const float StepsPerSecond = 15f;
        private const float BeatsPerTurn = 4f;
        private const float HoleEnd = 0.07f;
        private const float RimStart = 0.93f;
        private const float SheenLight = 0.22f;
        private const float SheenShade = 0.12f;
        private const float SheenSharpness = 12f;
        private const float SheenAngle = Mathf.PI * 0.25f;
        private const float RingRadius = 0.27f;
        private const float RingPixels = 0.6f;

        private static readonly Color32 BlankColor = new Color32(255, 255, 255, 255);
        private static readonly Color32 RimColor = new Color32(14, 12, 14, 255);
        private static readonly Color32 HoleColor = new Color32(8, 6, 8, 255);
        private static readonly Color32 RingColor = new Color32(232, 226, 210, 255);
        private static readonly Dictionary<long, Color32[]> Bakes = new Dictionary<long, Color32[]>();

        private RawImage image;
        private Texture2D texture;
        private Color32[] output;
        private Color32[] bake;
        private float[] radius;
        private float[] sheen;
        private int size;
        private float angle;
        private float nextStep;
        private float turnsPerSecond = 0.5f;

        public bool Spinning { get; set; }

        public void Show(Song song)
        {
            Prepare();
            turnsPerSecond = song.bpm / 60f / BeatsPerTurn;
            bake = BakeCover(song.cover, size * BakeScale);
            Redraw();
        }

        private void Prepare()
        {
            if (image != null)
                return;
            image = GetComponent<RawImage>();
            size = Mathf.Max(8, Mathf.RoundToInt(image.rectTransform.rect.width));
            output = new Color32[size * size];
            radius = new float[size * size];
            sheen = new float[size * size];
            float center = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - center) / center;
                    float dy = (y + 0.5f - center) / center;
                    radius[y * size + x] = Mathf.Sqrt(dx * dx + dy * dy);
                    sheen[y * size + x] = Mathf.Pow(Mathf.Abs(Mathf.Cos(Mathf.Atan2(dy, dx) - SheenAngle)), SheenSharpness);
                }
            }
            texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "Disc",
            };
            image.texture = texture;
        }

        private void Update()
        {
            if (!Spinning || bake == null)
                return;
            angle = Mathf.Repeat(angle + Time.unscaledDeltaTime * turnsPerSecond * Mathf.PI * 2f, Mathf.PI * 2f);
            if (Time.unscaledTime < nextStep)
                return;
            nextStep = Time.unscaledTime + 1f / StepsPerSecond;
            Redraw();
        }

        private void Redraw()
        {
            int bakeSize = size * BakeScale;
            float center = size * 0.5f;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int i = y * size + x;
                    float r = radius[i];
                    if (r > 1f)
                    {
                        output[i] = default;
                        continue;
                    }
                    if (r > RimStart)
                    {
                        output[i] = RimColor;
                        continue;
                    }
                    if (r < HoleEnd)
                    {
                        output[i] = HoleColor;
                        continue;
                    }
                    if (Mathf.Abs(r - RingRadius) * center < RingPixels)
                    {
                        output[i] = RingColor;
                        continue;
                    }
                    float dx = x + 0.5f - center;
                    float dy = y + 0.5f - center;
                    int u = Mathf.Clamp(Mathf.FloorToInt((cos * dx - sin * dy + center) * BakeScale), 0, bakeSize - 1);
                    int v = Mathf.Clamp(Mathf.FloorToInt((sin * dx + cos * dy + center) * BakeScale), 0, bakeSize - 1);
                    output[i] = Shine(bake[v * bakeSize + u], sheen[i]);
                }
            }
            texture.SetPixels32(output);
            texture.Apply(false);
        }

        private static Color32 Shine(Color32 color, float amount)
        {
            float shade = 1f - SheenShade * (1f - amount);
            float light = SheenLight * amount * 255f;
            float keep = 1f - SheenLight * amount;
            return new Color32(
                (byte)(color.r * shade * keep + light),
                (byte)(color.g * shade * keep + light),
                (byte)(color.b * shade * keep + light),
                color.a);
        }

        private static Color32[] BakeCover(Sprite cover, int bakeSize)
        {
            long key = ((long)(cover != null ? cover.GetInstanceID() : 0) << 16) ^ bakeSize;
            if (Bakes.TryGetValue(key, out var cached))
                return cached;

            var pixels = cover != null ? ReadCover(cover, bakeSize) : Blank(bakeSize);
            Bakes[key] = pixels;
            return pixels;
        }

        private static Color32[] Blank(int bakeSize)
        {
            var pixels = new Color32[bakeSize * bakeSize];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = BlankColor;
            return pixels;
        }

        private static Color32[] ReadCover(Sprite cover, int targetSize)
        {
            Texture2D source = cover.texture;
            Rect rect = cover.textureRect;
            float side = Mathf.Min(rect.width, rect.height);
            var scale = new Vector2(side / source.width, side / source.height);
            var offset = new Vector2(
                (rect.x + (rect.width - side) * 0.5f) / source.width,
                (rect.y + (rect.height - side) * 0.5f) / source.height);

            var previousActive = RenderTexture.active;
            int step = Mathf.Max(targetSize, Mathf.RoundToInt(side));
            var current = GetBuffer(step);
            Graphics.Blit(source, current, scale, offset);
            while (step > targetSize)
            {
                step = Mathf.Max(targetSize, step / 2);
                var next = GetBuffer(step);
                Graphics.Blit(current, next);
                RenderTexture.ReleaseTemporary(current);
                current = next;
            }

            RenderTexture.active = current;
            var readback = new Texture2D(targetSize, targetSize, TextureFormat.RGBA32, false);
            readback.ReadPixels(new Rect(0, 0, targetSize, targetSize), 0, 0, false);
            readback.Apply(false);
            var pixels = readback.GetPixels32();
            Destroy(readback);
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(current);
            return pixels;
        }

        private static RenderTexture GetBuffer(int side)
        {
            var buffer = RenderTexture.GetTemporary(side, side, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            buffer.filterMode = FilterMode.Bilinear;
            return buffer;
        }

        private void OnDestroy()
        {
            if (texture != null)
                Destroy(texture);
        }
    }
}
