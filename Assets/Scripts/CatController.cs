using System;
using UnityEngine;

namespace Gluttony
{
    public enum CatState
    {
        Grounded,
        Stabbing,
        Flipping,
        Airborne,
        Intro,
        Blasting,
        Launching,
        Braking,
    }

    public enum TridentPose
    {
        Side,
        Thrust,
        Overhead,
        Feeding,
        Stuck,
        Hidden,
    }

    [RequireComponent(typeof(CatInput))]
    public class CatController : MonoBehaviour
    {
        public const int IntroBeats = 4;
        public const float RowHeight = 3.5f;
        private const float TickLead = 0.12f;
        private const float ChargeBarDelay = 0.1f;
        public const int ThrustPixels = 8;
        private const int BitePixels = 3;
        private const float FlipArc = 1.5f;
        private const float HopHeight = 0.6f;
        private const int MinBlastBeats = 2;
        private const float BlastUnitsPerBeat = 36f;
        private const float BrakeBeats = 0.5f;
        public const float TableDrop = 2.5f;
        private const float TableFallBeats = 0.5f;
        private const float IntroHop = 1.5f;
        private const float TossHeight = 5f;
        private const float TossSeconds = 0.6f;
        private const int TossBounces = 3;
        private const float SpeedLinesPerSecond = 90f;
        private const float WindPerSecond = 60f;
        private const float GhostEveryBeats = 0.0625f;
        private const float GhostLifeBeats = 0.3f;
        private const int GhostCount = 10;
        private const float GhostAlpha = 0.5f;
        private const float FallGravity = 24f;
        private const float MaxFallUnitsPerBeat = 12f;
        private const float BumpSeconds = 0.12f;
        private const int BumpPixels = 3;
        private const float HalfWidth = 0.28f;
        private const float BodyHeight = 0.85f;
        private const float Unit = 1f / PixelPerfectRig.PixelsPerUnit;

        public static CatController Instance { get; private set; }

        [SerializeField] private int chargeBeats = 3;
        [SerializeField] private float earlyWindow = 0.25f;
        [SerializeField] private float lateWindow = 0.3f;
        [SerializeField] private float touchGrace = 0.2f;
        [SerializeField] private float launchUnitsPerBeat = 8f;
        [SerializeField] private float laneSeconds = 0.08f;
        [SerializeField] private SpriteRenderer sprite;
        [SerializeField] private Sprite idleSprite;
        [SerializeField] private Sprite walkSprite;
        [SerializeField] private Sprite[] crouchSprites = new Sprite[0];
        [SerializeField] private Sprite airSprite;
        [SerializeField] private Sprite launchSprite;
        [SerializeField] private Sprite holdSprite;
        [SerializeField] private Sprite eatOpenSprite;
        [SerializeField] private Sprite eatFullSprite;
        [SerializeField] private Sprite eatChewSprite;
        [SerializeField] private Sprite faceplantSprite;
        [SerializeField] private SpriteRenderer looseFork;
        [SerializeField] private Sprite[] flipSprites = new Sprite[0];
        [SerializeField] private GameObject chargeBar;
        [SerializeField] private SpriteRenderer chargeFrame;
        [SerializeField] private SpriteRenderer[] chargeSegments = new SpriteRenderer[0];

        public event Action<Platform> Landed;
        public event Action Jumped;
        public event Action<bool> Stabbed;
        public event Action<bool> ChargeMissed;
        public event Action IntroImpact;
        public event Action IntroFinished;
        public event Action<Platform, int> Launched;
        public event Action ReachedApex;

        public CatInput Input { get; private set; }
        public CatHealth Health { get; private set; }
        public Skewer Skewer { get; private set; }
        public bool ControlEnabled { get; private set; }
        public CatState State { get; private set; }
        public TridentPose Pose { get; private set; }
        public Vector2 StuckHead { get; private set; }
        public Platform Ground { get; private set; }
        public bool Charging => !double.IsNaN(chargeStart);
        public bool JumpQueued { get; private set; }
        public int ChargeLevel { get; private set; }
        public int ChargeBeats => chargeBeats;
        public bool InReleaseWindow { get; private set; }
        public float MaxHeight { get; private set; }
        public bool FacingRight { get; private set; } = true;
        public bool Eating { get; set; }
        public bool Grounded => State == CatState.Grounded;
        public bool InIntro => State == CatState.Intro;
        public bool Jumping => State == CatState.Stabbing || State == CatState.Flipping || hopping;
        public bool IsLaunching => State == CatState.Blasting || State == CatState.Launching || State == CatState.Braking;
        public int CrouchPixels { get; private set; }
        public int Lane => Lanes.Nearest(restX);
        public float RestX => restX;
        public bool Sliding => Time.time - slideStart < laneSeconds;
        public int FlipFrame { get; private set; }
        public float LaunchUnitsPerBeat => launchUnitsPerBeat;
        public float VerticalSpeed { get; private set; }
        public double BlastStartBeat => motionStart;
        public double CruiseStartBeat => motionStart + blastBeats;
        public float CruiseStartY { get; private set; }
        public Vector2 BodyCenter => (Vector2)transform.position + new Vector2(0f, BodyHeight * 0.5f);
        public Rect BodyRect => new Rect(transform.position.x - HalfWidth, transform.position.y, HalfWidth * 2f, BodyHeight);

