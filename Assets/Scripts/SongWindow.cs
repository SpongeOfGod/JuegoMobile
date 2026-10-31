using UnityEngine;
using UnityEngine.UI;

namespace Gluttony
{
    public class SongWindow : MonoBehaviour
    {
        private const float PreviewSeconds = 30f;
        private const float SingleHeight = 171f;
        private const float TrialHeight = 191f;
        private const float TrialPrimaryY = 25f;
        private const float SinglePrimaryY = 5f;

        [SerializeField] private ShopController shop;
        [SerializeField] private SongPreview preview;
        [SerializeField] private RectTransform panel;
        [SerializeField] private DiscView disc;
        [SerializeField] private Image previewBar;
        [SerializeField] private PixelLabel nameLabel;
        [SerializeField] private PixelLabel packLabel;
        [SerializeField] private PixelLabel statusLabel;
        [SerializeField] private Button primaryButton;
        [SerializeField] private PixelLabel primaryLabel;
        [SerializeField] private Button secondaryButton;
        [SerializeField] private PixelLabel secondaryLabel;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;

        private Song song;
        private int shownSecond = -1;

        private void Awake()
        {
            primaryButton.onClick.AddListener(OnPrimary);
            secondaryButton.onClick.AddListener(Buy);
            closeButton.onClick.AddListener(Close);
            previousButton.onClick.AddListener(() => Browse(-1));
            nextButton.onClick.AddListener(() => Browse(1));
        }

        public void Show(Song value)
        {
            song = value;
            gameObject.SetActive(true);
            disc.Show(song);
            nameLabel.Text = song.songName;
            preview.Play(song, PreviewSeconds);
            Refresh();
        }

        private void Refresh()
        {
            bool owned = SongStore.IsOwned(song);
            bool trial = SongStore.IsTrial(shop.Library, song);
            packLabel.Text = song.pack;
            statusLabel.Text = SongStore.Status(shop.Library, song);
            var primaryRect = (RectTransform)primaryButton.transform;
            primaryRect.anchoredPosition = new Vector2(primaryRect.anchoredPosition.x, trial ? TrialPrimaryY : SinglePrimaryY);
            primaryLabel.Text = owned ? "JUGAR" : trial ? "PROBAR" : "COMPRAR";
            secondaryButton.gameObject.SetActive(trial);
            secondaryLabel.Text = "COMPRAR " + SongStore.FormatPrice(song);
            panel.sizeDelta = new Vector2(panel.sizeDelta.x, trial ? TrialHeight : SingleHeight);
            shop.Refresh();
        }

        private void OnPrimary()
        {
            if (SongStore.IsOwned(song) || SongStore.IsTrial(shop.Library, song))
            {
                preview.StopNow();
                shop.PlaySong(song);
            }
            else
            {
                Buy();
            }
        }

        private void Buy()
        {
            SongStore.Buy(song);
            UiSounds.Play(UiSound.Approved);
            Refresh();
        }

        private void Browse(int direction)
        {
            var songs = shop.Songs;
            for (int i = 0; i < songs.Count; i++)
            {
                if (songs[i].ID != song.ID)
                    continue;
                preview.StopNow();
                Show(songs[(i + direction + songs.Count) % songs.Count]);
                return;
            }
        }

        private void Close()
        {
            preview.Stop();
            gameObject.SetActive(false);
        }

        private void Update()
        {
            disc.Spinning = preview.IsPlaying;
            previewBar.fillAmount = preview.IsPlaying ? 1f - preview.Remaining / PreviewSeconds : 0f;
            int second = (int)SongStore.TimeUntilNextDaily().TotalSeconds;
            if (second == shownSecond)
                return;
            shownSecond = second;
            statusLabel.Text = SongStore.Status(shop.Library, song);
        }
    }
}
