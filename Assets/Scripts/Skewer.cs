using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Gluttony
{
    public enum MouthState
    {
        Idle,
        Open,
        Full,
        Chew,
    }

    [DefaultExecutionOrder(50)]
    [RequireComponent(typeof(CatController))]
    public class Skewer : MonoBehaviour
    {
        private const int PawX = 7;
        public const int PawY = 12;
        private const int GripPixels = 10;
        private const int OverheadX = -1;
        private const int OverheadY = 13;
        private const int ShaftPixels = 18;
        public const int HeadPixels = 26;
        private const int HeadHalfWidth = 7;
        private const int HandleWidth = 3;
        private const int TineSlots = 4;
        private const int SlotPitch = 5;
        private const int ChunkPixels = 4;
        private const int FeedRestX = 8;
        private const int FeedMouthX = -1;
        private const int FeedY = 7;
        private const int ChunkSortingOrder = 8;
        private const float AlignRange = 0.25f;
        private const float LockRange = 14f;
        private const float SlideBeats = 0.1f;
        private const float ThrustBeats = 0.25f;
        private const float Unit = 1f / PixelPerfectRig.PixelsPerUnit;

        [SerializeField] private Transform stackRoot;
        [SerializeField] private SpriteRenderer shaft;
        [SerializeField] private SpriteRenderer head;
        [SerializeField] private SpriteRenderer lockMarker;

        [SerializeField] private float lockBeats = 0.1f;
        [SerializeField] private float commitLate = 0.08f;
        [SerializeField] private float missAfter = 0.2f;

        public event Action<int, Vector3> Skewered;
        public event Action<Enemy> Missed;
        public event Action<int, int, Vector3> Bitten;
        public event Action<int, int> Feasted;

        public int Count => items.Count;
        public bool Locked => committed.Count > 0;
        public bool Eating { get; private set; }
        public float TipHeight => (OverheadY + ShaftPixels + HeadPixels) / (float)PixelPerfectRig.PixelsPerUnit;
        public int SideTipPixels => PawY - GripPixels + ShaftPixels + HeadPixels;
        public int SideShaftX => cat.FacingRight ? PawX : -PawX - 1;
        public Enemy LockedTarget { get; private set; }
        public bool LockAligned { get; private set; }

        public MouthState Mouth
        {
            get
            {
                if (!Eating || double.IsNaN(firstBite))
                    return MouthState.Idle;
                double rel = Beats - firstBite;
                if (rel < -ThrustBeats)
                    return MouthState.Idle;
                int k = (int)Math.Floor(rel + ThrustBeats);
                double u = rel - k;
                if (k < biteCount)
                    return u < 0.0 ? MouthState.Open : u < 0.5 ? MouthState.Full : MouthState.Chew;
                if (k == biteCount)
                    return u < 0.0 ? MouthState.Full : u < 0.25 ? MouthState.Open : MouthState.Idle;
                return MouthState.Idle;
            }
        }

        private CatController cat;
        private readonly List<Sprite> items = new List<Sprite>();
        private readonly List<SpriteRenderer> chunks = new List<SpriteRenderer>();
        private readonly List<Enemy> committed = new List<Enemy>();
        private double lastTake = double.NegativeInfinity;
        private double lastBite = double.NegativeInfinity;
        private double firstBite = double.NaN;
        private int biteCount;
        private int bitesTaken;
        private Sprite chunkSprite;
        private SpriteRenderer point;

        private static double Beats => Conductor.Instance != null ? Conductor.Instance.SongBeats : 0.0;
        private int OverheadShaftX => cat.FacingRight ? OverheadX : -OverheadX - 1;
        private int ShaftX => cat.Pose == TridentPose.Overhead ? OverheadShaftX : SideShaftX;
        private int ShaftBase => cat.Pose == TridentPose.Overhead ? OverheadY : cat.Pose == TridentPose.Thrust ? PawY - GripPixels + CatController.ThrustPixels : Mathf.Max(0, PawY - GripPixels - cat.CrouchPixels);
        private float ForkCenterX => cat.transform.position.x + (ShaftX + 0.5f) * Unit;
        private Vector3 MouthPoint => cat.transform.position + new Vector3(0f, (FeedY + 1.5f) * Unit, 0f);

        private void Awake()
        {
            cat = GetComponent<CatController>();
            var go = new GameObject("ForkTip");
            go.transform.SetParent(shaft.transform.parent, false);
            point = go.AddComponent<SpriteRenderer>();
            point.sprite = shaft.sprite;
            point.sharedMaterial = shaft.sharedMaterial;
            point.sortingOrder = shaft.sortingOrder;
            point.enabled = false;
        }
        private void OnEnable() => cat.Landed += OnLanded;
        private void OnDisable() => cat.Landed -= OnLanded;

        public void Clear()
        {
            StopAllCoroutines();
            items.Clear();
            committed.Clear();
            Eating = false;
            cat.Eating = false;
            LockedTarget = null;
            lastTake = double.NegativeInfinity;
            lastBite = double.NegativeInfinity;
            firstBite = double.NaN;
            biteCount = 0;
            bitesTaken = 0;
            Layout();
        }

        private void OnLanded(Platform platform)
        {
            if (platform.Kind == PlatformKind.Landing && items.Count > 0 && !Eating && cat.ControlEnabled)
                StartCoroutine(Eat());
        }

        private IEnumerator Eat()
        {
            Eating = true;
            cat.Eating = true;
            int count = items.Count;
            biteCount = Mathf.Min(count, TineSlots);
            bitesTaken = 0;
            firstBite = Math.Floor(Beats) + 1.0;
            int eaten = 0;
            int total = 0;
            for (int bite = 0; bite < biteCount; bite++)
            {
                double at = firstBite + bite;
                int size = Mathf.CeilToInt((count - eaten) / (float)(biteCount - bite));
                Sfx.PlayAtBeat(SfxId.Eat, at);
                Sfx.PlayAtBeat(SfxId.Chew, at + 0.5);
                while (Beats < at)
                    yield return null;
                items.RemoveRange(Mathf.Max(0, items.Count - size), Mathf.Min(size, items.Count));
                eaten += size;
                bitesTaken = bite + 1;
                lastBite = at;
                int points = 0;
                for (int i = 0; i < size; i++)
                    points += ScoreManager.Instance.AddBite(count);
                total += points;
                var mouth = MouthPoint;
                Fx.Burst(mouth, Palette.White, 8);
                Bitten?.Invoke(bite, points, mouth);
                while (Beats < at + 0.5)
                    yield return null;
                Fx.Dust(MouthPoint, 3, 0.6f);
            }
            double end = firstBite + biteCount;
            Sfx.PlayAtBeat(SfxId.Feast, end);
            Sfx.PlayAtBeat(SfxId.Burp, end + 1.0);
            while (Beats < end)
                yield return null;
            Fx.Burst(cat.BodyCenter, Palette.Red, 30);
            Fx.Ring(cat.BodyCenter, Palette.White, 24, 8f);
            Feasted?.Invoke(count, total);
            while (Beats < end + 0.25)
                yield return null;
            firstBite = double.NaN;
            cat.Eating = false;
            Eating = false;
        }

        private void Update()
        {
            if (!cat.ControlEnabled)
                return;
            double beats = Beats;
            if (cat.IsLaunching)
                Commit(beats);
            Resolve(beats);
            CheckHazards();
        }

        private void LateUpdate()
        {
            Layout();
            UpdateLock();
        }

        private void Commit(double beats)
        {
            float fork = ForkCenterX;
            for (int i = Enemy.All.Count - 1; i >= 0; i--)
            {
                var enemy = Enemy.All[i];
                if (!enemy.Edible || enemy.Committed || enemy.Missed)
                    continue;
                double impact = enemy.ImpactBeat;
                if (double.IsNaN(impact))
                    continue;
                double lead = impact - beats;
                if (lead < -commitLate)
                {
                    if (lead < -missAfter)
                    {
                        enemy.Miss();
                        Sfx.Play(SfxId.Miss, 1f, 0.5f);
                        Missed?.Invoke(enemy);
                    }
                    continue;
                }
                if (lead > lockBeats)
                    continue;
                if (Mathf.Abs(enemy.HitRect.center.x - fork) > AlignRange)
                    continue;
                enemy.Committed = true;
                committed.Add(enemy);
                Sfx.PlayAtBeat(SfxId.Skewer, impact, Sfx.Note(items.Count + committed.Count - 1));
            }
        }

        private void Resolve(double beats)
        {
            for (int i = committed.Count - 1; i >= 0; i--)
            {
                var enemy = committed[i];
                if (enemy == null)
                {
                    committed.RemoveAt(i);
                    continue;
                }
                if (beats < enemy.ImpactBeat)
                    continue;
                committed.RemoveAt(i);
                Take(enemy, beats);
            }
        }

        private void Take(Enemy enemy, double beats)
        {
            Vector2 at = enemy.HitRect.center;
            chunkSprite = enemy.Chunk;
            enemy.Consume();
            items.Add(chunkSprite);
            lastTake = beats;
            Fx.Ring(at, Palette.White, 16, 7f);
            Fx.Burst(at, Palette.Red, 14);
            ScoreManager.Instance.AddSkewer();
            Skewered?.Invoke(items.Count, at);
        }

        private void CheckHazards()
        {
            Rect body = cat.BodyRect;
            Rect fork = ForkRect();
            for (int i = Enemy.All.Count - 1; i >= 0; i--)
            {
                if (i >= Enemy.All.Count)
                    continue;
                var enemy = Enemy.All[i];
                if (enemy.Edible || !enemy.Solid)
                    continue;
                Rect hit = enemy.HitRect;
                if (hit.Overlaps(body) || hit.Overlaps(fork))
                    enemy.OnCatContact(this);
            }
        }

        private void UpdateLock()
        {
            LockedTarget = null;
            LockAligned = false;
            if (lockMarker == null)
                return;
            if (cat.IsLaunching && cat.ControlEnabled)
            {
                double beats = Beats;
                double best = double.MaxValue;
                foreach (var enemy in Enemy.All)
                {
                    if (!enemy.Edible || enemy.Missed)
                        continue;
                    double impact = enemy.ImpactBeat;
                    if (double.IsNaN(impact) || impact < beats - commitLate || impact >= best)
                        continue;
                    if (enemy.HitRect.yMin - (cat.transform.position.y + TipHeight) > LockRange)
                        continue;
                    best = impact;
                    LockedTarget = enemy;
                }
                if (LockedTarget != null)
                    LockAligned = LockedTarget.Committed || Mathf.Abs(LockedTarget.HitRect.center.x - ForkCenterX) <= AlignRange;
            }

            lockMarker.enabled = LockedTarget != null;
            if (LockedTarget == null)
                return;
            Vector2 c = LockedTarget.HitRect.center;
            lockMarker.transform.position = new Vector3(PixelPerfectRig.Snap(c.x), PixelPerfectRig.Snap(c.y), 0f);
            lockMarker.color = LockAligned ? Palette.Red : Palette.White;
        }

        private Rect ForkRect()
        {
            Vector2 origin = (Vector2)cat.transform.position + new Vector2((ShaftX - HeadHalfWidth) * Unit, ShaftBase * Unit);
            return new Rect(origin, new Vector2((HeadHalfWidth * 2 + 1) * Unit, (ShaftPixels + HeadPixels) * Unit));
        }

        private int FeedThrust()
        {
            if (double.IsNaN(firstBite))
                return 0;
            double rel = Beats - firstBite;
            int k = (int)Math.Floor(rel + ThrustBeats);
            if (k < 0 || k >= biteCount)
                return 0;
            double u = rel - k;
            int reach = FeedRestX - FeedMouthX;
            if (u < 0.0)
            {
                float t = (float)((u + ThrustBeats) / ThrustBeats);
                return Mathf.RoundToInt(reach * t * t);
            }
            if (u < ThrustBeats)
            {
                float t = 1f - (float)(u / ThrustBeats);
                return Mathf.RoundToInt(reach * t * t);
            }
            return 0;
        }

        private void Layout()
        {
            var pose = cat.Pose;
            bool visible = pose != TridentPose.Hidden;
            head.enabled = visible;
            if (pose == TridentPose.Feeding)
            {
                LayoutFeeding();
                return;
            }
            head.transform.localRotation = Quaternion.identity;

            int x = ShaftX;
            int baseY = ShaftBase;
            int length = ShaftPixels;
            if (pose == TridentPose.Stuck)
            {
                Vector2 snapped = new Vector2(PixelPerfectRig.Snap(cat.transform.position.x), PixelPerfectRig.Snap(cat.transform.position.y));
                Vector2 local = (cat.StuckHead - snapped) * PixelPerfectRig.PixelsPerUnit;
                x = Mathf.RoundToInt(local.x);
                baseY = PawY;
                length = Mathf.Max(0, Mathf.RoundToInt(local.y) - PawY);
            }

            Quad(shaft, x - HandleWidth / 2, baseY + 1, HandleWidth, length - 1, visible && length > 1);
            Quad(point, x, baseY, 1, 1, visible && length > 0);
            head.transform.localPosition = Pixel(x, baseY + length);

            int count = visible ? Mathf.Min(items.Count, TineSlots) : 0;
            float since = (float)(Beats - lastTake);
            int push = since >= 0f && since < SlideBeats ? Mathf.RoundToInt(SlotPitch * (1f - since / SlideBeats)) : 0;
            PlaceChunks(count, new Vector2Int(x, baseY + length), 0, push, 1);
        }

        private void LayoutFeeding()
        {
            int sign = cat.FacingRight ? 1 : -1;
            int tip = FeedRestX - FeedThrust();
            int pivotX = sign * (tip + HeadPixels);
            int pivotY = sign > 0 ? FeedY : FeedY + 1;

            int bar = ShaftPixels - 1;
            Quad(shaft, sign > 0 ? pivotX : pivotX - bar, FeedY - HandleWidth / 2, bar, HandleWidth, true);
            Quad(point, sign > 0 ? pivotX + bar : pivotX - ShaftPixels, FeedY, 1, 1, true);
            head.transform.localRotation = Quaternion.Euler(0f, 0f, 90f * sign);
            head.transform.localPosition = Pixel(pivotX, pivotY);

            int count = Mathf.Max(0, biteCount - bitesTaken);
            float since = (float)(Beats - lastBite);
            int pull = since >= 0f && since < ThrustBeats ? -Mathf.RoundToInt(SlotPitch * (1f - since / ThrustBeats)) : 0;
            PlaceChunks(count, new Vector2Int(pivotX, pivotY), sign, pull, 0);
        }

        private void PlaceChunks(int count, Vector2Int headPixel, int quarter, int offset, int offsetFrom)
        {
            EnsureChunks(count);
            var rotation = Quaternion.Euler(0f, 0f, 90f * quarter);
            for (int i = 0; i < chunks.Count; i++)
            {
                bool on = i < count;
                chunks[i].enabled = on;
                if (!on)
                    continue;
                int bottom = HeadPixels - 1 - ChunkPixels - i * SlotPitch + (i >= offsetFrom ? offset : 0);
                Vector2Int along = quarter == 0 ? new Vector2Int(0, bottom) : quarter > 0 ? new Vector2Int(-bottom, 0) : new Vector2Int(bottom, 0);
                int index = items.Count - 1 - i;
                chunks[i].sprite = index >= 0 ? items[index] : chunkSprite;
                chunks[i].sortingOrder = ChunkSortingOrder;
                chunks[i].transform.localRotation = rotation;
                chunks[i].transform.localPosition = Pixel(headPixel.x + along.x, headPixel.y + along.y);
            }
        }

        private void EnsureChunks(int count)
        {
            while (chunks.Count < count)
            {
                var go = new GameObject("Chunk");
                go.transform.SetParent(stackRoot, false);
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sharedMaterial = shaft.sharedMaterial;
                chunks.Add(renderer);
            }
        }

        private static void Quad(SpriteRenderer renderer, int x, int y, int width, int height, bool on)
        {
            renderer.enabled = on;
            if (!on)
                return;
            renderer.transform.localPosition = Pixel(x, y);
            renderer.transform.localScale = new Vector3(width, height, 1f);
        }

        private static Vector3 Pixel(int x, int y) => new Vector3(x * Unit, y * Unit, 0f);
    }
}