        public double NextReleaseBeat
        {
            get
            {
                if (!Charging)
                    return double.NaN;
                double rel = Beats - chargeStart;
                int n = Math.Max(0, (int)Math.Ceiling((rel - chargeBeats - lateWindow) / Cycle));
                return chargeStart + chargeBeats + n * Cycle;
            }
        }

        public float LaunchRise(int cruise) => launchUnitsPerBeat * (cruise + BrakeBeats * 0.5f);

        private int Cycle => chargeBeats + 1;

        private float restX;
        private float slideFrom;
        private float slideStart = float.NegativeInfinity;
        private int bumpDir;
        private int pendingSwipe;
        private SfxHandle queuedRelease = new SfxHandle { Voice = -1 };
        private readonly System.Collections.Generic.List<SfxHandle> chargeVoices = new System.Collections.Generic.List<SfxHandle>();
        private float bumpStart = float.NegativeInfinity;
        private double chargeStart = double.NaN;
        private double queuedBeat;
        private double lastTickBeat = double.NegativeInfinity;
        private double lastVisualTick = double.NegativeInfinity;
        private double missUntil = double.NegativeInfinity;
        private double squashUntil = double.NegativeInfinity;
        private double landSoundBeat = double.NaN;
        private double launchSoundBeat = double.NaN;
        private double motionStart;
        private float motionY;
        private float motionVelocity;
        private float motionGravity;
        private double jumpStart;
        private float jumpDuration = 1f;
        private float jumpFromX;
        private float jumpFromY;
        private float pullY;
        private float flipFromX;
        private float flipToX;
        private Vector2 stuckLocal;
        private Platform catchPlatform;
        private Platform hopGround;
        private Platform leftBehind;
        private bool hopping;
        private bool afterLaunch;
        private bool landTickScheduled;
        private int blastBeats;
        private float blastRise;
        private float lastStreakY;
        private int cruiseBeats;
        private float windBudget;
        private double shakeUntil = double.NegativeInfinity;
        private double introStart;
        private Platform introGround;
        private float introTop;
        private bool introCaught;
        private bool tossing;
        private float tossStart;
        private float tossFloor;
        private int tossHits;
        private Vector2 tossFrom;
        private int introStage;
        private Sprite introSprite;
        private SpriteRenderer[] ghosts;
        private double[] ghostBorn;
        private int nextGhost;
        private double lastGhostBeat = double.NegativeInfinity;
        private float lineBudget;

        private void Awake()
        {
            Instance = this;
            Input = GetComponent<CatInput>();
            Health = GetComponent<CatHealth>();
            Skewer = GetComponent<Skewer>();

            var holder = new GameObject("Trail").transform;
            holder.SetParent(transform.parent, false);
            ghosts = new SpriteRenderer[GhostCount];
            ghostBorn = new double[GhostCount];
            for (int i = 0; i < GhostCount; i++)
            {
                var go = new GameObject("Ghost");
                go.transform.SetParent(holder, false);
                var ghost = go.AddComponent<SpriteRenderer>();
                if (sprite != null)
                {
                    ghost.sharedMaterial = sprite.sharedMaterial;
                    ghost.sortingOrder = sprite.sortingOrder - 2;
                }
                ghost.enabled = false;
                ghosts[i] = ghost;
                ghostBorn[i] = double.NegativeInfinity;
            }
        }

        private void OnEnable()
        {
            Input.HoldReleased += OnHoldReleased;
            Input.Pressed += OnPressed;
            Input.Swiped += OnSwiped;
        }

        private void OnDisable()
        {
            Input.HoldReleased -= OnHoldReleased;
            Input.Pressed -= OnPressed;
            Input.Swiped -= OnSwiped;
            Sfx.Wind(0f);
        }

        public void ResetTo(Vector2 position, Platform ground)
        {
            transform.position = position;
            restX = position.x;
            pendingSwipe = 0;
            slideStart = float.NegativeInfinity;
            bumpStart = float.NegativeInfinity;
            chargeStart = double.NaN;
            JumpQueued = false;
            hopping = false;
            afterLaunch = false;
            catchPlatform = null;
            hopGround = null;
            leftBehind = null;
            missUntil = double.NegativeInfinity;
            squashUntil = double.NegativeInfinity;
            landSoundBeat = double.NaN;
            launchSoundBeat = double.NaN;
            lastTickBeat = double.NegativeInfinity;
            lastVisualTick = double.NegativeInfinity;
            shakeUntil = double.NegativeInfinity;
            lastGhostBeat = double.NegativeInfinity;
            introCaught = true;
            tossing = false;
            if (looseFork != null)
                looseFork.enabled = false;
            foreach (var ghost in ghosts)
                ghost.enabled = false;
            Ground = ground;
            State = ground != null ? CatState.Grounded : CatState.Airborne;
            Pose = TridentPose.Side;
            MaxHeight = position.y;
            FacingRight = true;
            Eating = false;
            if (ground == null)
                BeginMotion(Beats, position.y, 0f, FallGravity);
        }

        public void SetControl(bool value)
        {
            ControlEnabled = value;
            Input.SetEnabled(value);
            if (!value)
            {
                if (JumpQueued)
                    Sfx.Cancel(queuedRelease);
                chargeStart = double.NaN;
                JumpQueued = false;
                pendingSwipe = 0;
                CancelChargeSounds();
            }
        }

