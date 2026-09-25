using UnityEngine;

namespace Gluttony
{
    [DefaultExecutionOrder(100)]
    public class Backdrop : MonoBehaviour
    {
        private const int EmberCount = 36;
        private const float FarTile = 2f;
        private const float WallTile = 2f;
        private const float Parallax = 0.5f;
        private const float PulseStrength = 0.35f;
        private const float FlashStrength = 0.5f;
        private const int AbyssDepth = 14;
        private const float AbyssSlideSeconds = 0.5f;
        private const int GulpPixels = 10;
        private const float GulpSeconds = 0.6f;

        [SerializeField] private CameraRig cameraRig;
        [SerializeField] private SpriteRenderer far;
        [SerializeField] private SpriteRenderer leftWall;
        [SerializeField] private SpriteRenderer rightWall;
        [SerializeField] private Sprite emberSprite;
        [SerializeField] private Material emberMaterial;

        [SerializeField] private SpriteRenderer abyss;

        private float flashStart = float.NegativeInfinity;
        private float flashLength = 1f;
        private float gulpStart = float.NegativeInfinity;
        private float abyssLevel;
        private SpriteRenderer[] embers;
        private Vector2[] emberPositions;
        private float[] emberSpeeds;
        private float lastCamY = float.NaN;

        private void Awake()
        {
            embers = new SpriteRenderer[EmberCount];
            emberPositions = new Vector2[EmberCount];
            emberSpeeds = new float[EmberCount];
            for (int i = 0; i < EmberCount; i++)
            {
                var go = new GameObject("Ember");
                go.transform.SetParent(transform, false);
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = emberSprite;
                if (emberMaterial != null)
                    renderer.sharedMaterial = emberMaterial;
                renderer.sortingOrder = -50;
                renderer.color = Random.value < 0.6f ? Palette.Gray4 : Palette.Red;
                embers[i] = renderer;
                emberSpeeds[i] = Random.Range(0.6f, 1.4f);
                emberPositions[i] = new Vector2(Random.Range(-5f, 5f), Random.Range(-9f, 9f));
            }
        }

        public void Flash(float seconds)
        {
            flashStart = Time.unscaledTime;
            flashLength = Mathf.Max(0.01f, seconds * 2f);
        }

        public void Gulp()
        {
            gulpStart = Time.unscaledTime;
            abyssLevel = 1f;
        }

        private void LateUpdate()
        {
            var rig = PixelPerfectRig.Instance;
            if (rig == null)
                return;

            float camY = cameraRig.transform.position.y;
            float halfHeight = cameraRig.HalfHeight;
            float halfWidth = rig.HalfWidthUnits;

            var conductor = Conductor.Instance;
            float pulse = conductor != null ? conductor.Pulse : 0f;
            float flash = Mathf.Clamp01(1f - (Time.unscaledTime - flashStart) / flashLength);
            var background = Color.Lerp(Palette.Gray1, Palette.Gray2, pulse * PulseStrength);
            cameraRig.Camera.backgroundColor = Color.Lerp(background, Palette.Gray3, flash * flash * FlashStrength);

            float farOffset = Mathf.Repeat(camY * Parallax, FarTile);
            far.transform.position = new Vector3(0f, PixelPerfectRig.Snap(camY - farOffset), 10f);
            far.size = new Vector2(Mathf.Ceil(halfWidth * 2f / FarTile + 1f) * FarTile, Mathf.Ceil(halfHeight * 2f / FarTile + 2f) * FarTile);

            float wallY = Mathf.Floor(camY / WallTile) * WallTile;
            float wallHeight = Mathf.Ceil(halfHeight * 2f / WallTile + 3f) * WallTile;
            int wallPixels = Mathf.Max(12, Mathf.CeilToInt((halfWidth - Lanes.PlayHalfWidth) * PixelPerfectRig.PixelsPerUnit) + 8);
            if (wallPixels % 2 == 1)
                wallPixels++;
            float wallWidth = wallPixels / (float)PixelPerfectRig.PixelsPerUnit;
            PlaceWall(leftWall, -1f, wallY, wallWidth, wallHeight);
            PlaceWall(rightWall, 1f, wallY, wallWidth, wallHeight);

            PlaceAbyss(camY - halfHeight, halfWidth);
            UpdateEmbers(camY, halfWidth, halfHeight);
        }

        private void PlaceAbyss(float bottom, float halfWidth)
        {
            if (abyss == null)
                return;
            float target = cameraRig.Creeping ? 1f : 0f;
            abyssLevel = Mathf.MoveTowards(abyssLevel, target, Time.deltaTime / Mathf.Max(0.01f, AbyssSlideSeconds));
            float t = (Time.unscaledTime - gulpStart) / GulpSeconds;
            float gulp = t >= 0f && t < 1f ? Mathf.Sin(Mathf.Sqrt(t) * Mathf.PI) : 0f;
            abyss.enabled = abyssLevel > 0f;
            if (!abyss.enabled)
                return;
            float pixels = abyss.sprite != null ? abyss.sprite.rect.height : 48f;
            float shown = abyssLevel * abyssLevel * (3f - 2f * abyssLevel);
            int rise = Mathf.RoundToInt(gulp * GulpPixels) - Mathf.RoundToInt((1f - shown) * (pixels - AbyssDepth + 1f));
            float unit = 1f / PixelPerfectRig.PixelsPerUnit;
            float y = (Mathf.Floor(bottom * PixelPerfectRig.PixelsPerUnit) - AbyssDepth + rise) * unit;
            abyss.transform.position = new Vector3(0f, y, 0f);
            abyss.size = new Vector2(Mathf.Ceil(halfWidth + 1f) * 2f, pixels * unit);
        }

        private void UpdateEmbers(float camY, float halfWidth, float halfHeight)
        {
            float dt = Time.deltaTime;
            float scroll = float.IsNaN(lastCamY) ? 0f : (camY - lastCamY) * Parallax;
            lastCamY = camY;
            for (int i = 0; i < embers.Length; i++)
            {
                Vector2 p = emberPositions[i];
                p.y += emberSpeeds[i] * dt - scroll;
                if (p.y > halfHeight + 0.5f)
                {
                    p.y = -halfHeight - Random.Range(0.1f, 2f);
                    p.x = Random.Range(-halfWidth, halfWidth);
                }
                else if (p.y < -halfHeight - 2.5f)
                {
                    p.y = halfHeight + Random.Range(0f, 0.5f);
                    p.x = Random.Range(-halfWidth, halfWidth);
                }
                emberPositions[i] = p;
                embers[i].transform.position = new Vector3(PixelPerfectRig.Snap(p.x), PixelPerfectRig.Snap(camY + p.y), 8f);
            }
        }

        private void PlaceWall(SpriteRenderer wall, float side, float y, float width, float height)
        {
            wall.transform.position = new Vector3(side * (Lanes.PlayHalfWidth + width * 0.5f), y, 0f);
            wall.size = new Vector2(width, height);
        }
    }
}
