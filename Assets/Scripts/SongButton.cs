using Gluttony;
using UnityEngine;
using UnityEngine.UI;

public class SongButton : MonoBehaviour
{
    [SerializeField] PixelLabel nameLabel;
    [SerializeField] PixelLabel statusLabel;
    [SerializeField] DiscView disc;
    [SerializeField] Button button;

    Song song;
    SongScriptableObject library;

    public Button Button => button;

    public void Initialize(ShopController shop, Song value)
    {
        song = value;
        library = shop.Library;
        nameLabel.Text = song.songName;
        disc.Show(song);
        button.onClick.AddListener(() => shop.OpenSong(song));
    }

    public void Refresh() => statusLabel.Text = SongStore.Status(library, song);
}