        public void BeginIntro(double startBeat, float fromY)
        {
            introStart = startBeat;
            introGround = Ground;
            introTop = fromY;
            introCaught = false;
            introStage = 0;
            introSprite = faceplantSprite;
            State = CatState.Intro;
            Ground = null;
            FacingRight = true;
            transform.position = new Vector3(0f, fromY, 0f);
            if (looseFork != null)
                looseFork.enabled = true;
            Sfx.PlayAtBeat(SfxId.Land, startBeat + 1.0, 0.8f);
            Sfx.PlayAtBeat(SfxId.Stab, startBeat + 1.0, 0.5f);
            Sfx.PlayAtBeat(SfxId.Stab, startBeat + 2.0, 0.8f);
            Sfx.PlayAtBeat(SfxId.Stab, startBeat + 3.0, 0.9f);
            Sfx.PlayAtBeat(SfxId.Flip, startBeat + 3.0);
            Sfx.PlayAtBeat(SfxId.Stab, startBeat + 3.5, 1.2f);
            Sfx.PlayAtBeat(SfxId.Land, startBeat + 4.0);
        }

        private void UpdateIntro(ref Vector3 p, double beats)
        {
            float t = (float)(beats - introStart);
            float ground = introGround != null ? introGround.Top : 0f;
            p.x = 0f;
            if (t < 1f)
            {
                float k = Mathf.Max(0f, t);
                p.y = Mathf.Lerp(introTop, ground, k * k);
                introSprite = faceplantSprite;
                PlaceFork(new Vector2(0.6f, p.y + 1.4f), Mathf.FloorToInt(k * 4f));
                return;
            }

            p.y = ground;
            if (introStage == 0)
            {
                introStage = 1;
                Fx.Dust(new Vector3(0f, ground, 0f), 14, 1.6f);
                Fx.Burst(new Vector3(0f, ground + 0.3f, 0f), Palette.White, 10);
                IntroImpact?.Invoke();
            }

            if (t < 3f)
            {
                int crouchCount = crouchSprites.Length;
                if (t < 1.75f)
                    introSprite = faceplantSprite;
                else if (t < 2.5f && crouchCount > 0)
                    introSprite = crouchSprites[Mathf.Clamp(crouchCount - 1 - (int)((t - 1.75f) / 0.25f), 0, crouchCount - 1)];
                else
                    introSprite = idleSprite;
            }
            else if (t < 4f)
            {
                float u = t - 3f;
                p.y = ground + 4f * IntroHop * u * (1f - u);
                introSprite = airSprite;
            }

            if (!introCaught)
            {
                var a = new Vector2(0.6f, ground + 0.6f);
                var b = new Vector2(3.0f, ground + 0.5f);
                var c = new Vector2(-2.6f, ground + 0.5f);
                var d = new Vector2((Skewer.SideShaftX + 0.5f) * Unit, ground + IntroHop + Skewer.PawY * Unit);
                Vector2 fork;
                if (t < 2f)
                {
                    fork = Arc(a, b, 4.5f, t - 1f);
                }
                else if (t < 3f)
                {
                    if (introStage == 1)
                    {
                        introStage = 2;
                        Fx.Sparks(b, 8);
                    }
                    fork = Arc(b, c, 3.5f, t - 2f);
                }
                else
                {
                    if (introStage == 2)
                    {
                        introStage = 3;
                        Fx.Sparks(c, 8);
                    }
                    fork = Arc(c, d, 1f, Mathf.Clamp01((t - 3f) / 0.5f));
                }
                FacingRight = fork.x >= p.x;
                PlaceFork(fork, Mathf.FloorToInt((t - 1f) * 8f));
                if (t >= 3.5f)
                {
                    introCaught = true;
                    FacingRight = true;
                    if (looseFork != null)
                        looseFork.enabled = false;
                    Fx.Ring(d, Palette.White, 12, 5f);
                    Fx.Burst(d, Palette.Red, 8);
                }
            }

            if (t >= IntroBeats)
            {
                p.y = ground;
                restX = p.x;
                State = CatState.Grounded;
                Ground = introGround;
                squashUntil = beats + 0.2;
                Fx.Dust(p, 5, 1f);
                IntroFinished?.Invoke();
            }
        }

        public void TossFork(float floor)
        {
            if (looseFork == null)
                return;
            tossing = true;
            tossStart = Time.unscaledTime;
            tossFloor = floor;
            tossHits = 0;
            tossFrom = new Vector2(transform.position.x + (Skewer.SideShaftX + 0.5f) * Unit, transform.position.y + 1.5f);
            looseFork.enabled = true;
            PlaceFork(tossFrom, 0);
            Sfx.Play(SfxId.Flip);
        }

        private void UpdateToss()
        {
            float t = Time.unscaledTime - tossStart;
            float dir = tossFrom.x > 0.01f ? -1f : 1f;
            var from = tossFrom;
            var to = new Vector2(tossFrom.x * 0.5f, tossFloor);
            float height = TossHeight;
            float seconds = TossSeconds;
            for (int hop = 0; hop < TossBounces; hop++)
            {
                if (t < seconds)
                {
                    PlaceFork(Arc(from, to, height, t / seconds), Mathf.FloorToInt((Time.unscaledTime - tossStart) * 12f));
                    return;
                }
                t -= seconds;
                if (tossHits <= hop)
                {
                    tossHits = hop + 1;
                    Fx.Sparks(to, 8 - 2 * hop);
                    Sfx.Play(SfxId.Stab, 1f - 0.1f * hop, 0.8f - 0.2f * hop);
                }
                from = to;
                height *= 0.3f;
                seconds *= 0.6f;
                to = new Vector2(from.x + dir * height * 0.6f, tossFloor);
            }
            PlaceFork(from, 1);
        }

