using UnityEngine;

namespace Gluttony
{
    public class TutorialCoach : MonoBehaviour
    {
        private const string MoveText = "DESLIZA PARA MOVERTE";
        private const string JumpText = "MANTÉN PULSADO\nPARA SALTAR";
        private const string FoodText = "¡PINCHA A TODA LA COMIDA!";
        private const float MinHintSeconds = 2.5f;

        private static bool moved;
        private static bool jumped;
        private static bool fed;

        [SerializeField] private PixelLabel hintLabel;
        [SerializeField] private PixelLabel countLabel;

        private float hintShownAt;
        private bool onTrampoline;
        private CatController cat;

        private void Start()
        {
            cat = CatController.Instance;
            cat.Input.Swiped += OnSwiped;
            cat.Jumped += OnJumped;
            cat.ChargeMissed += OnChargeMissed;
            cat.Landed += OnLanded;
        }

        private void OnDestroy()
        {
            if (cat == null)
                return;
            cat.Input.Swiped -= OnSwiped;
            cat.Jumped -= OnJumped;
            cat.ChargeMissed -= OnChargeMissed;
            cat.Landed -= OnLanded;
        }

        private void OnSwiped(int direction) => moved = true;

        private void OnJumped() => jumped = true;

        private void OnChargeMissed(bool early) => jumped = true;

        private void OnLanded(Platform platform)
        {
            if (fed)
                return;
            if (platform.Kind == PlatformKind.Launch)
            {
                onTrampoline = true;
            }
            else if (platform.Kind == PlatformKind.Landing && onTrampoline)
            {
                onTrampoline = false;
                fed = true;
            }
        }

        private void Update()
        {
            var game = GameManager.Instance;
            if (game == null || !game.TutorialActive || game.State == GameState.GameOver)
            {
                Show("", "");
                return;
            }

            string count = "";
            if (cat.Charging && game.Checkpoints < 1)
                count = cat.InReleaseWindow ? "YA" : cat.ChargeLevel > 0 ? cat.ChargeLevel.ToString() : "";
            Show(HeldHint(CurrentHint()), count);
        }

        private string HeldHint(string next)
        {
            if (next == hintLabel.Text)
                return next;
            if (hintLabel.Text != "" && Time.unscaledTime - hintShownAt < MinHintSeconds)
                return hintLabel.Text;
            hintShownAt = Time.unscaledTime;
            return next;
        }

        private string CurrentHint()
        {
            if (onTrampoline)
                return FoodText;
            if (!cat.ControlEnabled)
                return "";
            if (!moved)
                return MoveText;
            return !jumped ? JumpText : "";
        }

        private void Show(string hint, string count)
        {
            hintLabel.Text = hint;
            countLabel.Text = count;
        }
    }
}
