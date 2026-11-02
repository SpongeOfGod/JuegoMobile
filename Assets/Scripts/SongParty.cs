using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Gluttony
{
    [DefaultExecutionOrder(90)]
    public class SongParty : MonoBehaviour
    {
        private const int Ppu = PixelPerfectRig.PixelsPerUnit;
        private const int PartyStart = 176;
        private const int PartyMadnessStart = 208;
        private const int PartyBreak = 272;
        private const int PartyReturn = 304;
        private const int PartyEnd = 384;
        private const int PetalStart = 48;
        private const int PartyStageBeats = 16;
        private const int EqBars = 8;
        private const int SpectrumSize = 64;
        private const int ConfettiCount = 30;
        private const int MarqueeCount = 12;
        private const int SparkCount = 14;
        private const int TileColumns = 5;
        private const int TileRows = 9;
        private const int CoinCount = 24;
        private const int PetalCount = 20;
        private const float EqFall = 3f;
        private const int ShopFirstMain = 32;
        private const int ShopBreak = 96;
        private const int ShopSecondMain = 128;
        private const int ShopOutro = 256;

        private static readonly Color Gold = new Color32(0xff, 0xcf, 0x3a, 0xff);
        private static readonly Color[] StudioTints =
        {
            new Color(0.55f, 1f, 0.95f), new Color(1f, 0.6f, 0.85f), new Color(0.6f, 0.7f, 1f), new Color(1f, 0.85f, 0.5f),
        };
        private static readonly Color[] TestBars =
        {
            new Color(1f, 1f, 1f), new Color(1f, 1f, 0f), new Color(0f, 1f, 1f), new Color(0f, 1f, 0f),
            new Color(1f, 0f, 1f), new Color(1f, 0f, 0f), new Color(0f, 0f, 1f),
        };
        private static readonly int GridId = Shader.PropertyToID("_Grid");
        private static readonly int BeatId = Shader.PropertyToID("_Beat");
        private static readonly int WaveId = Shader.PropertyToID("_Wave");
        private static readonly int SplitId = Shader.PropertyToID("_Split");
        private static readonly int RainbowId = Shader.PropertyToID("_Rainbow");
        private static readonly int ScanId = Shader.PropertyToID("_Scan");
        private static readonly int TrackId = Shader.PropertyToID("_Track");
        private static readonly int TrackAmountId = Shader.PropertyToID("_TrackAmount");
        private static readonly int StaticId = Shader.PropertyToID("_Static");
        private static readonly int SepiaId = Shader.PropertyToID("_Sepia");
        private static readonly int VignetteId = Shader.PropertyToID("_Vignette");

        [SerializeField] private CameraRig cameraRig;
        [SerializeField] private Backdrop backdrop;
        [SerializeField] private MusicPlayer music;
        [SerializeField] private Material screenMaterial;
        [SerializeField] private Sprite[] coinFrames;
        [SerializeField] private Sprite[] productSprites;
        [SerializeField] private Sprite[] petalSprites;

        private SpriteRenderer[] sprites;
        private Transform[] spriteTransforms;
        private Sprite pixel;
        private readonly float[] spectrum = new float[SpectrumSize];
        private readonly float[] eq = new float[EqBars];
        private SongEffects effects;
        private float clipBeats;
        private int used;
        private int shownCount;
        private int lastKick = -1;
        private float beat;
        private int halfWidth;
        private int halfHeight;
        private Vector2 center;
        private Material screen;
        private ScreenPass pass;
        private bool screenOn;

        private class ScreenPass : ScriptableRenderPass
        {
            private readonly Material material;
            private RTHandle copy;

            public ScreenPass(Material material)
            {
                this.material = material;
                renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
                ConfigureInput(ScriptableRenderPassInput.Color);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                var descriptor = renderingData.cameraData.cameraTargetDescriptor;
                descriptor.depthBufferBits = 0;
                descriptor.msaaSamples = 1;
                RenderingUtils.ReAllocateIfNeeded(ref copy, descriptor, FilterMode.Point, TextureWrapMode.Clamp, name: "_PartyCopy");
                var target = renderingData.cameraData.renderer.cameraColorTargetHandle;
                var cmd = CommandBufferPool.Get("PartyScreen");
                Blitter.BlitCameraTexture(cmd, target, copy);
                Blitter.BlitCameraTexture(cmd, copy, target, material, 0);
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            public void Release()
            {
                copy?.Release();
            }
        }

        private void Awake()
        {
            sprites = GetComponentsInChildren<SpriteRenderer>(true);
            spriteTransforms = new Transform[sprites.Length];
            for (int i = 0; i < sprites.Length; i++)
                spriteTransforms[i] = sprites[i].transform;
            pixel = sprites[0].sprite;
            screen = Instantiate(screenMaterial);
            pass = new ScreenPass(screen);
        }

        private void OnEnable() => RenderPipelineManager.beginCameraRendering += OnBeginCamera;

        private void OnDisable() => RenderPipelineManager.beginCameraRendering -= OnBeginCamera;

        private void OnDestroy()
        {
            pass.Release();
            Destroy(screen);
        }

        private void OnBeginCamera(ScriptableRenderContext context, Camera camera)
        {
            if (screenOn && camera == cameraRig.Camera)
                camera.GetUniversalAdditionalCameraData().scriptableRenderer.EnqueuePass(pass);
        }

        public void SetSong(Song song)
        {
            effects = song.clip != null ? song.effects : SongEffects.None;
            clipBeats = song.clip != null ? Mathf.Round(song.clip.length * song.bpm / 60f) : 0f;
            screenOn = false;
            if (effects != SongEffects.None)
                cameraRig.Camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        }

        private void LateUpdate()
        {
            var conductor = Conductor.Instance;
            var rig = PixelPerfectRig.Instance;
            if (effects == SongEffects.None || conductor == null || rig == null)
                return;
            if (!conductor.IsRunning || conductor.ClipBeats < 0.0)
            {
                screenOn = false;
                backdrop.SetPartyTint(Color.white, Color.white);
                for (int i = 0; i < shownCount; i++)
                    sprites[i].enabled = false;
                shownCount = 0;
                return;
            }

            center = cameraRig.transform.position;
            halfWidth = Mathf.CeilToInt(rig.HalfWidthUnits * Ppu);
            halfHeight = Mathf.CeilToInt(cameraRig.HalfHeight * Ppu);
            used = 0;
            screen.SetVector(GridId, new Vector4(rig.Width, rig.Height, 0f, 0f));
            screenOn = true;

            if (effects == SongEffects.Party)
                Party(Mathf.Repeat((float)conductor.ClipBeats, clipBeats));
            else
                ShoppingChannel(Mathf.Repeat((float)conductor.ClipBeats, clipBeats));

            for (int i = used; i < shownCount; i++)
                sprites[i].enabled = false;
            shownCount = used;
        }

        private void Party(float clipBeat)
        {
            beat = clipBeat;
            bool returned = beat >= PartyReturn && beat < PartyEnd;
            bool party = returned || (beat >= PartyStart && beat < PartyBreak);
            float madness = !party ? 0f : returned ? 1f : Mathf.Clamp01((beat - PartyMadnessStart) / (PartyBreak - PartyMadnessStart));
            screen.SetFloat(BeatId, beat);
            screen.SetFloat(WaveId, madness * 1.5f);
            screen.SetFloat(SplitId, madness);
            screen.SetFloat(RainbowId, madness * 0.3f);
            screen.SetFloat(ScanId, 0f);
            screen.SetFloat(TrackAmountId, 0f);
            screen.SetFloat(StaticId, 0f);
            screen.SetFloat(SepiaId, party ? 0f : 1f);
            screen.SetFloat(VignetteId, party ? 0f : 1f);
            if (!party)
            {
                backdrop.SetPartyTint(Color.white, Color.white);
                if (beat >= PetalStart)
                    PetalRain();
                return;
            }
            int stage = returned ? 5 : ((int)beat - PartyStart) / PartyStageBeats;
            PaintBackdrop();
            Marquee();
            Equalizer();
            if (stage >= 1) Confetti();
            if (stage >= 2) Spotlights();
            if (stage >= 3) Fireworks();
            if (stage >= 4) DanceFloor();
            if (stage >= 5) Kick();
        }

        private void ShoppingChannel(float clipBeat)
        {
            beat = clipBeat;
            bool inBreak = beat >= ShopBreak && beat < ShopSecondMain;
            bool main = (beat >= ShopFirstMain && beat < ShopBreak) || (beat >= ShopSecondMain && beat < ShopOutro);
            float change = Mathf.Min(SinceSwitch(ShopFirstMain), SinceSwitch(ShopBreak), SinceSwitch(ShopSecondMain), SinceSwitch(ShopOutro), SinceSwitch(0f));
            float glitch = Mathf.Clamp01(1f - change);

            screen.SetFloat(BeatId, beat);
            screen.SetFloat(WaveId, 0f);
            screen.SetFloat(SplitId, inBreak ? 2f : 1f);
            screen.SetFloat(RainbowId, 0f);
            screen.SetFloat(ScanId, 0.14f);
            screen.SetFloat(TrackId, 1f - Mathf.Repeat(beat / 32f, 1f));
            screen.SetFloat(TrackAmountId, 4f + glitch * 30f);
            screen.SetFloat(StaticId, glitch * glitch * 0.45f);

            int program = Mathf.FloorToInt(beat / 16f) % StudioTints.Length;
            Color studio = inBreak ? new Color(0.55f, 0.55f, 0.6f) : StudioTints[program];
            backdrop.SetPartyTint(studio, inBreak ? Color.white : Color.Lerp(studio, Gold, 0.5f));

            if (inBreak)
            {
                ColorBars();
                return;
            }
            if (main || beat >= ShopOutro)
                Conveyor();
            if (beat >= ShopSecondMain && beat < ShopOutro)
                CoinRain();
            if (main)
                Twinkles();
        }

        private float SinceSwitch(float start) => beat >= start ? beat - start : float.MaxValue;

        private void ColorBars()
        {
            int width = halfWidth * 2 / TestBars.Length + 1;
            for (int i = 0; i < TestBars.Length; i++)
            {
                Color color = TestBars[i];
                color.a = 0.3f;
                Put(-halfWidth + i * width, -halfHeight, width, halfHeight * 2, color);
            }
        }

        private void Conveyor()
        {
            const int spacing = 34;
            int beatIndex = Mathf.FloorToInt(beat);
            float eased = beatIndex + Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((beat - beatIndex) * 3f));
            int slide = Mathf.RoundToInt(eased * spacing / 2f);
            int y = Mathf.RoundToInt(halfHeight * 0.3f);
            Put(-halfWidth, y - 5, halfWidth * 2, 4, Palette.Gray3);
            for (int x = -halfWidth - (slide % 8); x < halfWidth; x += 8)
                Put(x, y - 4, 2, 2, Palette.Gray5);
            int count = (halfWidth * 2 + spacing * 2) / spacing + 1;
            int first = slide / spacing;
            int offset = slide % spacing;
            for (int i = 0; i < count; i++)
                Put(productSprites[(first + count - i) % productSprites.Length], halfWidth - i * spacing + offset - 14, y);
        }

        private void CoinRain()
        {
            int fall = halfHeight * 2 + 8;
            for (int i = 0; i < CoinCount; i++)
            {
                int y = halfHeight - Mathf.FloorToInt(Mathf.Repeat(beat * (14f + Noise(i * 3) * 10f) + Noise(i) * fall, fall));
                int x = Mathf.RoundToInt((Noise(i * 7) * 2f - 1f) * halfWidth);
                Put(coinFrames[(Mathf.FloorToInt(beat * 2f) + i) % coinFrames.Length], x - 3, y);
            }
        }

        private void PetalRain()
        {
            int fall = halfHeight * 2 + 8;
            for (int i = 0; i < PetalCount; i++)
            {
                int y = halfHeight - Mathf.FloorToInt(Mathf.Repeat(beat * (5f + Noise(i * 3) * 4f) + Noise(i) * fall, fall));
                int sway = Mathf.RoundToInt(Mathf.Sin(beat * Mathf.PI / 4f + i) * 6f);
                int x = Mathf.RoundToInt((Noise(i * 7) * 2f - 1f) * halfWidth) + sway;
                Put(petalSprites[(Mathf.FloorToInt(beat / 2f) + i) % petalSprites.Length], x, y);
            }
        }

        private void Twinkles()
        {
            int offbeat = Mathf.FloorToInt(beat);
            float age = beat - offbeat;
            if (offbeat % 2 == 0 || age > 0.5f)
                return;
            for (int i = 0; i < 4; i++)
            {
                int x = Mathf.RoundToInt((Noise(offbeat * 13 + i) * 2f - 1f) * halfWidth);
                int y = Mathf.RoundToInt((Noise(offbeat * 17 + i) * 2f - 1f) * halfHeight);
                int arm = age < 0.25f ? 3 : 1;
                Put(x - arm, y, arm * 2 + 1, 1, Palette.White);
                Put(x, y - arm, 1, arm * 2 + 1, Palette.White);
            }
        }

        private void PaintBackdrop()
        {
            float bar = beat / 4f;
            float eased = Mathf.Floor(bar) + Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((bar - Mathf.Floor(bar)) * 4f));
            backdrop.SetPartyTint(Hue(eased / 8f, 0.55f, 1f), Hue(eased / 8f + 0.5f, 0.6f, 1f));
        }

        private void Marquee()
        {
            int edge = (int)(Lanes.PlayHalfWidth * Ppu) - 3;
            int step = halfHeight * 2 / MarqueeCount;
            int phase = Mathf.FloorToInt(beat * 2f);
            for (int i = 0; i < MarqueeCount; i++)
            {
                if ((i + phase) % 3 != 0)
                    continue;
                int y = -halfHeight + i * step;
                Color color = Hue(i / (float)MarqueeCount + beat / 16f, 0.8f, 1f);
                Put(-edge, y, 3, 3, color);
                Put(edge - 3, y, 3, 3, color);
            }
        }

        private void Equalizer()
        {
            if (Time.deltaTime > 0f)
            {
                music.GetSpectrum(spectrum);
                int bin = 0;
                for (int i = 0; i < EqBars; i++)
                {
                    int next = Mathf.Min(SpectrumSize, bin + 1 + i * 2);
                    float sum = 0f;
                    for (int j = bin; j < next; j++)
                        sum += spectrum[j];
                    bin = next;
                    float level = Mathf.Clamp01(Mathf.Sqrt(sum) * 1.6f);
                    eq[i] = Mathf.Max(level, eq[i] - EqFall * Time.deltaTime);
                }
            }
            int width = (int)(Lanes.PlayHalfWidth * Ppu) * 2 / EqBars;
            int left = -width * EqBars / 2;
            for (int i = 0; i < EqBars; i++)
            {
                int height = Mathf.RoundToInt(eq[i] * halfHeight * 0.7f);
                Color color = Hue(i / (float)EqBars + beat / 32f, 0.75f, 0.45f);
                Put(left + i * width + 1, -halfHeight, width - 2, height, color);
                Put(left + i * width + 1, -halfHeight + height + 2, width - 2, 2, Hue(i / (float)EqBars + beat / 32f, 0.4f, 1f));
            }
        }

        private void Confetti()
        {
            int fall = halfHeight * 2 + 8;
            for (int i = 0; i < ConfettiCount; i++)
            {
                float speed = 10f + Noise(i * 3) * 14f;
                int y = halfHeight - Mathf.FloorToInt(Mathf.Repeat(beat * speed + Noise(i) * fall, fall));
                int sway = Mathf.RoundToInt(Mathf.Sin(beat * Mathf.PI + i) * 3f);
                int x = Mathf.RoundToInt((Noise(i * 7) * 2f - 1f) * halfWidth) + sway;
                Put(x, y, 2, Mathf.FloorToInt(beat * 2f + i) % 2 == 0 ? 2 : 1, Hue(Noise(i * 11) + beat / 24f, 0.8f, 1f));
            }
        }

        private void Spotlights()
        {
            for (int i = 0; i < 3; i++)
            {
                float swing = Mathf.Sin((beat / 8f + i / 3f) * Mathf.PI * 2f);
                Color color = Hue(i / 3f + beat / 32f, 0.8f, 1f);
                color.a = 0.16f;
                Put(Mathf.RoundToInt(swing * halfWidth) - 8, -halfHeight, 16, halfHeight * 2, color);
            }
        }

        private void Fireworks()
        {
            int bar = Mathf.FloorToInt(beat / 2f);
            float age = beat / 2f - bar;
            for (int side = 0; side < 2; side++)
            {
                int seed = bar * 2 + side;
                float cx = (Noise(seed) * 0.8f + 0.1f) * halfWidth * (side == 0 ? -1f : 1f);
                float cy = (Noise(seed + 50) * 0.8f - 0.1f) * halfHeight;
                for (int i = 0; i < SparkCount; i++)
                {
                    float angle = i * Mathf.PI * 2f / SparkCount;
                    float reach = age * 30f;
                    Color color = Hue(Noise(seed + 90) + i * 0.02f, 0.75f, 1f);
                    color.a = 1f - age;
                    Put(Mathf.RoundToInt(cx + Mathf.Cos(angle) * reach), Mathf.RoundToInt(cy + Mathf.Sin(angle) * reach - age * age * 16f), 2, 2, color);
                }
            }
        }

        private void DanceFloor()
        {
            int width = halfWidth * 2 / TileColumns + 1;
            int height = halfHeight * 2 / TileRows + 1;
            int flip = Mathf.FloorToInt(beat);
            for (int row = 0; row < TileRows; row++)
            {
                for (int column = 0; column < TileColumns; column++)
                {
                    if ((row + column + flip) % 2 != 0)
                        continue;
                    Color color = Hue((row + column) * 0.07f + beat / 16f, 0.8f, 1f);
                    color.a = 0.1f;
                    Put(-halfWidth + column * width, -halfHeight + row * height, width, height, color);
                }
            }
        }

        private void Kick()
        {
            int current = Mathf.FloorToInt(beat);
            if (current == lastKick)
                return;
            lastKick = current;
            if (current % 2 == 1)
                cameraRig.Shake(1, 0.05f);
        }

        private void Put(int x, int y, int width, int height, Color color)
        {
            if (width > 0 && height > 0)
                Place(pixel, x, y, new Vector3(width, height, 1f), color);
        }

        private void Put(Sprite sprite, int x, int y) => Place(sprite, x, y, Vector3.one, Color.white);

        private void Place(Sprite sprite, int x, int y, Vector3 scale, Color color)
        {
            if (used >= sprites.Length)
                return;
            int i = used++;
            spriteTransforms[i].position = new Vector3(center.x + x / (float)Ppu, center.y + y / (float)Ppu, 7f);
            spriteTransforms[i].localScale = scale;
            sprites[i].sprite = sprite;
            sprites[i].color = color;
            sprites[i].enabled = true;
        }

        private static Color Hue(float hue, float saturation, float value) => Color.HSVToRGB(Mathf.Repeat(hue, 1f), saturation, value);

        private static float Noise(int seed)
        {
            float n = Mathf.Sin(seed * 12.9898f + 4.1414f) * 43758.547f;
            return n - Mathf.Floor(n);
        }
    }
}