        private void PlaceFork(Vector2 position, int quarter)
        {
            if (looseFork == null)
                return;
            looseFork.transform.position = new Vector3(PixelPerfectRig.Snap(position.x), PixelPerfectRig.Snap(position.y), 0f);
            looseFork.transform.rotation = Quaternion.Euler(0f, 0f, -90f * Mod(quarter, 4));
        }

        private static Vector2 Arc(Vector2 from, Vector2 to, float height, float u) =>
            Vector2.Lerp(from, to, u) + Vector2.up * (4f * height * u * (1f - u));

        private static double Beats => Conductor.Instance != null ? Conductor.Instance.SongBeats : Time.timeSinceLevelLoad * 2.0;

        private static float BeatsPerSecond => Conductor.Instance != null ? Conductor.Instance.Bpm / 60f : 2f;

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;

            double beats = Beats;
            if (pendingSwipe != 0 && !Jumping && !JumpQueued && !(IsLaunching && Skewer.Locked))
            {
                int dir = pendingSwipe;
                pendingSwipe = 0;
                OnSwiped(dir);
            }
            if (JumpQueued && Grounded && ControlEnabled && beats >= queuedBeat)
                Jump(queuedBeat, 1f, true);

            Vector3 p = transform.position;
            if (Grounded && (Ground == null || !Ground.isActiveAndEnabled))
                StartFalling(beats, p.y);

            VerticalSpeed = 0f;
            switch (State)
            {
                case CatState.Grounded:
                    UpdateGrounded(ref p, dt);
                    break;
                case CatState.Stabbing:
                case CatState.Flipping:
                    UpdateClimb(ref p, beats);
                    break;
                case CatState.Intro:
                    UpdateIntro(ref p, beats);
                    break;
                case CatState.Blasting:
                case CatState.Launching:
                case CatState.Braking:
                    UpdateLaunch(ref p, dt, beats);
                    break;
                default:
                    UpdateAirborne(ref p, dt, beats);
                    break;
            }
            transform.position = p;

            UpdateCharge(beats);
            UpdatePose(beats);
            UpdateVisual(beats);
            UpdateTrail(beats, dt, p);
            if (tossing)
                UpdateToss();
            Sfx.Wind(State == CatState.Blasting ? 1f : State == CatState.Launching ? 0.75f : State == CatState.Braking ? 0.35f : 0f);
        }

        private void UpdateTrail(double beats, float dt, Vector3 p)
        {
            bool fast = State == CatState.Blasting || State == CatState.Launching;
            if (fast && sprite != null && beats - lastGhostBeat >= GhostEveryBeats)
            {
                lastGhostBeat = beats;
                var ghost = ghosts[nextGhost];
                ghostBorn[nextGhost] = beats;
                nextGhost = (nextGhost + 1) % ghosts.Length;
                ghost.sprite = sprite.sprite;
                ghost.flipX = sprite.flipX;
                ghost.transform.position = new Vector3(PixelPerfectRig.Snap(p.x), PixelPerfectRig.Snap(p.y), 0f);
                ghost.enabled = true;
            }
            for (int i = 0; i < ghosts.Length; i++)
            {
                double age = beats - ghostBorn[i];
                bool alive = age >= 0.0 && age < GhostLifeBeats;
                ghosts[i].enabled = alive;
                if (alive)
                    ghosts[i].color = new Color(1f, 1f, 1f, GhostAlpha * (1f - (float)(age / GhostLifeBeats)));
            }

            if (!fast)
            {
                lineBudget = 0f;
                windBudget = 0f;
                return;
            }
            windBudget += dt * WindPerSecond;
            while (windBudget >= 1f)
            {
                windBudget -= 1f;
                float side = UnityEngine.Random.value < 0.5f ? -1f : 1f;
                Fx.SpeedLine(new Vector2(p.x + side * UnityEngine.Random.Range(5f, 9f) * Unit, p.y + UnityEngine.Random.Range(0f, 18f) * Unit),
                    UnityEngine.Random.Range(2, 6), UnityEngine.Random.Range(18f, 26f), Palette.Gray4);
            }
            var rig = PixelPerfectRig.Instance;
            if (rig == null)
                return;
            float top = rig.Camera.transform.position.y + rig.Camera.orthographicSize;
            float half = rig.HalfWidthUnits;
            lineBudget += dt * SpeedLinesPerSecond;
            while (lineBudget >= 1f)
            {
                lineBudget -= 1f;
                Fx.SpeedLine(new Vector2(UnityEngine.Random.Range(-half, half), top + UnityEngine.Random.Range(0f, 2f)),
                    UnityEngine.Random.Range(10, 27), UnityEngine.Random.Range(55f, 75f), UnityEngine.Random.value < 0.7f ? Palette.Gray4 : Palette.Gray5);
            }
        }

        private void OnPressed()
        {
            if (ControlEnabled)
                Sfx.Play(SfxId.Touch);
        }

