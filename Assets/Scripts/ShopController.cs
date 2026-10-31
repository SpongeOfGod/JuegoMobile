using System.Collections.Generic;
using Gluttony;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ShopController : MonoBehaviour
{
    const int GameSceneIndex = 2;
    const int MinDragPixels = 12;
    const int DragScreenDivisor = 90;

    [SerializeField] SongScriptableObject songScriptable;
    [SerializeField] SongButton SongButtonPrefab;
    [SerializeField] PixelLabel PackHeaderPrefab;
    [SerializeField] RectTransform SongContainer;
    [SerializeField] SongWindow songWindow;

    readonly List<SongButton> songButtons = new();
    int shownSecond = -1;
    string dailyId;

    public SongScriptableObject Library => songScriptable;
    public IReadOnlyList<Song> Songs => songScriptable.AllSongs;

    private void Start()
    {
        Application.targetFrameRate = 60;
        if (EventSystem.current != null)
            EventSystem.current.pixelDragThreshold = Mathf.Max(MinDragPixels, Screen.height / DragScreenDivisor);

        string pack = null;
        foreach (var song in songScriptable.AllSongs)
        {
            if (song.pack != pack)
            {
                pack = song.pack;
                Instantiate(PackHeaderPrefab, SongContainer).Text = pack;
            }
            var songButton = Instantiate(SongButtonPrefab, SongContainer);
            songButton.Initialize(this, song);
            UiSounds.Hook(songButton.Button);
            songButtons.Add(songButton);
        }
        Refresh();
    }

    private void Update()
    {
        int second = (int)SongStore.TimeUntilNextDaily().TotalSeconds;
        if (second == shownSecond)
            return;
        shownSecond = second;
        var daily = SongStore.Daily(songScriptable);
        if ((daily != null ? daily.ID : null) != dailyId)
            Refresh();
        foreach (var songButton in songButtons)
            songButton.Refresh();
    }

    public void Refresh()
    {
        var daily = SongStore.Daily(songScriptable);
        dailyId = daily != null ? daily.ID : null;
        foreach (var songButton in songButtons)
            songButton.Refresh();
        LayoutRebuilder.ForceRebuildLayoutImmediate(SongContainer);
    }

    public void OpenSong(Song song) => songWindow.Show(song);

    public void PlaySong(Song song)
    {
        SongStore.RunSongId = song.ID;
        SceneManager.LoadScene(GameSceneIndex);
    }
}
