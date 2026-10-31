using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Gluttony
{
    public enum GameState
    {
        Playing,
        Paused,
        GameOver,
    }

    public class GameManager : MonoBehaviour
    {
        private const float FallMargin = 0.6f;
        private const float IntroLeadBeats = 0.6f;
        private const float MovingNear = 0.5f;
        private const float MovingFar = 8f;
        private const int MenuSceneIndex = 0;
        private const string TutorialSongId = "TutorialSong";
        private const string MainSongId = "ChasingStars";
        private const string FinalSongId = "AQuickAdvance";
        private const int TutorialCheckpoints = 2;
        private const float TutorialFadeSeconds = 1.5f;
        private const int FinalSongCheckpoint = 13;
        private const float ScorePerDoubleRate = 5000f;
        private const float RateStep = 0.01f;

        public static GameManager Instance { get; private set; }

        [SerializeField] private Conductor conductor;
        [SerializeField] private MusicPlayer music;
        [SerializeField] private CatController cat;
        [SerializeField] private Skewer skewer;
        [SerializeField] private LevelGenerator level;
        [SerializeField] private CameraRig cameraRig;
        [SerializeField] private Backdrop backdrop;
        [SerializeField] private ScoreManager score;
        [SerializeField] private GameUI ui;
        [SerializeField] private PopupManager popups;
        [SerializeField] private SongScriptableObject library;
        [SerializeField] private UnityEngine.UI.Image screenFade;
        [SerializeField] private Volume songTint;

        [SerializeField] private float restartSeconds = 1.2f;

        public event Action<GameState> StateChanged;

        public GameState State { get; private set; } = GameState.Playing;
        public int Section { get; private set; }
        public bool GreenRun { get; private set; }
        public string NowPlaying => normalRun && playing != null ? playing.songName : "";
        public bool TutorialActive => normalRun && playing != null && playing.ID == TutorialSongId;
        public int Checkpoints => checkpoints;

        private bool hasJumped;
        private string currentSongId;
        private Song playing;
        private bool normalRun;
        private int checkpoints;

        private void Awake()
        {
            Instance = this;
            MenuMusic.Release();
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            if (backdrop == null)
                backdrop = FindObjectOfType<Backdrop>();
        }

        private void Start()
        {
            cat.Landed += OnLanded;
            cat.Jumped += OnJumped;
            cat.Stabbed += OnStabbed;
            cat.Launched += OnLaunched;
            cat.ReachedApex += OnReachedApex;
            cat.ChargeMissed += OnChargeMissed;
            cat.IntroImpact += OnIntroImpact;
            cat.IntroFinished += OnIntroFinished;
            cat.Health.Died += OnDied;
            skewer.Bitten += OnBitten;
            skewer.Feasted += OnFeasted;
            skewer.Skewered += OnSkewered;

            StartRun();
        }

        private void OnDestroy()
        {
            cat.Landed -= OnLanded;
            cat.Jumped -= OnJumped;
            cat.Stabbed -= OnStabbed;
            cat.Launched -= OnLaunched;
            cat.ReachedApex -= OnReachedApex;
            cat.ChargeMissed -= OnChargeMissed;
            cat.IntroImpact -= OnIntroImpact;
            cat.IntroFinished -= OnIntroFinished;
            cat.Health.Died -= OnDied;
            skewer.Bitten -= OnBitten;
            skewer.Feasted -= OnFeasted;
            skewer.Skewered -= OnSkewered;
        }

        public void StartRun()
        {
            ResumeClock();
            PickSong();
            PrepareWorld(Environment.TickCount);
            score.BeginRun(cat.transform.position.y, currentSongId);
            cat.SetControl(false);
            SetState(GameState.Playing);
            ui.ShowHud();
            conductor.StartSong(CatController.IntroBeats + IntroLeadBeats);
            music.SetNearness(0f);
            music.SetMood(MusicMood.Climb, true);
            cat.BeginIntro(-CatController.IntroBeats, cameraRig.Top + 1.5f);
        }

        private void PickSong()
        {
            normalRun = string.IsNullOrEmpty(SongStore.RunSongId);
            checkpoints = 0;
            var song = normalRun
                ? SongStore.Find(library, SongStore.TutorialDone ? MainSongId : TutorialSongId)
                : SongStore.RunSong(library);
            playing = song;
            currentSongId = normalRun ? MainSongId : song.ID;
            if (normalRun)
            {
                SongStore.Find(library, MainSongId).clip.LoadAudioData();
                SongStore.Find(library, FinalSongId).clip.LoadAudioData();
            }
            if (backdrop != null)
                backdrop.SetSong(song);
            if (song.redToGreen && !GreenRun)
            {
                GreenRun = true;
                cameraRig.Camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
                songTint.gameObject.SetActive(true);
            }
            conductor.Configure(song.bpm, song.firstBeatSeconds);
            music.SetSong(song);
        }

        public void Pause()
        {
            if (State != GameState.Playing)
                return;
            cat.SetControl(false);
            Sfx.PauseAll();
            conductor.Pause();
            Time.timeScale = 0f;
            SetState(GameState.Paused);
        }

        public void Resume()
        {
            if (State != GameState.Paused)
                return;
            ResumeClock();
            Sfx.ResumeAll();
            cat.SetControl(!cat.InIntro);
            SetState(GameState.Playing);
        }

        public void QuitToMenu()
        {
            score.Commit();
            Time.timeScale = 1f;
            SongStore.RunSongId = null;
            SceneManager.LoadScene(MenuSceneIndex);
        }

        private void ResumeClock()
        {
            Time.timeScale = 1f;
            if (conductor.IsPaused)
                conductor.Resume();
        }

        private void PrepareWorld(int seed)
        {
            Sfx.CancelScheduled();
            Fx.Clear();
            cameraRig.Creeping = false;
            cameraRig.Section = 0;
            Section = 0;
            hasJumped = false;
            cameraRig.SnapTo(0f);
            level.BeginRun(seed, cameraRig.Top, TutorialActive);
            cat.ResetTo(new Vector2(0f, level.StartPlatform.Top), level.StartPlatform);
            cat.Health.ResetLives();
            skewer.Clear();
        }

        private void Update()
        {
            if (State != GameState.Playing)
                return;

            level.Fill(cameraRig.Top);
            level.Cull(cameraRig.Bottom);
            score.SetHeight(cat.MaxHeight);

            int section = level.SectionAt(cat.MaxHeight);
            if (section != Section)
            {
                Section = section;
                cameraRig.Section = section;
            }

            if (playing.speedsUpWithScore)
                conductor.SetRate(1f + Mathf.Round(score.Score / ScorePerDoubleRate / RateStep) * RateStep);
            music.SetMood(cat.IsLaunching ? MusicMood.Flight : MusicMood.Climb);
            music.SetNearness(MovingNearness());

            cameraRig.Creeping = hasJumped && !cat.Settling && !cat.IsLaunching;

            if (!cat.InIntro && cat.transform.position.y < cameraRig.Bottom - FallMargin)
                EndRun(true);
        }

        private float MovingNearness()
        {
            float y = cat.transform.position.y;
            float closest = float.MaxValue;
            foreach (var platform in Platform.All)
                if (platform.Kind == PlatformKind.Moving)
                    closest = Mathf.Min(closest, Mathf.Abs(platform.Top - y));
            return Mathf.InverseLerp(MovingFar, MovingNear, closest);
        }

        private void OnLanded(Platform platform)
        {
            if (State == GameState.Playing && platform != level.StartPlatform)
                hasJumped = true;
            if (State != GameState.Playing || platform.Kind != PlatformKind.Landing)
                return;
            score.SkipTo(platform.Top);
            if (platform.Visited)
                return;
            platform.Visited = true;
            ReachedCheckpoint();
            cameraRig.Shake(1, 0.08f);
            Fx.Ring(cat.transform.position, Palette.White, 20, 8f);
            Sfx.Play(SfxId.Impact);
        }

        private void ReachedCheckpoint()
        {
            checkpoints++;
            if (!normalRun)
                return;
            if (checkpoints == TutorialCheckpoints && playing.ID == TutorialSongId)
            {
                StartCoroutine(FinishTutorial());
            }
            else if (checkpoints == FinalSongCheckpoint)
            {
                SwitchTo(FinalSongId);
            }
        }

        private IEnumerator FinishTutorial()
        {
            SongStore.TutorialDone = true;
            cat.SetControl(false);
            cameraRig.Creeping = false;
            SetState(GameState.GameOver);
            music.FadeOut(TutorialFadeSeconds);
            screenFade.gameObject.SetActive(true);
            for (float t = 0f; t < TutorialFadeSeconds; t += Time.unscaledDeltaTime)
            {
                screenFade.color = new Color(0f, 0f, 0f, t / TutorialFadeSeconds);
                yield return null;
            }
            screenFade.color = Color.black;
            QuitToMenu();
        }

        private void SwitchTo(string songId)
        {
            playing = SongStore.Find(library, songId);
            conductor.SwitchSong(playing);
            if (backdrop != null)
                backdrop.SetSong(playing);
        }

        private void OnJumped()
        {
            if (State != GameState.Playing)
                return;
            if (!TutorialActive)
                hasJumped = true;
        }

        private void OnStabbed(bool caught)
        {
            if (State != GameState.Playing)
                return;
            if (caught)
                cameraRig.Shake(1, 0.06f);
            else
                popups.Show(PopupId.Whiff, cat.transform.position);
        }

        private void OnChargeMissed(bool early)
        {
            if (State == GameState.Playing)
                popups.Show(early ? PopupId.Early : PopupId.Late, cat.transform.position);
        }

        private void OnIntroImpact()
        {
            if (State != GameState.Playing)
                return;
            cameraRig.Shake(2, 0.12f);
            popups.Show(PopupId.Splat, cat.transform.position);
        }

        private void OnIntroFinished()
        {
            if (State != GameState.Playing)
                return;
            cat.SetControl(true);
        }

        private void OnLaunched(Platform pad, int beat)
        {
            if (State != GameState.Playing)
                return;
            level.OpenCorridor(pad, cat.transform.position.x);
            if (backdrop != null)
                backdrop.Flash(0.08f);
        }

        private void OnReachedApex()
        {
            if (State == GameState.Playing)
                level.ClearEnemies();
        }

        private void OnSkewered(int count, Vector3 at)
        {
            if (State != GameState.Playing)
                return;
            if (backdrop != null)
                backdrop.Flash(0.06f);
            popups.Show(PopupId.Combo, at, count);
        }

        private void OnBitten(int index, int points, Vector3 mouth)
        {
            if (State != GameState.Playing)
                return;
            popups.Show(PopupId.Bite, mouth, points, index % 2 == 1);
        }

        private void OnFeasted(int count, int points)
        {
            if (State != GameState.Playing)
                return;
            if (backdrop != null)
                backdrop.Flash(0.12f);
            cameraRig.Shake(1, 0.12f);
            popups.Show(PopupId.Feast, cat.transform.position, count);
            popups.Show(PopupId.FeastPoints, cat.transform.position, points);
        }

        private void OnDied() => EndRun(false);

        private void EndRun(bool fell)
        {
            if (State != GameState.Playing)
                return;
            if (fell)
            {
                cat.TossFork(cameraRig.Bottom + 1.25f);
                if (backdrop != null)
                    backdrop.Gulp();
            }
            cat.SetControl(false);
            Sfx.CancelScheduled();
            music.SetMood(MusicMood.Silent);
            score.Commit();
            Sfx.Play(SfxId.Hurt);
            GameSettings.Vibrate();
            SetState(GameState.GameOver);
            StartCoroutine(RestartSoon());
        }

        private IEnumerator RestartSoon()
        {
            yield return new WaitForSecondsRealtime(restartSeconds);
            if (State == GameState.GameOver)
                StartRun();
        }

        private void SetState(GameState state)
        {
            State = state;
            StateChanged?.Invoke(state);
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
                Pause();
        }

#if !UNITY_EDITOR
        private void OnApplicationFocus(bool focused)
        {
            if (!focused)
                Pause();
        }
#endif
    }
}