        private void OnSwiped(int dir)
        {
            if (!ControlEnabled || Eating || InIntro)
                return;
            if (Jumping || JumpQueued || (IsLaunching && Skewer.Locked))
            {
                pendingSwipe = dir;
                return;
            }
            float x = transform.position.x;
            if (Grounded)
            {
                if (Eating || Ground == null)
                    return;
                float target = restX + dir * Lanes.Width;
                if (Ground.CoversLane(target))
                    MoveLane(target, x, dir);
                else
                    Bump(dir);
                return;
            }
            if (IsLaunching || (State == CatState.Airborne && !hopping))
            {
                int lane = Lanes.Nearest(restX) + dir;
                if (lane < 0 || lane >= Lanes.Count)
                    Bump(dir);
                else
                    MoveLane(Lanes.X(lane), x, dir);
            }
        }

        private void MoveLane(float target, float fromX, int dir)
        {
            restX = target;
            slideFrom = fromX - target;
            slideStart = Time.time;
            bumpStart = float.NegativeInfinity;
            FacingRight = dir > 0;
            Sfx.Play(SfxId.Dash);
            if (Grounded)
                Fx.Dust(transform.position, 3, 0.8f);
        }

        private void Bump(int dir)
        {
            bumpDir = dir;
            bumpStart = Time.time;
            FacingRight = dir > 0;
            Sfx.Play(SfxId.Touch, 0.6f);
        }

        private float LaneX()
        {
            float x = restX;
            float t = (Time.time - slideStart) / laneSeconds;
            if (t < 1f)
            {
                float k = 1f - t;
                x += slideFrom * k * k * k;
            }
            float b = (Time.time - bumpStart) / BumpSeconds;
            if (b < 1f)
                x += bumpDir * BumpPixels * Unit * Mathf.Sin(b * Mathf.PI);
            return x;
        }

        private void UpdateGrounded(ref Vector3 p, float dt)
        {
            restX += Ground.Delta.x;
            p.x = LaneX();
            p.y = Ground.Top;
            MaxHeight = Mathf.Max(MaxHeight, p.y);
        }

        private void UpdateClimb(ref Vector3 p, double beats)
        {
            var platform = catchPlatform;
            if (platform == null || !platform.isActiveAndEnabled)
            {
                catchPlatform = null;
                StartFalling(beats, p.y);
                return;
            }

            float carry = platform.Delta.x;
            flipFromX += carry;
            flipToX += carry;
            StuckHead = (Vector2)platform.transform.position + stuckLocal;
            float t = (float)((beats - jumpStart) / jumpDuration);

            if (t < 0.5f)
            {
                State = CatState.Stabbing;
                float u = Mathf.Clamp01(t / 0.5f);
                float e = 1f - (1f - u) * (1f - u);
                float alignX = StuckHead.x - Skewer.SideShaftX * Unit;
                p.x = Mathf.Lerp(jumpFromX, alignX, e);
                p.y = Mathf.Lerp(jumpFromY, pullY, e);
                return;
            }

            if (State != CatState.Flipping)
                BeginFlip(platform, p);

            if (t < 1f)
            {
                float u = (t - 0.5f) / 0.5f;
                p.x = Mathf.Lerp(flipFromX, flipToX, u);
                p.y = pullY + (platform.Top - pullY) * u + 4f * FlipArc * u * (1f - u);
                FlipFrame = flipSprites.Length > 0 ? Mathf.Clamp((int)(u * flipSprites.Length), 0, flipSprites.Length - 1) : 0;
                if (!landTickScheduled && Input.Holding && ControlEnabled && platform.Kind != PlatformKind.Launch)
                {
                    double landBeat = jumpStart + jumpDuration;
                    chargeVoices.Add(Sfx.PlayAtBeat(SfxId.Charge1, landBeat));
                    lastTickBeat = Math.Round(landBeat);
                    landTickScheduled = true;
                }
                return;
            }

            p.x = flipToX;
            p.y = platform.Top;
            Land(platform, beats, p);
        }

        private void BeginFlip(Platform platform, Vector3 p)
        {
            State = CatState.Flipping;
            flipFromX = p.x;
            flipToX = platform.SnapToLane(p.x);
            Fx.Burst(StuckHead, Palette.White, 5);
        }

        private void UpdateAirborne(ref Vector3 p, float dt, double beats)
        {
            if (hopping && hopGround != null && hopGround.isActiveAndEnabled)
                restX += hopGround.Delta.x;
            p.x = LaneX();

            float t = (float)(beats - motionStart);
            float velocity = motionVelocity - motionGravity * t;
            float newY;
            if (velocity < -MaxFallUnitsPerBeat)
            {
                BeginMotion(beats, p.y, -MaxFallUnitsPerBeat, 0f);
                newY = p.y;
                velocity = -MaxFallUnitsPerBeat;
            }
            else
            {
                newY = motionY + motionVelocity * t - 0.5f * motionGravity * t * t;
            }

            Platform landing = null;
            if (velocity <= 0f)
            {
                foreach (var platform in Platform.All)
                {
                    if (platform == leftBehind)
                        continue;
                    if (p.y >= platform.Top - 0.05f && newY <= platform.Top && platform.Contains(p.x, HalfWidth)
                        && (landing == null || platform.Top > landing.Top))
                        landing = platform;
                }
            }

            if (landing != null)
            {
                p.y = landing.Top;
                Land(landing, beats, p);
            }
            else
            {
                p.y = newY;
            }
        }

