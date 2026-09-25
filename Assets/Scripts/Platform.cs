using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gluttony
{
    public enum PlatformKind
    {
        Static,
        Moving,
        Launch,
        Landing,
    }

    public sealed class LaunchInfo
    {
        public int Beat = int.MinValue;
        public int CruiseBeats;
        public int Bars;
        public float StartY;
    }

    [DefaultExecutionOrder(-50)]
    public class Platform : MonoBehaviour
    {
        public const float Thickness = 0.375f;

        public static readonly List<Platform> All = new List<Platform>();

        [SerializeField] private SpriteRenderer body;
        [SerializeField] private Sprite litSprite;

        public PlatformKind Kind { get; private set; }
        public float Width { get; private set; }
        public float Top => transform.position.y;
        public float Left => transform.position.x - Width * 0.5f;
        public float Right => transform.position.x + Width * 0.5f;
        public Vector2 Delta { get; private set; }
        public bool Visited { get; set; }
        public LaunchInfo Launch { get; private set; }

        private float[] stops;
        private int[] pattern;
        private int beatsPerStep = 1;
        private Vector3 lastPosition;
        private Sprite normalSprite;
        private int armedFor = int.MinValue;

        private void Awake() => normalSprite = body != null ? body.sprite : null;

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        public void Setup(PlatformKind kind, float width)
        {
            Kind = kind;
            Launch = kind == PlatformKind.Launch ? new LaunchInfo() : null;
            armedFor = int.MinValue;
            Width = Mathf.Max(2f / PixelPerfectRig.PixelsPerUnit, Mathf.Round(width * PixelPerfectRig.PixelsPerUnit / 2f) * 2f / PixelPerfectRig.PixelsPerUnit);
            if (body != null)
            {
                body.sprite = normalSprite;
                body.size = new Vector2(Width, Thickness);
            }
            lastPosition = transform.position;
        }

        public void SetMoving(float[] stopsX, int[] stepPattern, int stepBeats)
        {
            stops = stopsX;
            pattern = stepPattern;
            beatsPerStep = Mathf.Max(1, stepBeats);
            var p = transform.position;
            p.x = EvaluateX();
            transform.position = p;
            lastPosition = p;
        }

        public float XAtBeat(double beats) => stops != null ? BeatStep.Evaluate(beats, beatsPerStep, stops, pattern) : transform.position.x;

        public void Arm(int launchBeat) => armedFor = launchBeat;

        public bool Contains(float x, float halfWidth) => x + halfWidth > Left && x - halfWidth < Right;

        public bool CoversLane(float x) => x > Left && x < Right;

        public float SnapToLane(float x)
        {
            int lanes = Mathf.Max(1, Mathf.RoundToInt(Width / Lanes.Width));
            float center = transform.position.x;
            float half = (lanes - 1) * 0.5f;
            int k = Mathf.Clamp(Mathf.RoundToInt((x - center) / Lanes.Width + half), 0, lanes - 1);
            return center + (k - half) * Lanes.Width;
        }

        private void Update()
        {
            if (stops != null)
            {
                var p = transform.position;
                p.x = EvaluateX();
                transform.position = p;
            }
            Delta = transform.position - lastPosition;
            lastPosition = transform.position;

            if (body == null)
                return;
            bool lit = false;
            if (litSprite != null && armedFor != int.MinValue)
            {
                var conductor = Conductor.Instance;
                double beats = conductor != null ? conductor.SongBeats : 0.0;
                if (beats < armedFor)
                    lit = Math.Floor(beats * 4.0) % 2 == 0;
                else if (beats < armedFor + 0.5)
                    lit = true;
            }
            var wanted = lit ? litSprite : normalSprite;
            if (body.sprite != wanted)
            {
                body.sprite = wanted;
                body.size = new Vector2(Width, Thickness);
            }
        }

        private float EvaluateX()
        {
            var conductor = Conductor.Instance;
            double beats = conductor != null ? conductor.SongBeats : 0.0;
            return XAtBeat(beats);
        }
    }
}
