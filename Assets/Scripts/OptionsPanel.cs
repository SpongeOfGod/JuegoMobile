using UnityEngine;
using UnityEngine.UI;

namespace Gluttony
{
    public class OptionsPanel : MonoBehaviour
    {
        private const float ConfirmSeconds = 3f;
        private const string EraseText = "REINICIAR PROGRESO";

        [SerializeField] private SongScriptableObject library;
        [SerializeField] private Button openButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button vibrationButton;
        [SerializeField] private PixelLabel vibrationValue;
        [SerializeField] private Button eraseButton;
        [SerializeField] private PixelLabel eraseLabel;

        private float confirmUntil;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            openButton.onClick.AddListener(Open);
            closeButton.onClick.AddListener(Close);
            vibrationButton.onClick.AddListener(ToggleVibration);
            eraseButton.onClick.AddListener(Erase);
            gameObject.SetActive(false);
        }

        private void Open()
        {
            gameObject.SetActive(true);
            confirmUntil = 0f;
            Refresh();
        }

        private void Close()
        {
            GameSettings.Save();
            gameObject.SetActive(false);
        }

        private void ToggleVibration()
        {
            GameSettings.Vibration = !GameSettings.Vibration;
            GameSettings.Vibrate();
            Refresh();
        }

        private void Erase()
        {
            if (Time.unscaledTime > confirmUntil)
            {
                confirmUntil = Time.unscaledTime + ConfirmSeconds;
                eraseLabel.Text = "¿SEGURO?";
                return;
            }
            confirmUntil = 0f;
            if (ScoreManager.Instance != null)
                ScoreManager.Instance.ForgetBest();
            SongStore.Forget(library);
            Refresh();
        }

        private void Refresh()
        {
            vibrationValue.Text = GameSettings.Vibration ? "SÍ" : "NO";
            eraseLabel.Text = EraseText;
        }

        private void Update()
        {
            if (confirmUntil > 0f && Time.unscaledTime > confirmUntil)
            {
                confirmUntil = 0f;
                eraseLabel.Text = EraseText;
            }
        }
    }
}