        private void UpdateLaunch(ref Vector3 p, float dt, double beats)
        {
            p.x = LaneX();
            float v = launchUnitsPerBeat;
            float bps = BeatsPerSecond;
            float t = Mathf.Max(0f, (float)(beats - motionStart));
            if (t < blastBeats)
            {
                State = CatState.Blasting;
                float k = 1f - t / blastBeats;
                float extra = blastRise - v * blastBeats;
                p.y = motionY + v * t + extra * (1f - k * k * k);
                VerticalSpeed = (v + 3f * extra / blastBeats * k * k) * bps;
                Fx.Streak(new Vector3(p.x, lastStreakY, 0f), p);
                lastStreakY = p.y;
                return;
            }

            float tc = t - blastBeats;
            if (tc < cruiseBeats)
            {
                State = CatState.Launching;
                p.y = CruiseStartY + v * tc;
                VerticalSpeed = v * bps;
                return;
            }

            State = CatState.Braking;
            float cruiseTop = CruiseStartY + v * cruiseBeats;
            float tb = Mathf.Min(tc - cruiseBeats, BrakeBeats);
            float decel = v / BrakeBeats;
            p.y = cruiseTop + v * tb - 0.5f * decel * tb * tb;
            VerticalSpeed = (v - decel * tb) * bps;
            if (tc - cruiseBeats < BrakeBeats)
                return;

            float apex = cruiseTop + v * BrakeBeats * 0.5f;
            p.y = apex;
            State = CatState.Airborne;
            afterLaunch = true;
            double apexBeat = CruiseStartBeat + cruiseBeats + BrakeBeats;
            BeginMotion(apexBeat, apex, 0f, 2f * TableDrop / (TableFallBeats * TableFallBeats));
            landSoundBeat = apexBeat + TableFallBeats;
            Sfx.PlayAtBeat(SfxId.Land, landSoundBeat);
            ReachedApex?.Invoke();
        }

        private void UpdateCharge(double beats)
        {
            bool canCharge = Grounded && ControlEnabled && !Eating && !JumpQueued && Input.Holding;
            if (!canCharge)
            {
                if (!JumpQueued)
                {
                    chargeStart = double.NaN;
                    if (!Input.Holding && chargeVoices.Count > 0)
                        CancelChargeSounds();
                }
                ChargeLevel = JumpQueued ? chargeBeats : 0;
                InReleaseWindow = false;
                return;
            }

            if (!Charging)
            {
                double frac = beats - Math.Floor(beats);
                chargeStart = frac < touchGrace ? Math.Floor(beats) : Math.Floor(beats) + 1.0;
            }

            double rel = beats - chargeStart;
            double off = ReleaseOffset(rel);
            InReleaseWindow = rel >= 0 && off >= -earlyWindow && off <= lateWindow;
            if (rel < 0)
            {
                ChargeLevel = 0;
            }
            else
            {
                double cycle = rel - Math.Floor(rel / Cycle) * Cycle;
                ChargeLevel = InReleaseWindow ? chargeBeats : cycle < chargeBeats ? (int)Math.Floor(cycle) + 1 : 0;
            }

            double from = Math.Max(chargeStart, Math.Floor(beats));
            for (double b = from; b <= beats + TickLead; b += 1.0)
            {
                if (b <= lastTickBeat)
                    continue;
                lastTickBeat = b;
                int phase = Phase(b);
                if (b >= beats - 0.25 && phase < chargeBeats)
                    chargeVoices.Add(Sfx.PlayAtBeat(ChargeSound(phase), b));
            }

            double tick = Math.Floor(beats);
            if (tick >= chargeStart && tick > lastVisualTick && beats - tick < 0.25 && Phase(tick) < chargeBeats)
            {
                lastVisualTick = tick;
                Fx.Dust(transform.position, 2 + Phase(tick), 0.7f);
            }
        }

        private int Phase(double beat) => Mod((int)Math.Round(beat - chargeStart), Cycle);

        private static SfxId ChargeSound(int phase) => phase == 0 ? SfxId.Charge1 : phase == 1 ? SfxId.Charge2 : SfxId.Charge3;

        private double ReleaseOffset(double rel)
        {
            int n = Math.Max(0, (int)Math.Round((rel - chargeBeats) / Cycle));
            return rel - (chargeBeats + n * Cycle);
        }

        private void CancelChargeSounds()
        {
            foreach (var voice in chargeVoices)
                Sfx.Cancel(voice);
            chargeVoices.Clear();
        }

        private void OnHoldReleased()
        {
            if (!ControlEnabled || !Grounded || JumpQueued || !Charging || Eating)
                return;

            double beats = Beats;
            double rel = beats - chargeStart;
            double off = ReleaseOffset(rel);
            chargeStart = double.NaN;
            CancelChargeSounds();
            if (rel < 0)
                return;
            if (off < -earlyWindow || off > lateWindow)
            {
                missUntil = beats + 0.5;
                shakeUntil = beats + 0.3;
                Sfx.Play(SfxId.Miss);
                ChargeMissed?.Invoke(off < 0);
                return;
            }

            double target = beats - off;
            if (off < 0)
            {
                JumpQueued = true;
                queuedBeat = target;
                queuedRelease = Sfx.PlayAtBeat(SfxId.Release, target);
            }
            else
            {
                Jump(beats, 1f - (float)off, false);
            }
        }

