using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Gluttony
{
    public class DefeatScreen : MonoBehaviour
    {
        private const int RowPixels = 10;
        private const int NameGap = 20;

        [SerializeField] private Leaderboard leaderboard;
        [SerializeField] private PixelLabel scoreLabel;
        [SerializeField] private PixelLabel recordLabel;
        [SerializeField] private PixelLabel boardTitle;
        [SerializeField] private PixelLabel ranksLabel;
        [SerializeField] private PixelLabel namesLabel;
        [SerializeField] private PixelLabel scoresLabel;
        [SerializeField] private PixelLabel statusLabel;
        [SerializeField] private RectTransform highlight;
        [SerializeField] private Button nameButton;
        [SerializeField] private PixelLabel nameLabel;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button menuButton;

        public bool Visible => gameObject.activeSelf;

        private string songId;
        private int runScore;
        private float highlightTop;
        private RectTransform panel;
        private RectTransform retryRect;
        private RectTransform menuRect;
        private float panelHeight;
        private float retryY;
        private float menuY;
        private bool typing;
        private string typed = "";
        private TouchScreenKeyboard keyboard;

        private void Awake()
        {
            highlightTop = highlight.anchoredPosition.y;
            panel = (RectTransform)nameButton.transform.parent;
            retryRect = (RectTransform)retryButton.transform;
            menuRect = (RectTransform)menuButton.transform;
            panelHeight = panel.sizeDelta.y;
            retryY = retryRect.anchoredPosition.y;
            menuY = menuRect.anchoredPosition.y;
            retryButton.onClick.AddListener(Retry);
            menuButton.onClick.AddListener(Menu);
            nameButton.onClick.AddListener(ToggleTyping);
        }

        private void OnEnable() => leaderboard.Updated += OnUpdated;

        private void OnDisable() => leaderboard.Updated -= OnUpdated;

        private void OnUpdated(string id)
        {
            if (id == songId)
                RefreshBoard();
        }

        public void Show(int score, int record, bool newRecord, string id, string songName)
        {
            gameObject.SetActive(true);
            songId = id;
            runScore = score;
            scoreLabel.Text = score.ToString(CultureInfo.InvariantCulture);
            recordLabel.Text = newRecord ? "¡NUEVO RÉCORD!" : "RÉCORD " + record.ToString(CultureInfo.InvariantCulture);
            boardTitle.Text = "RANKING (" + songName + "):";
            StopTyping();
            RefreshName();
            leaderboard.Queue(songId, runScore);
            RefreshBoard();
            leaderboard.Sync(songId, false);
        }

        private void RefreshBoard()
        {
            var entries = leaderboard.Entries(songId);
            if (leaderboard.HasFailed(songId) && !leaderboard.IsBusy(songId))
                ShowStatus("SIN CONEXIÓN");
            else if (entries == null)
                ShowStatus("CARGANDO...");
            else
                ShowEntries(entries);
        }

        private void ShowStatus(string status)
        {
            statusLabel.Text = status;
            ranksLabel.Text = "";
            namesLabel.Text = "";
            scoresLabel.Text = "";
            highlight.gameObject.SetActive(false);
            SetNameVisible(false);
        }

        private void ShowEntries(List<LeaderboardEntry> entries)
        {
            string me = Leaderboard.PlayerId;
            int mine = -1;
            if (runScore > 0)
                for (int i = 0; i < entries.Count; i++)
                    if (entries[i].Id == me && entries[i].Score == runScore)
                        mine = i;
            if (mine >= Leaderboard.TopCount)
                mine = -1;

            int rows = Mathf.Min(Leaderboard.TopCount, entries.Count);
            if (rows == 0)
            {
                ShowStatus("¡SÉ EL PRIMERO!");
                return;
            }

            var ranks = new StringBuilder();
            var names = new StringBuilder();
            var scores = new StringBuilder();
            for (int row = 0; row < rows; row++)
            {
                if (row > 0)
                {
                    ranks.Append('\n');
                    names.Append('\n');
                    scores.Append('\n');
                }
                var entry = entries[row];
                ranks.Append((row + 1).ToString(CultureInfo.InvariantCulture)).Append('.');
                names.Append(entry.Name);
                scores.Append(entry.Score.ToString(CultureInfo.InvariantCulture));
            }

            statusLabel.Text = "";
            ranksLabel.Text = ranks.ToString();
            namesLabel.Text = names.ToString();
            scoresLabel.Text = scores.ToString();
            highlight.gameObject.SetActive(mine >= 0);
            if (mine >= 0)
                highlight.anchoredPosition = new Vector2(highlight.anchoredPosition.x, highlightTop - mine * RowPixels);
            SetNameVisible(mine >= 0);
        }

        private void SetNameVisible(bool visible)
        {
            if (!visible && typing)
            {
                StopTyping();
                RefreshName();
            }
            nameButton.gameObject.SetActive(visible);
            float lift = visible ? 0f : NameGap;
            panel.sizeDelta = new Vector2(panel.sizeDelta.x, panelHeight - lift);
            retryRect.anchoredPosition = new Vector2(retryRect.anchoredPosition.x, retryY + lift);
            menuRect.anchoredPosition = new Vector2(menuRect.anchoredPosition.x, menuY + lift);
        }

        private void ToggleTyping()
        {
            if (typing)
            {
                StopTyping();
                RefreshName();
                return;
            }
            typing = true;
            typed = Leaderboard.PlayerName;
            if (TouchScreenKeyboard.isSupported)
            {
                keyboard = TouchScreenKeyboard.Open(typed, TouchScreenKeyboardType.Default, false, false, false, false, "TU NOMBRE", Leaderboard.MaxNameLength);
                if (keyboard != null)
                    keyboard.characterLimit = Leaderboard.MaxNameLength;
            }
        }

        private void Type(string value)
        {
            typed = Leaderboard.CleanName(value);
            if (typed.Length == 0 || typed == Leaderboard.PlayerName)
                return;
            Leaderboard.PlayerName = typed;
            RefreshBoard();
        }

        private void StopTyping()
        {
            typing = false;
            typed = "";
            if (keyboard != null && keyboard.active)
                keyboard.active = false;
            keyboard = null;
        }

        private void RefreshName()
        {
            nameLabel.Text = "NOMBRE: " + (typing ? typed : Leaderboard.PlayerName);
        }

        private void Update()
        {
            if (!typing)
                return;

            if (keyboard != null)
            {
                Type(keyboard.text);
                if (keyboard.status != TouchScreenKeyboard.Status.Visible)
                    StopTyping();
            }
            else
            {
                string value = typed;
                foreach (char c in Input.inputString)
                {
                    if (c == '\b')
                    {
                        if (value.Length > 0)
                            value = value.Substring(0, value.Length - 1);
                    }
                    else if (c == '\n' || c == '\r')
                    {
                        typing = false;
                    }
                    else
                    {
                        value += c;
                    }
                }
                Type(value);
                if (!typing || Input.GetKeyDown(KeyCode.Escape))
                    StopTyping();
            }
            RefreshName();
        }

        private void Close()
        {
            StopTyping();
            gameObject.SetActive(false);
        }

        private void Retry()
        {
            Close();
            GameManager.Instance.Retry();
        }

        private void Menu()
        {
            Close();
            leaderboard.Push();
            GameManager.Instance.QuitToMenu();
        }
    }
}
