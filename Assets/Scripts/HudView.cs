using UnityEngine;
using UnityEngine.UI;

namespace Gluttony
{
    public class HudView : MonoBehaviour
    {
        private const int ScoreBounce = 2;
        private const int IconBounce = 1;

        [SerializeField] private PixelLabel scoreText;
        [SerializeField] private PixelLabel bestText;
        [SerializeField] private Image livesImage;
        [SerializeField] private Sprite[] livesSprites;
        [SerializeField] private Button pauseButton;
        [SerializeField] private CatHealth health;

        private int shownScore = -1;
        private int shownBest = -1;
        private int shownLives = -1;
        private RectTransform pauseRect;
        private Vector2 scoreHome;
        private Vector2 livesHome;
        private Vector2 pauseHome;

        private void Awake()
        {
            pauseRect = (RectTransform)pauseButton.transform;
            scoreHome = scoreText.rectTransform.anchoredPosition;
            livesHome = livesImage.rectTransform.anchoredPosition;
            pauseHome = pauseRect.anchoredPosition;
            pauseButton.onClick.AddListener(() =>
            {
                Sfx.Play(SfxId.Click);
                if (GameManager.Instance.State == GameState.Paused)
                    GameManager.Instance.Resume();
                else
                    GameManager.Instance.Pause();
            });
        }

        private void Update()
        {
            var score = ScoreManager.Instance;
            if (score != null)
            {
                if (score.Score != shownScore)
                {
                    shownScore = score.Score;
                    scoreText.Text = shownScore.ToString();
                }
                int best = Mathf.Max(score.Best, score.Score);
                if (best != shownBest)
                {
                    shownBest = best;
                    bestText.Text = "RÉCORD " + best;
                }
            }

            if (health.Lives != shownLives)
            {
                shownLives = health.Lives;
                livesImage.enabled = shownLives > 0;
                if (shownLives > 0)
                    livesImage.sprite = livesSprites[Mathf.Clamp(shownLives - 1, 0, livesSprites.Length - 1)];
            }

            var game = GameManager.Instance;
            bool paused = game != null && game.State == GameState.Paused;
            bool dim = paused && Mathf.Repeat(Time.unscaledTime * 2f, 1f) >= 0.5f;
            pauseButton.targetGraphic.color = dim ? new Color(1f, 1f, 1f, 0.25f) : Color.white;

            int icon = Conductor.Bounce(IconBounce);
            scoreText.rectTransform.anchoredPosition = scoreHome + Vector2.up * Conductor.Bounce(ScoreBounce);
            livesImage.rectTransform.anchoredPosition = livesHome + Vector2.down * icon;
            pauseRect.anchoredPosition = pauseHome + Vector2.down * icon;
        }
    }
}