        private void Jump(double start, float duration, bool stabScheduled)
        {
            Vector3 p = transform.position;
            p.x = restX;
            transform.position = p;
            slideStart = float.NegativeInfinity;
            bumpStart = float.NegativeInfinity;
            var from = Ground;
            jumpStart = start;
            jumpDuration = Mathf.Max(0.3f, duration);
            jumpFromX = p.x;
            jumpFromY = p.y;
            Ground = null;
            chargeStart = double.NaN;
            JumpQueued = false;
            landTickScheduled = false;
            if (!stabScheduled)
                Sfx.Play(SfxId.Release);

            double landBeat = start + jumpDuration;
            var target = FindCatch(p);
            if (target != null)
            {
                catchPlatform = target;
                hopping = false;
                float minHead = target.Left + 3f * Unit;
                float maxHead = target.Right - 4f * Unit;
                float headX = p.x + Skewer.SideShaftX * Unit;
                headX = minHead < maxHead ? Mathf.Clamp(headX, minHead, maxHead) : target.transform.position.x;
                headX = Mathf.Round(headX * PixelPerfectRig.PixelsPerUnit) / PixelPerfectRig.PixelsPerUnit;
                float underside = target.Top - Platform.Thickness;
                float headY = underside - (Skewer.HeadPixels - BitePixels) * Unit;
                StuckHead = new Vector2(headX, headY);
                stuckLocal = StuckHead - (Vector2)target.transform.position;
                pullY = headY - (Skewer.PawY + 2) * Unit;
                State = CatState.Stabbing;
                Sfx.PlayAtBeat(SfxId.Flip, start + jumpDuration * 0.5);
                if (target.Kind == PlatformKind.Launch)
                {
                    Sfx.PlayAtBeat(SfxId.Launch, landBeat);
                    launchSoundBeat = landBeat;
                }
                Fx.Sparks(new Vector3(headX, underside, 0f), 10);
                Stabbed?.Invoke(true);
            }
            else
            {
                catchPlatform = null;
                hopping = true;
                bool fromMoving = from != null && from.Kind == PlatformKind.Moving;
                hopGround = fromMoving ? null : from;
                leftBehind = fromMoving ? from : null;
                State = CatState.Airborne;
                BeginMotion(start, p.y, 4f * HopHeight / jumpDuration, 8f * HopHeight / (jumpDuration * jumpDuration));
                Sfx.PlayAtBeat(SfxId.Whiff, start + jumpDuration * 0.25);
                Fx.Dust(p + new Vector3((Skewer.SideShaftX + 0.5f) * Unit, (Skewer.SideTipPixels + ThrustPixels) * Unit, 0f), 6, 0.6f);
                Stabbed?.Invoke(false);
            }
            if (leftBehind == null)
            {
                Sfx.PlayAtBeat(SfxId.Land, landBeat);
                landSoundBeat = landBeat;
            }
            else
            {
                landSoundBeat = double.NaN;
            }
            Jumped?.Invoke();
        }

        private Platform FindCatch(Vector3 p)
        {
            float shaft = p.x + (Skewer.SideShaftX + 0.5f) * Unit;
            float left = Mathf.Min(p.x - HalfWidth, shaft - 3.5f * Unit);
            float right = Mathf.Max(p.x + HalfWidth, shaft + 3.5f * Unit);
            float reach = p.y + (Skewer.SideTipPixels + ThrustPixels) * Unit;
            Platform best = null;
            float bestOverlap = 0f;
            foreach (var platform in Platform.All)
            {
                float rise = platform.Top - p.y;
                if (rise < 0.5f || rise > RowHeight + 0.25f)
                    continue;
                if (platform.Top - Platform.Thickness > reach + 0.01f)
                    continue;
                float overlap = Mathf.Min(right, platform.Right) - Mathf.Max(left, platform.Left);
                if (overlap > bestOverlap)
                {
                    best = platform;
                    bestOverlap = overlap;
                }
            }
            return best;
        }

        private void Land(Platform platform, double beats, Vector3 p)
        {
            if (hopping)
                shakeUntil = beats + 0.3;
            State = CatState.Grounded;
            Ground = platform;
            float snapped = platform.SnapToLane(p.x);
            if (Mathf.Abs(snapped - p.x) > 0.001f)
            {
                slideFrom = p.x - snapped;
                slideStart = Time.time;
            }
            restX = snapped;
            JumpQueued = false;
            hopping = false;
            catchPlatform = null;
            afterLaunch = false;
            hopGround = null;
            leftBehind = null;
            if (double.IsNaN(landSoundBeat) || Math.Abs(beats - landSoundBeat) > 0.3)
                Sfx.Play(SfxId.Land);
            landSoundBeat = double.NaN;
            squashUntil = beats + 0.2;
            Fx.Dust(p, 5, 1f);
            MaxHeight = Mathf.Max(MaxHeight, p.y);
            if (Input.Holding && ControlEnabled && platform.Kind != PlatformKind.Launch)
                chargeStart = Math.Round(beats);
            Landed?.Invoke(platform);
            if (platform.Kind == PlatformKind.Launch && ControlEnabled)
                StartLaunch(platform, beats, p);
        }

