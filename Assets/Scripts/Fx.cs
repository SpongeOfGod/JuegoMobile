using UnityEngine;

namespace Gluttony
{
    public class Fx : MonoBehaviour
    {
        private const int Capacity = 512;
        private const int SortingOrder = 20;
        private const float DefaultGravity = 8f;

        private static Fx instance;

        [SerializeField] private Sprite pixel;
        [SerializeField] private Material material;

        private struct Particle
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Age;
            public float Life;
            public float Gravity;
            public Color Color;
            public int Size;
            public int Height;
        }

        private Particle[] particles;
        private SpriteRenderer[] renderers;
        private int count;

        private void Awake()
        {
            instance = this;
            particles = new Particle[Capacity];
            renderers = new SpriteRenderer[Capacity];
            for (int i = 0; i < Capacity; i++)
            {
                var go = new GameObject("Particle");
                go.transform.SetParent(transform, false);
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = pixel;
                if (material != null)
                    renderer.sharedMaterial = material;
                renderer.sortingOrder = SortingOrder;
                renderer.enabled = false;
                renderers[i] = renderer;
            }
        }

        public static void Clear()
        {
            if (instance == null)
                return;
            for (int i = 0; i < instance.count; i++)
                instance.renderers[i].enabled = false;
            instance.count = 0;
        }

        public static void Ring(Vector3 position, Color color, int count, float speed)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2f / count;
                Emit(position, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed, color, 2, 0.18f, 0f);
            }
        }

        public static void Burst(Vector3 position, Color color, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 direction = Random.insideUnitCircle.normalized;
                Emit(position, direction * Random.Range(1.5f, 5f) + Vector2.up * 1.5f,
                    Random.value < 0.75f ? color : Palette.White, Random.value < 0.6f ? 1 : 2, Random.Range(0.25f, 0.6f), 0.8f);
            }
        }

        public static void Dust(Vector3 position, int count, float spread = 1f)
        {
            for (int i = 0; i < count; i++)
            {
                float side = Random.value < 0.5f ? -1f : 1f;
                Emit(position + new Vector3(side * Random.Range(0.05f, 0.3f), 0f, 0f),
                    new Vector2(side * Random.Range(0.6f, 2.2f) * spread, Random.Range(0.4f, 1.6f)),
                    Palette.White, 1, Random.Range(0.15f, 0.3f), 0.5f);
            }
        }

        public static void Streak(Vector3 from, Vector3 to)
        {
            float pixel = 1f / PixelPerfectRig.PixelsPerUnit;
            float length = to.y - from.y;
            int steps = Mathf.Min(40, Mathf.FloorToInt(length / (pixel * 3f)));
            for (int i = 0; i < steps; i++)
            {
                float y = from.y + (i + 0.5f) * length / steps;
                float side = i % 2 == 0 ? -6f : 6f;
                Emit(new Vector2(from.x + side * pixel, y), Vector2.zero, i % 4 < 2 ? Palette.Gray5 : Palette.Gray4, 1, Random.Range(0.12f, 0.24f), 0f);
            }
        }

        public static void Sparks(Vector3 position, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = Random.Range(200f, 340f) * Mathf.Deg2Rad;
                Emit(position, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(2f, 6f),
                    Random.value < 0.5f ? Palette.White : Palette.Red, 1, Random.Range(0.12f, 0.3f), 1f);
            }
        }

        public static void SpeedLine(Vector2 position, int length, float speed, Color color)
        {
            Emit(position, Vector2.down * speed, color, 1, 0.6f, 0f, length);
        }

        private static void Emit(Vector2 position, Vector2 velocity, Color color, int size, float life, float gravityScale, int height = 0)
        {
            if (instance == null || instance.count >= Capacity)
                return;
            int i = instance.count++;
            instance.particles[i] = new Particle
            {
                Position = position,
                Velocity = velocity,
                Life = life,
                Gravity = gravityScale,
                Color = color,
                Size = size,
                Height = height > 0 ? height : size,
            };
            instance.Paint(i);
        }

        private void Paint(int i)
        {
            var renderer = renderers[i];
            renderer.color = particles[i].Color;
            renderer.transform.localScale = new Vector3(particles[i].Size, particles[i].Height, 1f);
            renderer.enabled = true;
            Place(i);
        }

        private void Place(int i)
        {
            Vector2 p = particles[i].Position;
            renderers[i].transform.position = new Vector3(
                Mathf.Floor(p.x * PixelPerfectRig.PixelsPerUnit) / PixelPerfectRig.PixelsPerUnit,
                Mathf.Floor(p.y * PixelPerfectRig.PixelsPerUnit) / PixelPerfectRig.PixelsPerUnit, 0f);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;
            for (int i = count - 1; i >= 0; i--)
            {
                var p = particles[i];
                p.Age += dt;
                if (p.Age >= p.Life)
                {
                    count--;
                    renderers[count].enabled = false;
                    if (i != count)
                    {
                        particles[i] = particles[count];
                        Paint(i);
                    }
                    continue;
                }
                p.Velocity.y -= DefaultGravity * p.Gravity * dt;
                p.Position += p.Velocity * dt;
                particles[i] = p;
                Place(i);
            }
        }
    }
}