        private void StartLaunch(Platform pad, double beats, Vector3 p)
        {
            int land = (int)Math.Round(beats);
            int cruiseStart = land + MinBlastBeats;
            if (cruiseStart % 2 != 0)
                cruiseStart++;
            blastBeats = cruiseStart - land;
            float v = launchUnitsPerBeat;
            blastRise = PixelPerfectRig.Snap(v * blastBeats + (BlastUnitsPerBeat - v) * blastBeats / 3f);
            CruiseStartY = pad.Top + blastRise;
            cruiseBeats = Mathf.Max(1, pad.Launch.CruiseBeats);
            pad.Launch.Beat = cruiseStart;
            pad.Launch.StartY = CruiseStartY;
            pad.Arm(land);

            BeginMotion(land, pad.Top, v, 0f);
            State = CatState.Blasting;
            Ground = null;
            chargeStart = double.NaN;
            afterLaunch = false;
            restX = p.x;
            lastStreakY = pad.Top;
            if (double.IsNaN(launchSoundBeat) || Math.Abs(beats - launchSoundBeat) > 0.3)
                Sfx.Play(SfxId.Launch);
            launchSoundBeat = double.NaN;
            Fx.Burst(p, Palette.Red, 20);
            Fx.Ring(p, Palette.White, 20, 9f);
            Launched?.Invoke(pad, cruiseStart);
        }

        private void StartFalling(double beats, float y)
        {
            State = CatState.Airborne;
            Ground = null;
            hopping = false;
            catchPlatform = null;
            BeginMotion(beats, y, 0f, FallGravity);
        }

        private void BeginMotion(double start, float y, float velocity, float gravity)
        {
            motionStart = start;
            motionY = y;
            motionVelocity = velocity;
            motionGravity = gravity;
        }

        private void UpdatePose(double beats)
        {
            if (tossing)
                Pose = TridentPose.Hidden;
            else if (State == CatState.Intro)
                Pose = introCaught ? TridentPose.Side : TridentPose.Hidden;
            else if (State == CatState.Stabbing)
                Pose = TridentPose.Stuck;
            else if (State == CatState.Flipping)
                Pose = TridentPose.Hidden;
            else if (IsLaunching)
                Pose = TridentPose.Overhead;
            else if (Eating)
                Pose = TridentPose.Feeding;
            else if (afterLaunch)
                Pose = TridentPose.Overhead;
            else if (hopping && beats - jumpStart < jumpDuration * 0.5)
                Pose = TridentPose.Thrust;
            else
                Pose = TridentPose.Side;
        }

        private void UpdateVisual(double beats)
        {
            if (Pose == TridentPose.Feeding)
                FacingRight = transform.position.x <= 0f;

            int crouch = 0;
            Sprite shown;
            if (State == CatState.Intro)
                shown = introSprite;
            else if (State == CatState.Flipping && flipSprites.Length > 0)
                shown = flipSprites[FlipFrame];
            else if (Pose == TridentPose.Feeding)
                shown = FeedSprite(Skewer.Mouth);
            else if (Pose == TridentPose.Overhead)
                shown = launchSprite;
            else if (!Grounded)
                shown = airSprite;
            else
            {
                if (JumpQueued)
                    crouch = chargeBeats;
                else if (Charging && ChargeLevel > 0)
                    crouch = ChargeLevel;
                else if (beats < squashUntil)
                    crouch = 1;
                else if (!Sliding)
                    crouch = Conductor.Bounce(1);

                if (crouch > 0 && crouchSprites.Length > 0)
                    shown = crouchSprites[Mathf.Min(crouch, crouchSprites.Length) - 1];
                else
                    shown = Sliding ? walkSprite : idleSprite;
            }
            CrouchPixels = crouch;

            if (sprite != null)
            {
                sprite.sprite = shown;
                sprite.flipX = !FacingRight;
                int shake = beats < shakeUntil ? (Math.Floor(beats * 16.0) % 2 == 0 ? 1 : -1) : 0;
                sprite.transform.localPosition = new Vector3(shake * Unit, 0f, 0f);
            }

            if (chargeBar == null)
                return;
            bool missed = beats < missUntil;
            bool held = Input.Holding && Time.realtimeSinceStartup - Input.PressedAt >= ChargeBarDelay;
            bool show = Grounded && ControlEnabled && !Eating && (held || JumpQueued || missed);
            chargeBar.SetActive(show);
            if (!show)
                return;
            bool ready = InReleaseWindow || JumpQueued;
            bool blink = Math.Floor(beats * 8.0) % 2 == 0;
            bool fresh = beats - lastVisualTick < 0.12;
            for (int i = 0; i < chargeSegments.Length; i++)
            {
                var segment = chargeSegments[i];
                segment.enabled = !missed && i < ChargeLevel;
                bool white = ready ? blink : fresh && i == ChargeLevel - 1;
                segment.color = white ? Palette.White : Palette.Red;
            }
            chargeFrame.color = missed || (ready && blink) ? Palette.Red : Palette.White;
        }

        private Sprite FeedSprite(MouthState mouth)
        {
            switch (mouth)
            {
                case MouthState.Open: return eatOpenSprite;
                case MouthState.Full: return eatFullSprite;
                case MouthState.Chew: return eatChewSprite;
                default: return holdSprite;
            }
        }

        private static int Mod(int a, int n) => ((a % n) + n) % n;
    }
}
